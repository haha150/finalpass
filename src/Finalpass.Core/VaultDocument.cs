namespace Finalpass.Core;

public sealed class VaultDocument
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public required Guid VaultId { get; init; }

    public ulong Revision { get; set; }

    public required string Name { get; set; }

    public required DateTimeOffset CreatedUtc { get; init; }

    public required DateTimeOffset UpdatedUtc { get; set; }

    public List<VaultFolder> Folders { get; init; } = [];

    public List<VaultEntry> Entries { get; init; } = [];

    public static VaultDocument Create(string name, DateTimeOffset? now = null)
    {
        DateTimeOffset created = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        return new VaultDocument
        {
            VaultId = Guid.NewGuid(),
            Revision = 0,
            Name = name.Trim(),
            CreatedUtc = created,
            UpdatedUtc = created,
        };
    }
}

public sealed class VaultFolder
{
    public required Guid Id { get; init; }

    public Guid? ParentId { get; set; }

    public required string Name { get; set; }

    public required DateTimeOffset CreatedUtc { get; init; }

    public required DateTimeOffset UpdatedUtc { get; set; }
}

public sealed class VaultEntry
{
    public required Guid Id { get; init; }

    public Guid? FolderId { get; set; }

    public required string Title { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public List<string> Tags { get; init; } = [];

    public bool Favorite { get; set; }

    public List<VaultCustomField> CustomFields { get; init; } = [];

    public required DateTimeOffset CreatedUtc { get; init; }

    public required DateTimeOffset UpdatedUtc { get; set; }
}

public sealed class VaultCustomField
{
    public required string Name { get; set; }

    public string Value { get; set; } = string.Empty;

    public bool Secret { get; set; }
}
