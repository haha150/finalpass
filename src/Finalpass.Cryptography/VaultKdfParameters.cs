namespace Finalpass.Cryptography;

public readonly record struct VaultKdfParameters(ulong OperationsLimit, ulong MemoryLimit)
{
    public const ulong MinimumOperationsLimit = 1;
    public const ulong MaximumOperationsLimit = 10;
    public const ulong MinimumMemoryLimit = 64UL * 1024 * 1024;
    public const ulong MaximumMemoryLimit = 1024UL * 1024 * 1024;

    public static VaultKdfParameters Default { get; } = new(3, 256UL * 1024 * 1024);

    public void Validate()
    {
        if (OperationsLimit is < MinimumOperationsLimit or > MaximumOperationsLimit)
        {
            throw new VaultFormatException("The Argon2 operations limit is outside policy.");
        }

        if (MemoryLimit is < MinimumMemoryLimit or > MaximumMemoryLimit)
        {
            throw new VaultFormatException("The Argon2 memory limit is outside policy.");
        }
    }
}
