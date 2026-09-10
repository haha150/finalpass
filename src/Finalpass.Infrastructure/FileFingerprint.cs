using System.Security.Cryptography;

namespace Finalpass.Infrastructure;

public sealed record FileFingerprint(long Length, string Sha256)
{
    internal static async Task<FileFingerprint> ComputeAsync(
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
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return new FileFingerprint(stream.Length, Convert.ToHexString(hash));
    }
}
