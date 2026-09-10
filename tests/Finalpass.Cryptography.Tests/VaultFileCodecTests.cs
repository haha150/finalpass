using System.Buffers.Binary;
using Finalpass.Core;

namespace Finalpass.Cryptography.Tests;

public sealed class VaultFileCodecTests
{
    private static readonly VaultKdfParameters TestKdf = new(1, 64UL * 1024 * 1024);
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void VaultRoundTripPreservesAllFields()
    {
        VaultDocument original = CreateDocument();
        using VaultSession created = VaultFileCodec.Create(original, "correct horse".AsSpan(), TestKdf);
        byte[] file = created.Encode();

        using VaultSession opened = VaultFileCodec.Open(file, "correct horse".AsSpan());

        Assert.Equal(original.VaultId, opened.Document.VaultId);
        Assert.Equal("Admin", opened.Document.Entries.Single().Title);
        Assert.Equal("generated-secret", opened.Document.Entries.Single().Password);
        Assert.Equal(TestKdf, opened.KdfParameters);
    }

    [Fact]
    public void WrongPasswordIsRejected()
    {
        using VaultSession created = VaultFileCodec.Create(CreateDocument(), "correct".AsSpan(), TestKdf);
        byte[] file = created.Encode();

        Assert.Throws<VaultAuthenticationException>(
            () => VaultFileCodec.Open(file, "incorrect".AsSpan()));
    }

    [Fact]
    public void EveryAuthenticatedRegionRejectsModification()
    {
        using VaultSession created = VaultFileCodec.Create(CreateDocument(), "correct".AsSpan(), TestKdf);
        byte[] file = created.Encode();

        foreach (int offset in new[] { 12, 36, 52, 76, 124, 156, file.Length - 1 })
        {
            byte[] modified = (byte[])file.Clone();
            modified[offset] ^= 1;

            Assert.ThrowsAny<Exception>(() => VaultFileCodec.Open(modified, "correct".AsSpan()));
        }
    }

    [Fact]
    public void TruncationAndTrailingDataAreRejectedBeforePasswordWork()
    {
        using VaultSession created = VaultFileCodec.Create(CreateDocument(), "correct".AsSpan(), TestKdf);
        byte[] file = created.Encode();

        Assert.Throws<VaultFormatException>(
            () => VaultFileCodec.Open(file.AsSpan(0, file.Length - 1), "correct".AsSpan()));
        Assert.Throws<VaultFormatException>(
            () => VaultFileCodec.Open([.. file, 0], "correct".AsSpan()));
    }

    [Fact]
    public void ExcessiveKdfMemoryIsRejectedBeforeArgon2()
    {
        using VaultSession created = VaultFileCodec.Create(CreateDocument(), "correct".AsSpan(), TestKdf);
        byte[] file = created.Encode();
        BinaryPrimitives.WriteUInt64LittleEndian(file.AsSpan(28, 8), ulong.MaxValue);

        Assert.Throws<VaultFormatException>(
            () => VaultFileCodec.Open(file, "correct".AsSpan()));
    }

    [Fact]
    public void RepeatedSavesUseDifferentPayloadNonces()
    {
        using VaultSession created = VaultFileCodec.Create(CreateDocument(), "correct".AsSpan(), TestKdf);

        byte[] first = created.Encode();
        byte[] second = created.Encode();

        Assert.NotEqual(first.AsSpan(124, Sodium.NonceBytes).ToArray(), second.AsSpan(124, Sodium.NonceBytes).ToArray());
        using VaultSession firstOpened = VaultFileCodec.Open(first, "correct".AsSpan());
        using VaultSession secondOpened = VaultFileCodec.Open(second, "correct".AsSpan());
    }

    [Fact]
    public void NewVaultsUseIndependentSaltsWrappedKeysAndNonces()
    {
        using VaultSession firstSession = VaultFileCodec.Create(
            CreateDocument(),
            "correct".AsSpan(),
            TestKdf);
        using VaultSession secondSession = VaultFileCodec.Create(
            CreateDocument(),
            "correct".AsSpan(),
            TestKdf);
        byte[] first = firstSession.Encode();
        byte[] second = secondSession.Encode();

        Assert.NotEqual(first.AsSpan(36, Sodium.SaltBytes).ToArray(),
            second.AsSpan(36, Sodium.SaltBytes).ToArray());
        Assert.NotEqual(first.AsSpan(52, Sodium.NonceBytes).ToArray(),
            second.AsSpan(52, Sodium.NonceBytes).ToArray());
        Assert.NotEqual(first.AsSpan(76, Sodium.KeyBytes + Sodium.TagBytes).ToArray(),
            second.AsSpan(76, Sodium.KeyBytes + Sodium.TagBytes).ToArray());
        Assert.NotEqual(first.AsSpan(124, Sodium.NonceBytes).ToArray(),
            second.AsSpan(124, Sodium.NonceBytes).ToArray());
    }

    [Fact]
    public void RandomMalformedInputsAreRejectedWithoutUncontrolledFailures()
    {
        Random random = new(0x46504153);
        for (int iteration = 0; iteration < 500; iteration++)
        {
            byte[] malformed = new byte[random.Next(0, 4097)];
            random.NextBytes(malformed);

            Assert.Throws<VaultFormatException>(() =>
                VaultFileCodec.Open(malformed, "correct".AsSpan()));
        }
    }

    [Fact]
    public void MasterPasswordChangeRewrapsTheVault()
    {
        using VaultSession created = VaultFileCodec.Create(CreateDocument(), "old password".AsSpan(), TestKdf);
        created.ChangeMasterPassword("new password".AsSpan(), TestKdf);
        byte[] file = created.Encode();

        Assert.Throws<VaultAuthenticationException>(
            () => VaultFileCodec.Open(file, "old password".AsSpan()));
        using VaultSession opened = VaultFileCodec.Open(file, "new password".AsSpan());
        Assert.Equal("generated-secret", opened.Document.Entries.Single().Password);
    }

    [Fact]
    public void MasterPasswordChangeRetainsExistingKdfCostByDefault()
    {
        using VaultSession created = VaultFileCodec.Create(
            CreateDocument(),
            "old password".AsSpan(),
            TestKdf);

        created.ChangeMasterPassword("new password".AsSpan());

        Assert.Equal(TestKdf, created.KdfParameters);
    }

    private static VaultDocument CreateDocument()
    {
        Guid folderId = Guid.NewGuid();
        return new VaultDocument
        {
            VaultId = Guid.NewGuid(),
            Revision = 1,
            Name = "Personal",
            CreatedUtc = Now,
            UpdatedUtc = Now,
            Folders =
            [
                new VaultFolder
                {
                    Id = folderId,
                    Name = "General",
                    CreatedUtc = Now,
                    UpdatedUtc = Now,
                },
            ],
            Entries =
            [
                new VaultEntry
                {
                    Id = Guid.NewGuid(),
                    FolderId = folderId,
                    Title = "Admin",
                    Username = "person@example.test",
                    Password = "generated-secret",
                    Url = "https://example.test/",
                    Notes = "A note",
                    Tags = ["work"],
                    CustomFields =
                    [
                        new VaultCustomField { Name = "Tenant", Value = "Example" },
                    ],
                    CreatedUtc = Now,
                    UpdatedUtc = Now,
                },
            ],
        };
    }
}
