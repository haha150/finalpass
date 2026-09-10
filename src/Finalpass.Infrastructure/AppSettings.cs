using System.Text.Json;

namespace Finalpass.Infrastructure;

public sealed class AppSettings
{
    public int IdleLockMinutes { get; set; } = 5;

    public int ClipboardClearSeconds { get; set; } = 30;

    public string? LastVaultPath { get; set; }

    public int PasswordLength { get; set; } = 24;

    public bool PasswordUppercase { get; set; } = true;

    public bool PasswordLowercase { get; set; } = true;

    public bool PasswordDigits { get; set; } = true;

    public bool PasswordSymbols { get; set; } = true;

    public bool PasswordExcludeAmbiguous { get; set; } = true;

    public int PassphraseWordCount { get; set; } = 8;

    public string PassphraseSeparator { get; set; } = "-";

    public bool PassphraseCapitalizeWords { get; set; }

    public bool PassphraseAppendNumber { get; set; }

    internal AppSettings Normalize()
    {
        IdleLockMinutes = Math.Clamp(IdleLockMinutes, 1, 120);
        ClipboardClearSeconds = Math.Clamp(ClipboardClearSeconds, 5, 300);
        if (string.IsNullOrWhiteSpace(LastVaultPath) || LastVaultPath.Length > 32_767)
        {
            LastVaultPath = null;
        }
        else
        {
            try
            {
                LastVaultPath = Path.GetFullPath(LastVaultPath);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
            {
                LastVaultPath = null;
            }
        }

        PasswordLength = Math.Clamp(PasswordLength, 12, 128);
        PassphraseWordCount = Math.Clamp(PassphraseWordCount, 6, 16);
        if (string.IsNullOrEmpty(PassphraseSeparator) ||
            PassphraseSeparator.Length > 3 ||
            PassphraseSeparator.Any(char.IsLetterOrDigit))
        {
            PassphraseSeparator = "-";
        }

        if (!PasswordUppercase && !PasswordLowercase && !PasswordDigits && !PasswordSymbols)
        {
            PasswordLowercase = true;
        }

        return this;
    }
}

public static class AppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Finalpass",
        "settings.json");

    public static async Task<AppSettings> LoadAsync(
        string? path = null,
        CancellationToken cancellationToken = default)
    {
        string settingsPath = Path.GetFullPath(path ?? DefaultPath);
        if (!File.Exists(settingsPath))
        {
            return new AppSettings();
        }

        try
        {
            await using FileStream stream = new(
                settingsPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                16 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length > 64 * 1024)
            {
                return new AppSettings();
            }

            AppSettings? settings = await JsonSerializer.DeserializeAsync<AppSettings>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
            return (settings ?? new AppSettings()).Normalize();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
        catch (IOException)
        {
            return new AppSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public static async Task SaveAsync(
        AppSettings settings,
        string? path = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Normalize();
        string settingsPath = Path.GetFullPath(path ?? DefaultPath);
        string directory = Path.GetDirectoryName(settingsPath)
            ?? throw new DirectoryNotFoundException("The settings directory is invalid.");
        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(settingsPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (FileStream stream = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                16 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    settings,
                    JsonOptions,
                    cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(true);
            }

            if (File.Exists(settingsPath))
            {
                File.Replace(temporaryPath, settingsPath, null, true);
            }
            else
            {
                File.Move(temporaryPath, settingsPath, false);
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
}
