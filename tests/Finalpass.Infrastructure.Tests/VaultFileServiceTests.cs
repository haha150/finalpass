using Finalpass.Core;
using Finalpass.Cryptography;

namespace Finalpass.Infrastructure.Tests;

public sealed class VaultFileServiceTests
{
    private static readonly VaultKdfParameters TestKdf = new(1, 64UL * 1024 * 1024);

    [Fact]
    public async Task CreateSaveBackupAndOpenRoundTrip()
    {
        using TemporaryDirectory directory = new();
        string vaultPath = Path.Combine(directory.Path, "personal.fpass");
        string backupPath = Path.Combine(directory.Path, "manual-backup.fpass");
        VaultDocument document = CreateDocument();

        using (OpenedVault opened = await VaultFileService.CreateAsync(
            vaultPath,
            document,
            "master password".AsMemory(),
            TestKdf))
        {
            opened.Session.Document.Entries.Add(CreateEntry());
            await VaultFileService.SaveAsync(opened);
            await VaultFileService.BackUpAsAsync(opened, backupPath);

            Assert.Equal<ulong>(1, opened.Session.Document.Revision);
            Assert.True(File.Exists(vaultPath + ".bak"));
            Assert.Equal(await File.ReadAllBytesAsync(vaultPath), await File.ReadAllBytesAsync(backupPath));
        }

        using OpenedVault reopened = await VaultFileService.OpenAsync(
            vaultPath,
            "master password".AsMemory());
        Assert.Single(reopened.Session.Document.Entries);
        Assert.Equal("generated-secret", reopened.Session.Document.Entries[0].Password);
    }

    [Fact]
    public async Task SecondWriterIsRejected()
    {
        using TemporaryDirectory directory = new();
        string vaultPath = Path.Combine(directory.Path, "personal.fpass");
        using OpenedVault first = await VaultFileService.CreateAsync(
            vaultPath,
            CreateDocument(),
            "master password".AsMemory(),
            TestKdf);

        await Assert.ThrowsAsync<VaultConcurrencyException>(
            () => VaultFileService.OpenAsync(vaultPath, "master password".AsMemory()));
    }

