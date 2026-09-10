namespace Finalpass.Core;

public static class VaultValidation
{
    public const int MaximumFolders = 10_000;
    public const int MaximumEntries = 100_000;

    public static void Validate(VaultDocument vault)
    {
        ArgumentNullException.ThrowIfNull(vault);

        if (vault.SchemaVersion != VaultDocument.CurrentSchemaVersion)
        {
            throw new VaultValidationException($"Unsupported schema version {vault.SchemaVersion}.");
        }

        RequireNonEmpty(vault.VaultId, "Vault ID");
        RequireText(vault.Name, 256, "Vault name");
        RequireUtc(vault.CreatedUtc, "Vault created timestamp");
        RequireUtc(vault.UpdatedUtc, "Vault updated timestamp");

        if (vault.Folders is null)
        {
            throw new VaultValidationException("Folders cannot be null.");
        }

        if (vault.Entries is null)
        {
            throw new VaultValidationException("Entries cannot be null.");
        }

        if (vault.Folders.Count > MaximumFolders)
        {
            throw new VaultValidationException($"A vault can contain at most {MaximumFolders} folders.");
        }

        if (vault.Entries.Count > MaximumEntries)
        {
            throw new VaultValidationException($"A vault can contain at most {MaximumEntries} entries.");
        }

        Dictionary<Guid, VaultFolder> folders = [];
        foreach (VaultFolder folder in vault.Folders)
        {
            if (folder is null)
            {
                throw new VaultValidationException("Folders cannot contain null values.");
            }

            RequireNonEmpty(folder.Id, "Folder ID");
            RequireText(folder.Name, 256, "Folder name");
            RequireUtc(folder.CreatedUtc, "Folder created timestamp");
            RequireUtc(folder.UpdatedUtc, "Folder updated timestamp");
            if (!folders.TryAdd(folder.Id, folder))
            {
                throw new VaultValidationException($"Duplicate folder ID {folder.Id}.");
            }
        }

        foreach (VaultFolder folder in vault.Folders)
        {
            if (folder.ParentId is Guid parentId && !folders.ContainsKey(parentId))
            {
                throw new VaultValidationException($"Folder {folder.Id} has an unknown parent.");
            }
        }

        foreach (VaultFolder folder in vault.Folders)
        {
            ValidateFolderAncestry(folder, folders);
        }

        HashSet<Guid> entryIds = [];
        foreach (VaultEntry entry in vault.Entries)
        {
            if (entry is null)
            {
                throw new VaultValidationException("Entries cannot contain null values.");
            }

            RequireNonEmpty(entry.Id, "Entry ID");
            if (!entryIds.Add(entry.Id))
            {
                throw new VaultValidationException($"Duplicate entry ID {entry.Id}.");
            }

            if (entry.FolderId is Guid folderId && !folders.ContainsKey(folderId))
            {
                throw new VaultValidationException($"Entry {entry.Id} has an unknown folder.");
            }

            RequireText(entry.Title, 512, "Entry title");
            RequireOptionalText(entry.Username, 4_096, "Username");
            RequireOptionalText(entry.Password, 16_384, "Password");
            RequireOptionalText(entry.Url, 8_192, "URL");
            RequireOptionalText(entry.Notes, 1_048_576, "Notes");
            RequireUtc(entry.CreatedUtc, "Entry created timestamp");
            RequireUtc(entry.UpdatedUtc, "Entry updated timestamp");

            if (entry.Tags is null)
            {
                throw new VaultValidationException("Entry tags cannot be null.");
            }

            if (entry.Tags.Count > 128)
            {
                throw new VaultValidationException("An entry can contain at most 128 tags.");
            }

            foreach (string tag in entry.Tags)
            {
                RequireText(tag, 128, "Tag");
            }

            if (entry.CustomFields is null)
            {
                throw new VaultValidationException("Entry custom fields cannot be null.");
            }

            if (entry.CustomFields.Count > 128)
            {
                throw new VaultValidationException("An entry can contain at most 128 custom fields.");
            }

            foreach (VaultCustomField field in entry.CustomFields)
            {
                if (field is null)
                {
                    throw new VaultValidationException("Custom fields cannot contain null values.");
                }

                RequireText(field.Name, 256, "Custom field name");
                RequireOptionalText(field.Value, 65_536, "Custom field value");
            }
        }
    }

    private static void ValidateFolderAncestry(
        VaultFolder folder,
        Dictionary<Guid, VaultFolder> folders)
    {
        HashSet<Guid> visited = [folder.Id];
        Guid? current = folder.ParentId;

        while (current is Guid currentId)
        {
            if (!visited.Add(currentId))
            {
                throw new VaultValidationException($"Folder {folder.Id} is part of a cycle.");
            }

            current = folders[currentId].ParentId;
        }
    }

    private static void RequireNonEmpty(Guid value, string fieldName)
    {
        if (value == Guid.Empty)
        {
            throw new VaultValidationException($"{fieldName} cannot be empty.");
        }
    }

    private static void RequireText(string? value, int maximumLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new VaultValidationException($"{fieldName} cannot be empty.");
        }

        RequireOptionalText(value, maximumLength, fieldName);
    }

    private static void RequireOptionalText(string? value, int maximumLength, string fieldName)
    {
        if (value is null)
        {
            throw new VaultValidationException($"{fieldName} cannot be null.");
        }

        if (value.Length > maximumLength)
        {
            throw new VaultValidationException($"{fieldName} exceeds {maximumLength} characters.");
        }
    }

    private static void RequireUtc(DateTimeOffset value, string fieldName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new VaultValidationException($"{fieldName} must be UTC.");
        }
    }
}

public sealed class VaultValidationException(string message) : Exception(message);
