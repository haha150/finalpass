using System.Runtime.InteropServices;

namespace Finalpass.Cryptography;

public static partial class Sodium
{
    public const int KeyBytes = 32;
    public const int NonceBytes = 24;
    public const int TagBytes = 16;
    public const int SaltBytes = 16;

    private const string LibraryName = "libsodium";

    static Sodium()
    {
        if (Native.sodium_init() < 0)
        {
            throw new SodiumException("libsodium initialization failed.");
        }

        if (Native.crypto_aead_xchacha20poly1305_ietf_keybytes() != KeyBytes ||
            Native.crypto_aead_xchacha20poly1305_ietf_npubbytes() != NonceBytes ||
            Native.crypto_aead_xchacha20poly1305_ietf_abytes() != TagBytes ||
            Native.crypto_pwhash_saltbytes() != SaltBytes)
        {
            throw new SodiumException("The loaded libsodium ABI is incompatible.");
        }
    }

    public static unsafe void FillRandom(Span<byte> destination)
    {
        fixed (byte* output = destination)
        {
            Native.randombytes_buf(output, (nuint)destination.Length);
        }
    }

    public static byte[] RandomBytes(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        byte[] result = GC.AllocateUninitializedArray<byte>(length);
        FillRandom(result);
        return result;
    }

    public static unsafe void DeriveArgon2IdKey(
        ReadOnlySpan<byte> password,
        ReadOnlySpan<byte> salt,
        ulong operationsLimit,
        ulong memoryLimit,
        Span<byte> destination)
    {
        if (password.IsEmpty)
        {
            throw new ArgumentException("A password is required.", nameof(password));
        }

        if (salt.Length != SaltBytes)
        {
            throw new ArgumentException($"The salt must be {SaltBytes} bytes.", nameof(salt));
        }

        if (destination.Length != KeyBytes)
        {
            throw new ArgumentException($"The derived key must be {KeyBytes} bytes.", nameof(destination));
        }

        ArgumentOutOfRangeException.ThrowIfGreaterThan(memoryLimit, (ulong)nuint.MaxValue);

        fixed (byte* output = destination)
        fixed (byte* passwordPointer = password)
        fixed (byte* saltPointer = salt)
        {
            int result = Native.crypto_pwhash(
                output,
                (ulong)destination.Length,
                passwordPointer,
                (ulong)password.Length,
                saltPointer,
                operationsLimit,
                (nuint)memoryLimit,
                Native.crypto_pwhash_alg_argon2id13());

            if (result != 0)
            {
                throw new SodiumException("Argon2id key derivation failed.");
            }
        }
    }

    public static unsafe byte[] Encrypt(
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> associatedData)
    {
        ValidateAeadInputs(nonce, key);
        byte[] ciphertext = GC.AllocateUninitializedArray<byte>(checked(plaintext.Length + TagBytes));

        fixed (byte* ciphertextPointer = ciphertext)
        fixed (byte* plaintextPointer = plaintext)
        fixed (byte* associatedDataPointer = associatedData)
        fixed (byte* noncePointer = nonce)
        fixed (byte* keyPointer = key)
        {
            ulong ciphertextLength = 0;
            int result = Native.crypto_aead_xchacha20poly1305_ietf_encrypt(
                ciphertextPointer,
                &ciphertextLength,
                plaintextPointer,
                (ulong)plaintext.Length,
                associatedDataPointer,
                (ulong)associatedData.Length,
                null,
                noncePointer,
                keyPointer);

            if (result != 0 || ciphertextLength != (ulong)ciphertext.Length)
            {
                Wipe(ciphertext);
                throw new SodiumException("XChaCha20-Poly1305 encryption failed.");
            }
        }

        return ciphertext;
    }

    public static unsafe byte[] Decrypt(
        ReadOnlySpan<byte> ciphertext,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> associatedData)
    {
        ValidateAeadInputs(nonce, key);
        if (ciphertext.Length < TagBytes)
        {
            throw new VaultAuthenticationException();
        }

        byte[] plaintext = GC.AllocateUninitializedArray<byte>(ciphertext.Length - TagBytes);
        fixed (byte* plaintextPointer = plaintext)
        fixed (byte* ciphertextPointer = ciphertext)
        fixed (byte* associatedDataPointer = associatedData)
        fixed (byte* noncePointer = nonce)
        fixed (byte* keyPointer = key)
        {
            ulong plaintextLength = 0;
            int result = Native.crypto_aead_xchacha20poly1305_ietf_decrypt(
                plaintextPointer,
                &plaintextLength,
                null,
                ciphertextPointer,
                (ulong)ciphertext.Length,
                associatedDataPointer,
                (ulong)associatedData.Length,
                noncePointer,
                keyPointer);

            if (result != 0 || plaintextLength != (ulong)plaintext.Length)
            {
                Wipe(plaintext);
                throw new VaultAuthenticationException();
            }
        }

        return plaintext;
    }

    public static unsafe void Wipe(Span<byte> buffer)
    {
        fixed (byte* pointer = buffer)
        {
            Native.sodium_memzero(pointer, (nuint)buffer.Length);
        }
    }

    private static void ValidateAeadInputs(ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> key)
    {
        if (nonce.Length != NonceBytes)
        {
            throw new ArgumentException($"The nonce must be {NonceBytes} bytes.", nameof(nonce));
        }

        if (key.Length != KeyBytes)
        {
            throw new ArgumentException($"The key must be {KeyBytes} bytes.", nameof(key));
        }
    }

    private static unsafe partial class Native
    {
        [LibraryImport(LibraryName)]
        internal static partial int sodium_init();

        [LibraryImport(LibraryName)]
        internal static partial void randombytes_buf(byte* buffer, nuint size);

        [LibraryImport(LibraryName)]
        internal static partial nuint crypto_aead_xchacha20poly1305_ietf_keybytes();

        [LibraryImport(LibraryName)]
        internal static partial nuint crypto_aead_xchacha20poly1305_ietf_npubbytes();

        [LibraryImport(LibraryName)]
        internal static partial nuint crypto_aead_xchacha20poly1305_ietf_abytes();

        [LibraryImport(LibraryName)]
        internal static partial nuint crypto_pwhash_saltbytes();

        [LibraryImport(LibraryName)]
        internal static partial int crypto_pwhash_alg_argon2id13();

        [LibraryImport(LibraryName)]
        internal static partial int crypto_pwhash(
            byte* output,
            ulong outputLength,
            byte* password,
            ulong passwordLength,
            byte* salt,
            ulong operationsLimit,
            nuint memoryLimit,
            int algorithm);

        [LibraryImport(LibraryName)]
        internal static partial int crypto_aead_xchacha20poly1305_ietf_encrypt(
            byte* ciphertext,
            ulong* ciphertextLength,
            byte* message,
            ulong messageLength,
            byte* associatedData,
            ulong associatedDataLength,
            byte* secretNonce,
            byte* publicNonce,
            byte* key);

        [LibraryImport(LibraryName)]
        internal static partial int crypto_aead_xchacha20poly1305_ietf_decrypt(
            byte* message,
            ulong* messageLength,
            byte* secretNonce,
            byte* ciphertext,
            ulong ciphertextLength,
            byte* associatedData,
            ulong associatedDataLength,
            byte* publicNonce,
            byte* key);

        [LibraryImport(LibraryName)]
        internal static partial void sodium_memzero(byte* buffer, nuint length);
    }
}

public sealed class SodiumException(string message) : Exception(message);

public sealed class VaultAuthenticationException()
    : Exception("The master password is incorrect or the vault is damaged.");
