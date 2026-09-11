namespace Finalpass.Infrastructure.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public async Task SettingsRoundTripContainsPreferencesOnly()
    {
        using TemporaryDirectory directory = new();
        string path = Path.Combine(directory.Path, "settings.json");
        AppSettings expected = new()
        {
            Theme = "Dark",
            IdleLockMinutes = 12,
            ClipboardClearSeconds = 45,
            LastVaultPath = Path.Combine(directory.Path, "Personal.fpass"),
            PasswordLength = 36,
            PassphraseWordCount = 10,
            PassphraseSeparator = ".",
        };

        await AppSettingsService.SaveAsync(expected, path);
        AppSettings actual = await AppSettingsService.LoadAsync(path);

        Assert.Equal("Dark", actual.Theme);
        Assert.Equal(12, actual.IdleLockMinutes);
        Assert.Equal(45, actual.ClipboardClearSeconds);
        Assert.Equal(Path.Combine(directory.Path, "Personal.fpass"), actual.LastVaultPath);
        Assert.Equal(36, actual.PasswordLength);
        Assert.Equal(10, actual.PassphraseWordCount);
        Assert.Equal(".", actual.PassphraseSeparator);
        string serialized = await File.ReadAllTextAsync(path);
        Assert.DoesNotContain("passwordValue", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("masterPassword", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MalformedOrOutOfRangeSettingsFailSafe()
    {
        using TemporaryDirectory directory = new();
        string malformedPath = Path.Combine(directory.Path, "malformed.json");
        await File.WriteAllTextAsync(malformedPath, "{ definitely not json");
        AppSettings malformed = await AppSettingsService.LoadAsync(malformedPath);
        Assert.Equal(5, malformed.IdleLockMinutes);

        string boundedPath = Path.Combine(directory.Path, "bounded.json");
        await File.WriteAllTextAsync(
            boundedPath,
            "{\"theme\":\"sepia\",\"idleLockMinutes\":-1,\"clipboardClearSeconds\":9999," +
            "\"passwordLength\":2,\"passphraseWordCount\":99,\"passphraseSeparator\":\"word\"}");
        AppSettings bounded = await AppSettingsService.LoadAsync(boundedPath);
        Assert.Equal("System", bounded.Theme);
        Assert.Equal(1, bounded.IdleLockMinutes);
        Assert.Equal(300, bounded.ClipboardClearSeconds);
        Assert.Equal(12, bounded.PasswordLength);
        Assert.Equal(16, bounded.PassphraseWordCount);
        Assert.Equal("-", bounded.PassphraseSeparator);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "finalpass-settings-tests",
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
}
