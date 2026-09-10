using System.Buffers.Binary;

namespace Finalpass.Cryptography;

public sealed record PasswordGeneratorOptions(
    int Length = 24,
    bool IncludeUppercase = true,
    bool IncludeLowercase = true,
    bool IncludeDigits = true,
    bool IncludeSymbols = true,
    bool ExcludeAmbiguous = true);

public sealed record PassphraseGeneratorOptions(
    int WordCount = 8,
    string Separator = "-",
    bool CapitalizeWords = false,
    bool AppendNumber = false);

public static class PasswordGenerator
{
    private const string Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Lowercase = "abcdefghijklmnopqrstuvwxyz";
    private const string Digits = "0123456789";
    private const string Symbols = "!@#$%^&*()-_=+[]{}";
    private const string Ambiguous = "Il1O0o";

    // This fixed local list keeps generation offline. The default eight-word
    // phrase provides more than 64 bits of unbiased selection entropy.
    private static readonly string[] PassphraseWords = (
        "acorn amber anchor apple apron arrow atlas autumn badge bamboo banner beach " +
        "beacon berry birch biscuit blade blossom blue bonus bottle breeze brick bridge " +
        "brook brush bubble button cabin cactus candle canyon carpet cedar cherry circle " +
        "cliff cloud clover coast cobalt comet coral cotton creek crown crystal daisy dawn " +
        "delta desert diamond dolphin dragon dream drift drum dusk eagle earth echo ember " +
        "falcon feather fern field flame flash flint flower forest fox frost galaxy garden " +
        "gate gem glacier globe gold grape grove harbor hawk hazel hill honey horizon horse " +
        "island ivory jade jasmine jewel journey juniper kettle kite lake lantern leaf lemon " +
        "lilac lily lion lotus maple marble meadow melon meteor mint mist moon moss mountain " +
        "nectar night north oak ocean olive opal orchid otter owl palm panda paper peach pearl " +
        "pepper pine planet plum pond poppy prairie prism quartz rabbit rain raven reef river " +
        "robin rock rose ruby sail sand satin scarlet shadow shell shore silver sky slate snow " +
        "solar sparrow spice spring star stone storm summit sun sunset swift tiger timber trail " +
        "tree tulip valley velvet violet wave wheat willow wind winter wolf wood yarn zephyr " +
        "artist bakery basket bell bicycle blanket book border bronze brooklet castle cello " +
        "chestnut citrus compass copper cottage crane dahlia dance domino elm emerald engine " +
        "fable fabric ferry firefly fountain garnet ginger granite guitar hammock harvest haven " +
        "heron hibiscus iceberg indigo iris lagoon lavender lighthouse linen mango marina melody " +
        "mercury mirror morning orchidway pebble pencil phoenix piano picnic rainbow ripple rocket " +
        "saffron sapphire season signal skylark spruce starlight thistle thunder topaz turtle " +
        "walnut waterfall whisper wildflower window woodland sunrise seaglass moonbeam raincoat " +
        "snowdrop songbird starfish teacup windmill foxglove seashell nightfall daybreak")
        .Split(' ', StringSplitOptions.RemoveEmptyEntries);

    public static string Generate(int length = 24) =>
        Generate(new PasswordGeneratorOptions(Length: length));

    public static string Generate(PasswordGeneratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Length, 12);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.Length, 128);

        List<string> classes = [];
        AddClass(classes, Uppercase, options.IncludeUppercase, options.ExcludeAmbiguous);
        AddClass(classes, Lowercase, options.IncludeLowercase, options.ExcludeAmbiguous);
        AddClass(classes, Digits, options.IncludeDigits, options.ExcludeAmbiguous);
        AddClass(classes, Symbols, options.IncludeSymbols, excludeAmbiguous: false);
        if (classes.Count == 0)
        {
            throw new ArgumentException("At least one character class must be enabled.", nameof(options));
        }

        string alphabet = string.Concat(classes);
        Span<char> characters = stackalloc char[options.Length];
        int index = 0;
        foreach (string characterClass in classes)
        {
            characters[index++] = Pick(characterClass);
        }

        while (index < characters.Length)
        {
            characters[index++] = Pick(alphabet);
        }

        Shuffle(characters);
        return new string(characters);
    }

    public static string GeneratePassphrase(PassphraseGeneratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.WordCount, 6);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.WordCount, 16);
        if (options.Separator.Length is < 1 or > 3 || options.Separator.Any(char.IsLetterOrDigit))
        {
            throw new ArgumentException(
                "The passphrase separator must contain one to three punctuation characters.",
                nameof(options));
        }

        string[] words = new string[options.WordCount];
        for (int index = 0; index < words.Length; index++)
        {
            string word = PassphraseWords[NextIndex(PassphraseWords.Length)];
            words[index] = options.CapitalizeWords
                ? char.ToUpperInvariant(word[0]) + word[1..]
                : word;
        }

        string passphrase = string.Join(options.Separator, words);
        if (options.AppendNumber)
        {
            passphrase += options.Separator +
                NextIndex(10_000).ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
        }

        return passphrase;
    }

    public static double EstimatePassphraseEntropyBits(int wordCount, bool appendNumber = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(wordCount, 1);
        return (wordCount * Math.Log2(PassphraseWords.Length)) +
            (appendNumber ? Math.Log2(10_000) : 0);
    }

    private static void AddClass(
        List<string> classes,
        string characters,
        bool include,
        bool excludeAmbiguous)
    {
        if (!include)
        {
            return;
        }

        classes.Add(excludeAmbiguous
            ? new string(characters.Where(character =>
                !Ambiguous.Contains(character, StringComparison.Ordinal)).ToArray())
            : characters);
    }

    private static char Pick(string alphabet) => alphabet[NextIndex(alphabet.Length)];

    private static void Shuffle(Span<char> characters)
    {
        for (int index = characters.Length - 1; index > 0; index--)
        {
            int swapIndex = NextIndex(index + 1);
            (characters[index], characters[swapIndex]) = (characters[swapIndex], characters[index]);
        }
    }

    private static int NextIndex(int exclusiveUpperBound)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(exclusiveUpperBound, 1);
        uint bound = checked((uint)exclusiveUpperBound);
        uint rejectionLimit = uint.MaxValue - (uint.MaxValue % bound);
        Span<byte> random = stackalloc byte[sizeof(uint)];
        uint value;
        do
        {
            Sodium.FillRandom(random);
            value = BinaryPrimitives.ReadUInt32LittleEndian(random);
        }
        while (value >= rejectionLimit);

        return checked((int)(value % bound));
    }
}
