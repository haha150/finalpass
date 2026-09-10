using Finalpass.Cryptography;

namespace Finalpass.Infrastructure;

public sealed class OpenedVault : IDisposable
{
    private FileStream? _lockHandle;

    internal OpenedVault(
        string path,
        VaultSession session,
        FileFingerprint fingerprint,
        FileStream? lockHandle,
        bool isReadOnly = false)
    {
        Path = path;
        Session = session;
        Fingerprint = fingerprint;
        _lockHandle = lockHandle;
        IsReadOnly = isReadOnly;
    }

    public string Path { get; }

    public VaultSession Session { get; }

    public bool IsReadOnly { get; }

    public FileFingerprint Fingerprint { get; internal set; }

    public void Dispose()
    {
        Session.Dispose();
        Interlocked.Exchange(ref _lockHandle, null)?.Dispose();
        GC.SuppressFinalize(this);
    }
}