    [Fact]
    public async Task LockedVaultCanBeOpenedAsReadOnlyButCannotBeSaved()
    {
        using TemporaryDirectory directory = new();
        string vaultPath = Path.Combine(directory.Path, "personal.fpass");
        using OpenedVault writer = await VaultFileService.CreateAsync(
            vaultPath,
            CreateDocument(),
            "master password".AsMemory(),
            TestKdf);

        using OpenedVault reader = await VaultFileService.OpenReadOnlyAsync(
            vaultPath,
            "master password".AsMemory());

        Assert.True(reader.IsReadOnly);
        Assert.Equal("Personal", reader.Session.Document.Name);
        await Assert.ThrowsAsync<InvalidOperationException>(() => VaultFileService.SaveAsync(reader));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            VaultFileService.ChangeMasterPasswordAsync(
                reader,
                "master password".AsMemory(),
                "new password".AsMemory(),
                TestKdf));
    }

    [Fact]
    public async Task ExternalModificationPreventsOverwrite()
    {
        using TemporaryDirectory directory = new();
        string vaultPath = Path.Combine(directory.Path, "personal.fpass");
        using OpenedVault opened = await VaultFileService.CreateAsync(
            vaultPath,
            CreateDocument(),
            "master password".AsMemory(),
            TestKdf);
        await File.AppendAllTextAsync(vaultPath, "modified");

        await Assert.ThrowsAsync<VaultConcurrencyException>(() => VaultFileService.SaveAsync(opened));
        Assert.Equal<ulong>(0, opened.Session.Document.Revision);
    }

    [Fact]
    public async Task BackupRefusesToOverwriteExistingFile()
    {
        using TemporaryDirectory directory = new();
        string vaultPath = Path.Combine(directory.Path, "personal.fpass");
        string backupPath = Path.Combine(directory.Path, "backup.fpass");
        using OpenedVault opened = await VaultFileService.CreateAsync(
            vaultPath,
            CreateDocument(),
            "master password".AsMemory(),
            TestKdf);
        await File.WriteAllTextAsync(backupPath, "existing");

        await Assert.ThrowsAsync<IOException>(() => VaultFileService.BackUpAsAsync(opened, backupPath));
        Assert.Equal("existing", await File.ReadAllTextAsync(backupPath));
    }

    [Fact]
    public async Task ExplicitReplacementPreservesPreviousDestinationAsBackup()
    {
        using TemporaryDirectory directory = new();
        string vaultPath = Path.Combine(directory.Path, "personal.fpass");
        await File.WriteAllTextAsync(vaultPath, "previous content");

        using OpenedVault opened = await VaultFileService.CreateAsync(
            vaultPath,
            CreateDocument(),
            "master password".AsMemory(),
            TestKdf,
            replaceExisting: true);

        Assert.Equal("previous content", await File.ReadAllTextAsync(vaultPath + ".bak"));
        Assert.NotEqual("previous content", await File.ReadAllTextAsync(vaultPath));
    }

    [Fact]
    public async Task ExplicitBackupReplacementPreservesPreviousDestination()
    {
        using TemporaryDirectory directory = new();
        string vaultPath = Path.Combine(directory.Path, "personal.fpass");
        string backupPath = Path.Combine(directory.Path, "backup.fpass");
        using OpenedVault opened = await VaultFileService.CreateAsync(
            vaultPath,
            CreateDocument(),
            "master password".AsMemory(),
            TestKdf);
        await File.WriteAllTextAsync(backupPath, "previous backup");

        await VaultFileService.BackUpAsAsync(opened, backupPath, replaceExisting: true);

        Assert.Equal("previous backup", await File.ReadAllTextAsync(backupPath + ".bak"));
        Assert.Equal(await File.ReadAllBytesAsync(vaultPath), await File.ReadAllBytesAsync(backupPath));
    }

    [Fact]
    public async Task MasterPasswordChangeRequiresCurrentPasswordAndPersistsNewPassword()
    {
        using TemporaryDirectory directory = new();
        string vaultPath = Path.Combine(directory.Path, "personal.fpass");
        using (OpenedVault opened = await VaultFileService.CreateAsync(
            vaultPath,
            CreateDocument(),
            "old master password".AsMemory(),
            TestKdf))
        {
            await Assert.ThrowsAsync<VaultAuthenticationException>(() =>
                VaultFileService.ChangeMasterPasswordAsync(
                    opened,
                    "incorrect password".AsMemory(),
                    "new master password".AsMemory(),
                    TestKdf));

            await VaultFileService.ChangeMasterPasswordAsync(
                opened,
                "old master password".AsMemory(),
                "new master password".AsMemory(),
                TestKdf);
        }

        await Assert.ThrowsAsync<VaultAuthenticationException>(() =>
            VaultFileService.OpenAsync(vaultPath, "old master password".AsMemory()));
        using OpenedVault reopened = await VaultFileService.OpenAsync(
            vaultPath,
            "new master password".AsMemory());
        Assert.Equal("Personal", reopened.Session.Document.Name);
    }

    [Theory]
    [InlineData((int)VaultSaveStage.BeforeWrite)]
    [InlineData((int)VaultSaveStage.DuringWrite)]
    [InlineData((int)VaultSaveStage.BeforeFlush)]
    [InlineData((int)VaultSaveStage.BeforeReplace)]
    public async Task InterruptedSaveLeavesPreviousVaultUntouched(int failureStageValue)
    {
        VaultSaveStage failureStage = (VaultSaveStage)failureStageValue;
        using TemporaryDirectory directory = new();
        string vaultPath = Path.Combine(directory.Path, "personal.fpass");
        using OpenedVault opened = await VaultFileService.CreateAsync(
            vaultPath,
            CreateDocument(),
            "master password".AsMemory(),
            TestKdf);
        byte[] previousFile = await File.ReadAllBytesAsync(vaultPath);
        opened.Session.Document.Name = "Changed";

        await Assert.ThrowsAsync<InjectedSaveException>(() => VaultFileService.SaveAsync(
            opened,
            stage =>
            {
                if (stage == failureStage)
                {
                    throw new InjectedSaveException();
                }
            }));

        Assert.Equal(previousFile, await File.ReadAllBytesAsync(vaultPath));
        Assert.Equal<ulong>(0, opened.Session.Document.Revision);
        Assert.Empty(Directory.EnumerateFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public async Task FailureReportedAfterAtomicReplaceIsRecoveredAsSuccess()
    {
        using TemporaryDirectory directory = new();
        string vaultPath = Path.Combine(directory.Path, "personal.fpass");
        using OpenedVault opened = await VaultFileService.CreateAsync(
            vaultPath,
            CreateDocument(),
            "master password".AsMemory(),
            TestKdf);
        byte[] previousFile = await File.ReadAllBytesAsync(vaultPath);
        opened.Session.Document.Name = "Changed";

        await VaultFileService.SaveAsync(
            opened,
            stage =>
            {
                if (stage == VaultSaveStage.AfterReplaceAndBackup)
                {
                    throw new InjectedSaveException();
                }
            });

        Assert.Equal<ulong>(1, opened.Session.Document.Revision);
        Assert.Equal(previousFile, await File.ReadAllBytesAsync(vaultPath + ".bak"));
        using OpenedVault reopenedReadOnly = await VaultFileService.OpenReadOnlyAsync(
            vaultPath,
            "master password".AsMemory());
        Assert.Equal("Changed", reopenedReadOnly.Session.Document.Name);
    }

    private static VaultDocument CreateDocument() =>
        VaultDocument.Create(
            "Personal",
            new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero));

    private static VaultEntry CreateEntry()
    {
        DateTimeOffset now = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);
        return new VaultEntry
        {
            Id = Guid.NewGuid(),
            Title = "Example",
            Username = "person@example.test",
            Password = "generated-secret",
            CreatedUtc = now,
            UpdatedUtc = now,
        };
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "finalpass-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }
    }

    private sealed class InjectedSaveException : Exception;
}
