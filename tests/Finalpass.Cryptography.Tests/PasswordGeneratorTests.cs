using Finalpass.Cryptography;

namespace Finalpass.Cryptography.Tests;

public sealed class PasswordGeneratorTests
{
    [Fact]
    public void GeneratedPasswordsUseEveryEnabledCharacterClass()
    {
        for (int iteration = 0; iteration < 100; iteration++)
        {
            string password = PasswordGenerator.Generate(24);

            Assert.Equal(24, password.Length);
            Assert.Contains(password, char.IsUpper);
            Assert.Contains(password, char.IsLower);
            Assert.Contains(password, char.IsDigit);
            Assert.Contains(password, character => !char.IsLetterOrDigit(character));
        }
    }

    [Theory]
    [InlineData(11)]
    [InlineData(129)]
    public void UnsupportedLengthsAreRejected(int length)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PasswordGenerator.Generate(length));
    }

    [Fact]
    public void ConfigurablePasswordsHonorEnabledClassesAndAmbiguousExclusion()
    {
        PasswordGeneratorOptions options = new(
            Length: 40,
            IncludeUppercase: false,
            IncludeLowercase: true,
            IncludeDigits: false,
            IncludeSymbols: false,
            ExcludeAmbiguous: true);

        for (int iteration = 0; iteration < 50; iteration++)
        {
            string password = PasswordGenerator.Generate(options);
            Assert.Equal(40, password.Length);
            Assert.All(password, character => Assert.True(char.IsLower(character)));
            Assert.DoesNotContain(password, character => "Il1O0o".Contains(character));
        }
    }

    [Fact]
    public void AtLeastOneCharacterClassIsRequired()
    {
        PasswordGeneratorOptions options = new(
            IncludeUppercase: false,
            IncludeLowercase: false,
            IncludeDigits: false,
            IncludeSymbols: false);

        Assert.Throws<ArgumentException>(() => PasswordGenerator.Generate(options));
    }

    [Fact]
    public void PassphrasesHaveTheRequestedShapeAndStrongDefaultEntropy()
    {
        PassphraseGeneratorOptions options = new(
            WordCount: 8,
            Separator: ".",
            CapitalizeWords: true,
            AppendNumber: true);

        string passphrase = PasswordGenerator.GeneratePassphrase(options);
        string[] parts = passphrase.Split('.');

        Assert.Equal(9, parts.Length);
        Assert.All(parts[..8], word => Assert.True(char.IsUpper(word[0])));
        Assert.Matches("^[0-9]{4}$", parts[^1]);
        Assert.True(PasswordGenerator.EstimatePassphraseEntropyBits(8) > 64);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(17)]
    public void UnsupportedPassphraseWordCountsAreRejected(int wordCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PasswordGenerator.GeneratePassphrase(new PassphraseGeneratorOptions(wordCount)));
    }
}
