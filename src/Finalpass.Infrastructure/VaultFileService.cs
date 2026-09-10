using Finalpass.Core;
using Finalpass.Cryptography;

namespace Finalpass.Infrastructure;

public static class VaultFileService
{
    private const int MaximumFileLength =
        VaultFileCodec.HeaderLength + VaultFileCodec.MaximumPlaintextPayloadLength + Sodium.TagBytes;

    public static async Task<OpenedVault> CreateAsync(
        string path,
        VaultDocument document,
        ReadOnlyMemory<char> masterPassword,
        VaultKdfParameters? kdfParameters = null,
        bool replaceExisting = false,
        CancellationToken cancellationToken = default)
    {
        string fullPath = NormalizePath(path);
        string? directory = System.IO.Path.GetDirectoryName(fullPath);
        if (directory is null || !Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException("The vault directory does not exist.");
        }

        FileStream lockHandle = AcquireLock(fullPath);
        VaultSession? session = null;
        try
        {
            bool destinationExists = File.Exists(fullPath);
            if (destinationExists && !replaceExisting)
            {
                throw new IOException("A file already exists at the selected vault path.");
            }

            FileFingerprint? destinationFingerprint = destinationExists
                ? await FileFingerprint.ComputeAsync(fullPath, cancellationToken).ConfigureAwait(false)
                : null;

            session = VaultFileCodec.Create(document, masterPassword.Span, kdfParameters);
            byte[] encoded = session.Encode();
            await CommitAsync(
                fullPath,
                encoded,
                session,
                destinationExists,
                destinationFingerprint,
                cancellationToken).ConfigureAwait(false);
            FileFingerprint fingerprint = await FileFingerprint
                .ComputeAsync(fullPath, cancellationToken)
                .ConfigureAwait(false);

            OpenedVault opened = new(fullPath, session, fingerprint, lockHandle);
            session = null;
            lockHandle = null!;
            return opened;
        }
        finally
        {
            session?.Dispose();
            lockHandle?.Dispose();
        }
    }

    public static async Task<OpenedVault> OpenAsync(
        string path,
        ReadOnlyMemory<char> masterPassword,
        CancellationToken cancellationToken = default)
    {
        string fullPath = NormalizePath(path);
        FileStream lockHandle = AcquireLock(fullPath);
        VaultSession? session = null;
        try
        {
            byte[] file = await ReadBoundedAsync(fullPath, cancellationToken).ConfigureAwait(false);
            session = VaultFileCodec.Open(file, masterPassword.Span);
            FileFingerprint fingerprint = await FileFingerprint
                .ComputeAsync(fullPath, cancellationToken)
                .ConfigureAwait(false);

            OpenedVault opened = new(fullPath, session, fingerprint, lockHandle);
            session = null;
            lockHandle = null!;
            return opened;
        }
        finally
        {
            session?.Dispose();
            lockHandle?.Dispose();
        }
    }

    public static async Task<OpenedVault> OpenReadOnlyAsync(
        string path,
        ReadOnlyMemory<char> masterPassword,
        CancellationToken cancellationToken = default)
    {
        string fullPath = NormalizePath(path);
        byte[] file = await ReadBoundedAsync(fullPath, cancellationToken).ConfigureAwait(false);
        VaultSession? session = null;
        try
        {
            session = VaultFileCodec.Open(file, masterPassword.Span);
            FileFingerprint fingerprint = await FileFingerprint
                .ComputeAsync(fullPath, cancellationToken)
                .ConfigureAwait(false);
            OpenedVault opened = new(fullPath, session, fingerprint, null, isReadOnly: true);
            session = null;
            return opened;
        }
        finally
        {
            session?.Dispose();
        }
    }

