using Windows.ApplicationModel.DataTransfer;

namespace Finalpass.App;

internal static class SecureClipboard
{
    private static readonly object Sync = new();
    private static CancellationTokenSource? _clearCancellation;
    private static string? _lastCopiedValue;

    public static void Copy(string value, TimeSpan clearAfter)
    {
        if (clearAfter < TimeSpan.FromSeconds(5) || clearAfter > TimeSpan.FromMinutes(5))
        {
            throw new ArgumentOutOfRangeException(nameof(clearAfter));
        }

        DataPackage package = new();
        package.SetText(value);
        ClipboardContentOptions options = new()
        {
            IsAllowedInHistory = false,
            IsRoamable = false,
        };

        if (!Clipboard.SetContentWithOptions(package, options))
        {
            throw new InvalidOperationException("Windows rejected the clipboard content.");
        }

        CancellationTokenSource cancellation = new();
        lock (Sync)
        {
            _clearCancellation?.Cancel();
            _clearCancellation?.Dispose();
            _clearCancellation = cancellation;
            _lastCopiedValue = value;
        }

        _ = ClearAfterDelayAsync(value, clearAfter, cancellation);
    }

    public static async Task ClearIfOwnedAsync()
    {
        CancellationTokenSource? cancellation;
        string? copiedValue;
        lock (Sync)
        {
            cancellation = _clearCancellation;
            copiedValue = _lastCopiedValue;
            _clearCancellation = null;
            _lastCopiedValue = null;
        }

        cancellation?.Cancel();
        cancellation?.Dispose();
        if (copiedValue is not null)
        {
            await ClearIfUnchangedAsync(copiedValue);
        }
    }

    private static async Task ClearAfterDelayAsync(
        string copiedValue,
        TimeSpan clearAfter,
        CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(clearAfter, cancellation.Token);
            await ClearIfUnchangedAsync(copiedValue);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // The clipboard may be temporarily unavailable.
        }
        finally
        {
            lock (Sync)
            {
                if (ReferenceEquals(_clearCancellation, cancellation))
                {
                    _clearCancellation = null;
                    _lastCopiedValue = null;
                    cancellation.Dispose();
                }
            }
        }
    }

    private static async Task ClearIfUnchangedAsync(string copiedValue)
    {
        DataPackageView content = Clipboard.GetContent();
        if (content.Contains(StandardDataFormats.Text) &&
            string.Equals(await content.GetTextAsync(), copiedValue, StringComparison.Ordinal))
        {
            Clipboard.Clear();
        }
    }
}
