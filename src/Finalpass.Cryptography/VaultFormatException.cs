namespace Finalpass.Cryptography;

public sealed class VaultFormatException(string message, Exception? innerException = null)
    : Exception(message, innerException);
