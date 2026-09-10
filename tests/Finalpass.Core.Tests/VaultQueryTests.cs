using Finalpass.Core;

namespace Finalpass.Core.Tests;

public sealed class VaultQueryTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SearchExcludesPasswordsAndSecretFieldsByDefault()
    {
        VaultDocument vault = CreateVault();

        Assert.Empty(VaultQuery.Apply(vault, new VaultQueryOptions("hidden-value")));
        Assert.Empty(VaultQuery.Apply(vault, new VaultQueryOptions("secret-custom")));
        Assert.Single(VaultQuery.Apply(
            vault,
            new VaultQueryOptions("hidden-value", IncludePasswords: true)));
        Assert.Single(VaultQuery.Apply(
            vault,
            new VaultQueryOptions("secret-custom", IncludePasswords: true)));
    }

    [Fact]
    public void SearchIncludesMetadataAndNonSecretCustomFields()
    {
        VaultDocument vault = CreateVault();

        Assert.Single(VaultQuery.Apply(vault, new VaultQueryOptions("EXAMPLE.TEST")));
        Assert.Single(VaultQuery.Apply(vault, new VaultQueryOptions("account number")));
        Assert.Single(VaultQuery.Apply(vault, new VaultQueryOptions("visible-custom")));
        Assert.Single(VaultQuery.Apply(vault, new VaultQueryOptions("Work / Infrastructure")));
    }

    [Fact]
    public void FolderFilterIncludesDescendants()
    {
        VaultDocument vault = CreateVault();
        Guid workId = vault.Folders.Single(folder => folder.Name == "Work").Id;

        IReadOnlyList<VaultEntryResult> results = VaultQuery.Apply(
            vault,
            new VaultQueryOptions(FolderId: workId));

        Assert.Single(results);
        Assert.Equal("Admin portal", results[0].Entry.Title);
    }

    [Fact]
    public void SortIsStableAndSupportsDescending()
    {
        VaultDocument vault = CreateVault();
        vault.Entries.Add(new VaultEntry
        {
            Id = Guid.NewGuid(),
            Title = "Admin portal",
            Username = "second",
            CreatedUtc = Now,
            UpdatedUtc = Now,
        });

        IReadOnlyList<VaultEntryResult> ascending = VaultQuery.Apply(vault, new VaultQueryOptions());
        IReadOnlyList<VaultEntryResult> descending = VaultQuery.Apply(
            vault,
            new VaultQueryOptions(Descending: true));

        Assert.Equal("person@example.test", ascending[0].Entry.Username);
        Assert.Equal("second", ascending[1].Entry.Username);
        Assert.Equal("person@example.test", descending[0].Entry.Username);
        Assert.Equal("second", descending[1].Entry.Username);
    }

    [Fact]
    public void TagFilterComposesWithFavoritesAndFolderFilter()
    {
        VaultDocument vault = CreateVault();
        Guid workId = vault.Folders.Single(folder => folder.Name == "Work").Id;
        vault.Entries[0].Favorite = true;

        Assert.Single(VaultQuery.Apply(
            vault,
            new VaultQueryOptions(
                FolderId: workId,
                Tag: "WORK",
                FavoritesOnly: true)));
        Assert.Empty(VaultQuery.Apply(
            vault,
            new VaultQueryOptions(
                FolderId: workId,
                Tag: "personal",
                FavoritesOnly: true)));
    }

    [Fact]
    public void TenThousandEntryUnicodeQueryRemainsResponsive()
    {
        VaultDocument vault = VaultDocument.Create("Large", Now);
        for (int index = 0; index < 10_000; index++)
        {
            vault.Entries.Add(new VaultEntry
            {
                Id = Guid.NewGuid(),
                Title = index == 9_999 ? "Café 東京" : $"Entry {index:D5}",
                Username = $"person{index}@example.test",
                Tags = [index % 2 == 0 ? "even" : "odd"],
                CreatedUtc = Now,
                UpdatedUtc = Now.AddSeconds(index),
            });
        }

        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        IReadOnlyList<VaultEntryResult> sorted = VaultQuery.Apply(
            vault,
            new VaultQueryOptions(
                Tag: "even",
                SortBy: VaultSortField.UpdatedUtc,
                Descending: true));
        IReadOnlyList<VaultEntryResult> results = VaultQuery.Apply(
            vault,
            new VaultQueryOptions("CAFÉ 東京", SortBy: VaultSortField.UpdatedUtc));
        stopwatch.Stop();

        Assert.Equal(5_000, sorted.Count);
        Assert.Equal("person9998@example.test", sorted[0].Entry.Username);
        Assert.Single(results);
        Assert.Equal("Café 東京", results[0].Entry.Title);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"10,000-entry query took {stopwatch.Elapsed}.");
    }

    private static VaultDocument CreateVault()
    {
        Guid workId = Guid.NewGuid();
        Guid infrastructureId = Guid.NewGuid();
        VaultDocument vault = new()
        {
            VaultId = Guid.NewGuid(),
            Name = "Personal",
            CreatedUtc = Now,
            UpdatedUtc = Now,
            Folders =
            [
                new VaultFolder
                {
                    Id = workId,
                    Name = "Work",
                    CreatedUtc = Now,
                    UpdatedUtc = Now,
                },
                new VaultFolder
                {
                    Id = infrastructureId,
                    ParentId = workId,
                    Name = "Infrastructure",
                    CreatedUtc = Now,
                    UpdatedUtc = Now,
                },
            ],
            Entries =
            [
                new VaultEntry
                {
                    Id = Guid.NewGuid(),
                    FolderId = infrastructureId,
                    Title = "Admin portal",
                    Username = "person@example.test",
                    Password = "hidden-value",
                    Url = "https://example.test/",
                    Notes = "Production",
                    Tags = ["work"],
                    CustomFields =
                    [
                        new VaultCustomField
                        {
                            Name = "Account number",
                            Value = "visible-custom",
                        },
                        new VaultCustomField
                        {
                            Name = "Recovery",
                            Value = "secret-custom",
                            Secret = true,
                        },
                    ],
                    CreatedUtc = Now,
                    UpdatedUtc = Now,
                },
            ],
        };

        VaultValidation.Validate(vault);
        return vault;
    }
}
