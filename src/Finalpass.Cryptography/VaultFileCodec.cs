using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Finalpass.Core;

namespace Finalpass.Cryptography;

public static class VaultFileCodec
{
    public const ushort FileVersion = 1;
    public const ushort HeaderLength = 156;
    public const int MaximumPlaintextPayloadLength = 128 * 1024 * 1024;

    private const uint KdfAlgorithmArgon2Id13 = 1;
    private const int WrappedKeyOffset = 76;
    private const int WrappedKeyLength = Sodium.KeyBytes + Sodium.TagBytes;
    private const int PayloadNonceOffset = 124;
    private const int PayloadLengthOffset = 148;

    private static ReadOnlySpan<byte> Magic => "FPASSDB\0"u8;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        MaxDepth = 64,
        Converters = { new UtcDateTimeOffsetConverter() },
    };

    public static VaultSession Create(
        VaultDocument document,
        ReadOnlySpan<char> masterPassword,
        VaultKdfParameters? kdfParameters = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        VaultValidation.Validate(document);

        VaultKdfParameters parameters = kdfParameters ?? VaultKdfParameters.Default;
        parameters.Validate();

        byte[] dataKey = Sodium.RandomBytes(Sodium.KeyBytes);
        try
        {
            byte[] keyHeader = WrapDataKey(dataKey, masterPassword, parameters);
            return new VaultSession(document, dataKey, keyHeader, parameters);
        }
        catch
        {
            Sodium.Wipe(dataKey);
            throw;
        }
    }

    public static VaultSession Open(ReadOnlySpan<byte> file, ReadOnlySpan<char> masterPassword)
    {
        ParsedHeader parsed = ParseHeader(file);
        byte[] passwordBytes = EncodePassword(masterPassword);
        byte[] keyEncryptionKey = GC.AllocateUninitializedArray<byte>(Sodium.KeyBytes);
        byte[]? dataKey = null;
        byte[]? payload = null;

        try
        {
            Sodium.DeriveArgon2IdKey(
                passwordBytes,
                parsed.Salt,
                parsed.KdfParameters.OperationsLimit,
                parsed.KdfParameters.MemoryLimit,
                keyEncryptionKey);

            dataKey = Sodium.Decrypt(
                parsed.WrappedKey,
                parsed.WrappedKeyNonce,
                keyEncryptionKey,
                file[..WrappedKeyOffset]);

            payload = Sodium.Decrypt(
                parsed.PayloadCiphertext,
                parsed.PayloadNonce,
                dataKey,
                file[..HeaderLength]);

            VaultDocument document = DeserializeAndValidate(payload);
            byte[] keyHeader = file[..PayloadNonceOffset].ToArray();
            VaultSession session = new(document, dataKey, keyHeader, parsed.KdfParameters);
            dataKey = null;
            return session;
        }
        finally
        {
            Sodium.Wipe(passwordBytes);
            Sodium.Wipe(keyEncryptionKey);
            if (dataKey is not null)
            {
                Sodium.Wipe(dataKey);
            }

            if (payload is not null)
            {
                Sodium.Wipe(payload);
            }
        }
    }

    internal static byte[] Encode(VaultDocument document, ReadOnlySpan<byte> dataKey, ReadOnlySpan<byte> keyHeader)
    {
        VaultValidation.Validate(document);
        if (keyHeader.Length != PayloadNonceOffset)
        {
            throw new ArgumentException("The wrapped-key header has an invalid length.", nameof(keyHeader));
        }

        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
        if (payload.Length > MaximumPlaintextPayloadLength)
        {
            Sodium.Wipe(payload);
            throw new VaultFormatException("The vault payload is too large.");
        }

        try
        {
            byte[] header = GC.AllocateUninitializedArray<byte>(HeaderLength);
            keyHeader.CopyTo(header);
            Sodium.FillRandom(header.AsSpan(PayloadNonceOffset, Sodium.NonceBytes));
            BinaryPrimitives.WriteUInt64LittleEndian(
                header.AsSpan(PayloadLengthOffset, sizeof(ulong)),
                checked((ulong)payload.Length + Sodium.TagBytes));

            byte[] ciphertext = Sodium.Encrypt(
                payload,
                header.AsSpan(PayloadNonceOffset, Sodium.NonceBytes),
                dataKey,
                header);

            byte[] file = GC.AllocateUninitializedArray<byte>(checked(HeaderLength + ciphertext.Length));
            header.CopyTo(file, 0);
            ciphertext.CopyTo(file, HeaderLength);
            return file;
        }
        finally
        {
            Sodium.Wipe(payload);
        }
    }

    internal static byte[] RewrapDataKey(
        ReadOnlySpan<byte> dataKey,
        ReadOnlySpan<char> newMasterPassword,
        VaultKdfParameters parameters) =>
        WrapDataKey(dataKey, newMasterPassword, parameters);

    internal static void ValidateEncodedSnapshot(
        ReadOnlySpan<byte> file,
        ReadOnlySpan<byte> dataKey,
        ReadOnlySpan<byte> expectedKeyHeader)
    {
        ParsedHeader parsed = ParseHeader(file);
        if (expectedKeyHeader.Length != PayloadNonceOffset ||
            !file[..PayloadNonceOffset].SequenceEqual(expectedKeyHeader))
        {
            throw new VaultAuthenticationException();
        }

        byte[] payload = Sodium.Decrypt(
            parsed.PayloadCiphertext,
            parsed.PayloadNonce,
            dataKey,
            file[..HeaderLength]);
        try
        {
            _ = DeserializeAndValidate(payload);
        }
        finally
        {
            Sodium.Wipe(payload);
        }
    }

    private static byte[] WrapDataKey(
        ReadOnlySpan<byte> dataKey,
        ReadOnlySpan<char> masterPassword,
        VaultKdfParameters parameters)
    {
        if (dataKey.Length != Sodium.KeyBytes)
        {
            throw new ArgumentException($"The data key must be {Sodium.KeyBytes} bytes.", nameof(dataKey));
        }

        byte[] header = new byte[PayloadNonceOffset];
        Magic.CopyTo(header);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(8, 2), FileVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(10, 2), HeaderLength);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12, 4), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(16, 4), KdfAlgorithmArgon2Id13);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(20, 8), parameters.OperationsLimit);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(28, 8), parameters.MemoryLimit);
        Sodium.FillRandom(header.AsSpan(36, Sodium.SaltBytes));
        Sodium.FillRandom(header.AsSpan(52, Sodium.NonceBytes));

        byte[] passwordBytes = EncodePassword(masterPassword);
        byte[] keyEncryptionKey = GC.AllocateUninitializedArray<byte>(Sodium.KeyBytes);
        try
        {
            Sodium.DeriveArgon2IdKey(
                passwordBytes,
                header.AsSpan(36, Sodium.SaltBytes),
                parameters.OperationsLimit,
                parameters.MemoryLimit,
                keyEncryptionKey);

            byte[] wrappedKey = Sodium.Encrypt(
                dataKey,
                header.AsSpan(52, Sodium.NonceBytes),
                keyEncryptionKey,
                header.AsSpan(0, WrappedKeyOffset));
            wrappedKey.CopyTo(header, WrappedKeyOffset);
            return header;
        }
        finally
        {
            Sodium.Wipe(passwordBytes);
            Sodium.Wipe(keyEncryptionKey);
        }
    }

    private static ParsedHeader ParseHeader(ReadOnlySpan<byte> file)
    {
        if (file.Length < HeaderLength || !file[..Magic.Length].SequenceEqual(Magic))
        {
            throw new VaultFormatException("This is not a Finalpass vault.");
        }

        if (BinaryPrimitives.ReadUInt16LittleEndian(file.Slice(8, 2)) != FileVersion)
        {
            throw new VaultFormatException("The vault file version is not supported.");
        }

        if (BinaryPrimitives.ReadUInt16LittleEndian(file.Slice(10, 2)) != HeaderLength ||
            BinaryPrimitives.ReadUInt32LittleEndian(file.Slice(12, 4)) != 0 ||
            BinaryPrimitives.ReadUInt32LittleEndian(file.Slice(16, 4)) != KdfAlgorithmArgon2Id13)
        {
            throw new VaultFormatException("The vault header is invalid or unsupported.");
        }

        VaultKdfParameters parameters = new(
            BinaryPrimitives.ReadUInt64LittleEndian(file.Slice(20, 8)),
            BinaryPrimitives.ReadUInt64LittleEndian(file.Slice(28, 8)));
        parameters.Validate();

        ulong ciphertextLength = BinaryPrimitives.ReadUInt64LittleEndian(
            file.Slice(PayloadLengthOffset, sizeof(ulong)));
        if (ciphertextLength < Sodium.TagBytes ||
            ciphertextLength > (ulong)MaximumPlaintextPayloadLength + Sodium.TagBytes ||
            ciphertextLength > int.MaxValue)
        {
            throw new VaultFormatException("The vault payload length is invalid.");
        }

        int expectedFileLength;
        try
        {
            expectedFileLength = checked(HeaderLength + (int)ciphertextLength);
        }
        catch (OverflowException exception)
        {
            throw new VaultFormatException("The vault payload length overflows.", exception);
        }

        if (file.Length != expectedFileLength)
        {
            throw new VaultFormatException("The vault is truncated or contains trailing data.");
        }

        return new ParsedHeader(
            parameters,
            file.Slice(36, Sodium.SaltBytes),
            file.Slice(52, Sodium.NonceBytes),
            file.Slice(WrappedKeyOffset, WrappedKeyLength),
            file.Slice(PayloadNonceOffset, Sodium.NonceBytes),
            file[HeaderLength..]);
    }

    private static byte[] EncodePassword(ReadOnlySpan<char> password)
    {
        int byteCount = Encoding.UTF8.GetByteCount(password);
        if (byteCount is < 1 or > 1024)
        {
            throw new ArgumentException("The master password must encode to between 1 and 1024 bytes.", nameof(password));
        }

        byte[] encoded = GC.AllocateUninitializedArray<byte>(byteCount);
        Encoding.UTF8.GetBytes(password, encoded);
        return encoded;
    }

    private static VaultDocument DeserializeAndValidate(ReadOnlySpan<byte> payload)
    {
        try
        {
            VaultDocument? document = JsonSerializer.Deserialize<VaultDocument>(payload, JsonOptions);
            if (document is null)
            {
                throw new VaultFormatException("The vault payload is empty.");
            }

            VaultValidation.Validate(document);
            return document;
        }
        catch (JsonException exception)
        {
            throw new VaultFormatException("The vault payload is invalid.", exception);
        }
        catch (VaultValidationException exception)
        {
            throw new VaultFormatException("The vault payload failed validation.", exception);
        }
    }

    private readonly ref struct ParsedHeader(
        VaultKdfParameters kdfParameters,
        ReadOnlySpan<byte> salt,
        ReadOnlySpan<byte> wrappedKeyNonce,
        ReadOnlySpan<byte> wrappedKey,
        ReadOnlySpan<byte> payloadNonce,
        ReadOnlySpan<byte> payloadCiphertext)
    {
        public VaultKdfParameters KdfParameters { get; } = kdfParameters;

        public ReadOnlySpan<byte> Salt { get; } = salt;

        public ReadOnlySpan<byte> WrappedKeyNonce { get; } = wrappedKeyNonce;

        public ReadOnlySpan<byte> WrappedKey { get; } = wrappedKey;

        public ReadOnlySpan<byte> PayloadNonce { get; } = payloadNonce;

        public ReadOnlySpan<byte> PayloadCiphertext { get; } = payloadCiphertext;
    }
}
