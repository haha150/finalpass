using Finalpass.Core;

namespace Finalpass.Cryptography;

public sealed class VaultSession : IDisposable
{
    private byte[]? _dataKey;
    private byte[] _keyHeader;

    internal VaultSession(
        VaultDocument document,
        byte[] dataKey,
        byte[] keyHeader,
        VaultKdfParameters kdfParameters)
    {
        Document = document;
        _dataKey = dataKey;
        _keyHeader = keyHeader;
        KdfParameters = kdfParameters;
    }

    public VaultDocument Document { get; }

    public VaultKdfParameters KdfParameters { get; private set; }

    public bool IsDisposed => _dataKey is null;

    public byte[] Encode()
    {
        byte[] dataKey = GetDataKey();
        return VaultFileCodec.Encode(Document, dataKey, _keyHeader);
    }

    public void ValidateEncodedSnapshot(ReadOnlySpan<byte> file)
    {
        VaultFileCodec.ValidateEncodedSnapshot(file, GetDataKey(), _keyHeader);
    }

    public void ChangeMasterPassword(
        ReadOnlySpan<char> newMasterPassword,
        VaultKdfParameters? kdfParameters = null)
    {
        VaultKdfParameters parameters = kdfParameters ?? KdfParameters;
        parameters.Validate();
        byte[] replacement = VaultFileCodec.RewrapDataKey(
            GetDataKey(),
            newMasterPassword,
            parameters);

        Sodium.Wipe(_keyHeader);
        _keyHeader = replacement;
        KdfParameters = parameters;
    }

    public void Dispose()
    {
        byte[]? dataKey = Interlocked.Exchange(ref _dataKey, null);
        if (dataKey is not null)
        {
            Sodium.Wipe(dataKey);
        }

        Sodium.Wipe(_keyHeader);
        GC.SuppressFinalize(this);
    }

    private byte[] GetDataKey() =>
        _dataKey ?? throw new ObjectDisposedException(nameof(VaultSession));
}