    public static async Task SaveAsync(
        OpenedVault openedVault,
        CancellationToken cancellationToken = default)
    {
        await SaveCoreAsync(openedVault, null, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task SaveAsync(
        OpenedVault openedVault,
        Action<VaultSaveStage> testHook,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(testHook);
        await SaveCoreAsync(openedVault, testHook, cancellationToken).ConfigureAwait(false);
    }

    private static async Task SaveCoreAsync(
        OpenedVault openedVault,
        Action<VaultSaveStage>? testHook,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(openedVault);
        if (openedVault.IsReadOnly)
        {
            throw new InvalidOperationException("A read-only vault cannot be saved.");
        }

        FileFingerprint current = await FileFingerprint
            .ComputeAsync(openedVault.Path, cancellationToken)
            .ConfigureAwait(false);
        if (current != openedVault.Fingerprint)
        {
            throw new VaultConcurrencyException(
                "The vault changed after it was opened. It was not overwritten.");
        }

        VaultDocument document = openedVault.Session.Document;
        if (document.Revision == ulong.MaxValue)
        {
            throw new InvalidOperationException("The vault revision cannot be incremented.");
        }

        ulong previousRevision = document.Revision;
        DateTimeOffset previousUpdatedUtc = document.UpdatedUtc;
        document.Revision++;
        document.UpdatedUtc = DateTimeOffset.UtcNow;

        byte[]? encoded = null;
        try
        {
            encoded = openedVault.Session.Encode();
            await CommitAsync(
                openedVault.Path,
                encoded,
                openedVault.Session,
                true,
                current,
                cancellationToken,
                testHook).ConfigureAwait(false);
            openedVault.Fingerprint = await FileFingerprint
                .ComputeAsync(openedVault.Path, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            if (encoded is not null &&
                await DestinationMatchesAsync(
                    openedVault.Path,
                    encoded,
                    cancellationToken).ConfigureAwait(false))
            {
                openedVault.Fingerprint = await FileFingerprint
                    .ComputeAsync(openedVault.Path, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            document.Revision = previousRevision;
            document.UpdatedUtc = previousUpdatedUtc;
            throw;
        }
    }

    public static async Task ChangeMasterPasswordAsync(
        OpenedVault openedVault,
        ReadOnlyMemory<char> currentMasterPassword,
        ReadOnlyMemory<char> newMasterPassword,
        VaultKdfParameters? newKdfParameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(openedVault);
        if (openedVault.IsReadOnly)
        {
            throw new InvalidOperationException(
                "The master password cannot be changed while the vault is read-only.");
        }

        FileFingerprint currentFingerprint = await FileFingerprint
            .ComputeAsync(openedVault.Path, cancellationToken)
            .ConfigureAwait(false);
        if (currentFingerprint != openedVault.Fingerprint)
        {
            throw new VaultConcurrencyException(
                "The vault changed after it was opened. Its master password was not changed.");
        }

        byte[] encoded = await ReadBoundedAsync(openedVault.Path, cancellationToken).ConfigureAwait(false);
        using (VaultSession verification = VaultFileCodec.Open(encoded, currentMasterPassword.Span))
        {
            // Successful authenticated open proves knowledge of the current password.
        }

        VaultKdfParameters previousParameters = openedVault.Session.KdfParameters;
        openedVault.Session.ChangeMasterPassword(newMasterPassword.Span, newKdfParameters);
        try
        {
            await SaveAsync(openedVault, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            openedVault.Session.ChangeMasterPassword(currentMasterPassword.Span, previousParameters);
            throw;
        }
    }

    public static async Task BackUpAsAsync(
        OpenedVault openedVault,
        string destinationPath,
        bool replaceExisting = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(openedVault);
        string destination = NormalizePath(destinationPath);
        if (string.Equals(openedVault.Path, destination, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("The backup destination must differ from the open vault.");
        }

        FileFingerprint sourceBefore = await FileFingerprint
            .ComputeAsync(openedVault.Path, cancellationToken)
            .ConfigureAwait(false);
        if (sourceBefore != openedVault.Fingerprint)
        {
            throw new VaultConcurrencyException(
                "The vault changed after it was opened. A backup was not created.");
        }

        bool destinationExists = File.Exists(destination);
        if (destinationExists && !replaceExisting)
        {
            throw new IOException("A file already exists at the backup destination.");
        }

        FileFingerprint? destinationBefore = destinationExists
            ? await FileFingerprint.ComputeAsync(destination, cancellationToken).ConfigureAwait(false)
            : null;

        string directory = System.IO.Path.GetDirectoryName(destination)!;
        string temporary = System.IO.Path.Combine(
            directory,
            $".{System.IO.Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (FileStream source = new(
                openedVault.Path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (FileStream destinationStream = new(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await source.CopyToAsync(destinationStream, cancellationToken).ConfigureAwait(false);
                await destinationStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                destinationStream.Flush(true);
            }

            FileFingerprint backup = await FileFingerprint
                .ComputeAsync(temporary, cancellationToken)
                .ConfigureAwait(false);
            if (backup != sourceBefore)
            {
                throw new IOException("The backup could not be verified.");
            }

            if (destinationBefore is not null)
            {
                FileFingerprint destinationNow = await FileFingerprint
                    .ComputeAsync(destination, cancellationToken)
                    .ConfigureAwait(false);
                if (destinationNow != destinationBefore)
                {
                    throw new VaultConcurrencyException(
                        "The backup destination changed while the copy was being prepared.");
                }

                File.Replace(temporary, destination, destination + ".bak", true);
            }
            else
            {
                File.Move(temporary, destination, false);
            }
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static async Task CommitAsync(
        string destination,
        byte[] encoded,
        VaultSession session,
        bool replace,
        FileFingerprint? expectedDestinationFingerprint,
        CancellationToken cancellationToken,
        Action<VaultSaveStage>? testHook = null)
    {
        string directory = System.IO.Path.GetDirectoryName(destination)!;
        string filename = System.IO.Path.GetFileName(destination);
        string temporaryPath = System.IO.Path.Combine(
            directory,
            $".{filename}.{Guid.NewGuid():N}.tmp");

        try
        {
            testHook?.Invoke(VaultSaveStage.BeforeWrite);
            await using (FileStream temporary = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                if (testHook is null)
                {
                    await temporary.WriteAsync(encoded, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    int split = Math.Max(1, encoded.Length / 2);
                    await temporary.WriteAsync(
                        encoded.AsMemory(0, split),
                        cancellationToken).ConfigureAwait(false);
                    testHook(VaultSaveStage.DuringWrite);
                    await temporary.WriteAsync(
                        encoded.AsMemory(split),
                        cancellationToken).ConfigureAwait(false);
                }

                testHook?.Invoke(VaultSaveStage.BeforeFlush);
                await temporary.FlushAsync(cancellationToken).ConfigureAwait(false);
                temporary.Flush(true);
            }

            byte[] persisted = await ReadBoundedAsync(temporaryPath, cancellationToken).ConfigureAwait(false);
            session.ValidateEncodedSnapshot(persisted);

            if (replace)
            {
                testHook?.Invoke(VaultSaveStage.BeforeReplace);
                FileFingerprint latest = await FileFingerprint
                    .ComputeAsync(destination, cancellationToken)
                    .ConfigureAwait(false);
                if (latest != expectedDestinationFingerprint)
                {
                    throw new VaultConcurrencyException(
                        "The destination changed while the encrypted snapshot was being prepared.");
                }

                File.Replace(temporaryPath, destination, destination + ".bak", true);
                testHook?.Invoke(VaultSaveStage.AfterReplaceAndBackup);
            }
            else
            {
                File.Move(temporaryPath, destination, false);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static async Task<bool> DestinationMatchesAsync(
        string path,
        ReadOnlyMemory<byte> expected,
        CancellationToken cancellationToken)
    {
        try
        {
            byte[] actual = await ReadBoundedAsync(path, cancellationToken).ConfigureAwait(false);
            return actual.AsSpan().SequenceEqual(expected.Span);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length is < VaultFileCodec.HeaderLength or > MaximumFileLength)
        {
            throw new VaultFileTooLargeException();
        }

        byte[] content = GC.AllocateUninitializedArray<byte>(checked((int)stream.Length));
        await stream.ReadExactlyAsync(content, cancellationToken).ConfigureAwait(false);
        if (stream.ReadByte() != -1)
        {
            throw new IOException("The vault changed while it was being read.");
        }

        return content;
    }

    private static FileStream AcquireLock(string vaultPath)
    {
        try
        {
            return new FileStream(
                vaultPath + ".lock",
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                1,
                FileOptions.DeleteOnClose);
        }
        catch (IOException exception)
        {
            throw new VaultConcurrencyException(
                "The vault is already open in another Finalpass process.",
                exception);
        }
    }

    private static string NormalizePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return System.IO.Path.GetFullPath(path);
    }
}

public sealed class VaultConcurrencyException(string message, Exception? innerException = null)
    : IOException(message, innerException);

public sealed class VaultFileTooLargeException()
    : IOException("The vault file size is outside the supported range.");

internal enum VaultSaveStage
{
    BeforeWrite,
    DuringWrite,
    BeforeFlush,
    BeforeReplace,
    AfterReplaceAndBackup,
}
