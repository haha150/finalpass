namespace Finalpass.Core;

public static class VaultOperations
{
    public static VaultFolder AddFolder(
        VaultDocument vault,
        string name,
        Guid? parentId = null,
        DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(vault);
        string validatedName = ValidateFolderName(name);
        if (parentId is Guid id && vault.Folders.All(folder => folder.Id != id))
        {
            throw new VaultValidationException("The parent folder does not exist.");
        }

        DateTimeOffset timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        VaultFolder folder = new()
        {
            Id = Guid.NewGuid(),
            ParentId = parentId,
            Name = validatedName,
            CreatedUtc = timestamp,
            UpdatedUtc = timestamp,
        };
        vault.Folders.Add(folder);
        return folder;
    }

    public static void RenameFolder(
        VaultDocument vault,
        Guid folderId,
        string name,
        DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(vault);
        VaultFolder folder = vault.Folders.FirstOrDefault(item => item.Id == folderId)
            ?? throw new VaultValidationException("The folder does not exist.");
        folder.Name = ValidateFolderName(name);
        folder.UpdatedUtc = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();
    }

    public static void DeleteFolder(
        VaultDocument vault,
        Guid folderId,
        DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(vault);
        VaultFolder folder = vault.Folders.FirstOrDefault(item => item.Id == folderId)
            ?? throw new VaultValidationException("The folder does not exist.");
        DateTimeOffset timestamp = (now ?? DateTimeOffset.UtcNow).ToUniversalTime();

        foreach (VaultFolder child in vault.Folders.Where(item => item.ParentId == folderId))
        {
            child.ParentId = folder.ParentId;
            child.UpdatedUtc = timestamp;
        }

        foreach (VaultEntry entry in vault.Entries.Where(item => item.FolderId == folderId))
        {
            entry.FolderId = folder.ParentId;
            entry.UpdatedUtc = timestamp;
        }

        vault.Folders.Remove(folder);
        VaultValidation.Validate(vault);
    }

    private static string ValidateFolderName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        string trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            throw new VaultValidationException("A folder name is required.");
        }

        if (trimmed.Length > 256)
        {
            throw new VaultValidationException("A folder name cannot exceed 256 characters.");
        }

        return trimmed;
    }
}
