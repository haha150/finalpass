using Finalpass.Core;

namespace Finalpass.Core.Tests;

public sealed class VaultOperationsTests
{
    [Fact]
    public void DeleteFolderPreservesChildrenAndEntriesAtParent()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        VaultDocument vault = VaultDocument.Create("Personal", now);
        VaultFolder parent = VaultOperations.AddFolder(vault, "Parent", now: now);
        VaultFolder folder = VaultOperations.AddFolder(vault, "Delete me", parent.Id, now);
        VaultFolder child = VaultOperations.AddFolder(vault, "Child", folder.Id, now);
        VaultEntry entry = new()
        {
            Id = Guid.NewGuid(),
            FolderId = folder.Id,
            Title = "Login",
            CreatedUtc = now,
            UpdatedUtc = now,
        };
        vault.Entries.Add(entry);

        DateTimeOffset deletedAt = now.AddMinutes(1);
        VaultOperations.DeleteFolder(vault, folder.Id, deletedAt);

        Assert.DoesNotContain(vault.Folders, item => item.Id == folder.Id);
        Assert.Equal(parent.Id, child.ParentId);
        Assert.Equal(parent.Id, entry.FolderId);
        Assert.Equal(deletedAt, child.UpdatedUtc);
        Assert.Equal(deletedAt, entry.UpdatedUtc);
        VaultValidation.Validate(vault);
    }

    [Fact]
    public void FolderNamesAreTrimmedAndUnknownParentsAreRejected()
    {
        VaultDocument vault = VaultDocument.Create("Personal", DateTimeOffset.UnixEpoch);

        VaultFolder folder = VaultOperations.AddFolder(
            vault,
            "  Work  ",
            now: DateTimeOffset.UnixEpoch);

        Assert.Equal("Work", folder.Name);
        Assert.Throws<VaultValidationException>(() =>
            VaultOperations.AddFolder(vault, "Child", Guid.NewGuid(), DateTimeOffset.UnixEpoch));
    }
}
