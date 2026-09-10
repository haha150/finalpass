using Finalpass.Core;

namespace Finalpass.Core.Tests;

public sealed class VaultValidationTests
{
    [Fact]
    public void ValidateRejectsNullCollectionsFromMalformedJson()
    {
        VaultDocument vault = new()
        {
            VaultId = Guid.NewGuid(),
            Name = "Malformed",
            CreatedUtc = DateTimeOffset.UnixEpoch,
            UpdatedUtc = DateTimeOffset.UnixEpoch,
            Entries = null!,
        };

        VaultValidationException exception = Assert.Throws<VaultValidationException>(
            () => VaultValidation.Validate(vault));

        Assert.Equal("Entries cannot be null.", exception.Message);
    }

    [Fact]
    public void ValidDocumentPasses()
    {
        VaultDocument vault = VaultDocument.Create("Personal", DateTimeOffset.UnixEpoch);

        VaultValidation.Validate(vault);
    }

    [Fact]
    public void FolderCycleIsRejected()
    {
        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        VaultDocument vault = VaultDocument.Create("Personal", DateTimeOffset.UnixEpoch);
        vault.Folders.Add(new VaultFolder
        {
            Id = firstId,
            ParentId = secondId,
            Name = "First",
            CreatedUtc = DateTimeOffset.UnixEpoch,
            UpdatedUtc = DateTimeOffset.UnixEpoch,
        });
        vault.Folders.Add(new VaultFolder
        {
            Id = secondId,
            ParentId = firstId,
            Name = "Second",
            CreatedUtc = DateTimeOffset.UnixEpoch,
            UpdatedUtc = DateTimeOffset.UnixEpoch,
        });

        Assert.Throws<VaultValidationException>(() => VaultValidation.Validate(vault));
    }

    [Fact]
    public void UnknownEntryFolderIsRejected()
    {
        VaultDocument vault = VaultDocument.Create("Personal", DateTimeOffset.UnixEpoch);
        vault.Entries.Add(new VaultEntry
        {
            Id = Guid.NewGuid(),
            FolderId = Guid.NewGuid(),
            Title = "Entry",
            CreatedUtc = DateTimeOffset.UnixEpoch,
            UpdatedUtc = DateTimeOffset.UnixEpoch,
        });

        Assert.Throws<VaultValidationException>(() => VaultValidation.Validate(vault));
    }

    [Fact]
    public void UnknownFolderAncestorIsReportedAsValidationError()
    {
        Guid parentId = Guid.NewGuid();
        VaultDocument vault = VaultDocument.Create("Personal", DateTimeOffset.UnixEpoch);
        vault.Folders.Add(new VaultFolder
        {
            Id = Guid.NewGuid(),
            ParentId = parentId,
            Name = "Child",
            CreatedUtc = DateTimeOffset.UnixEpoch,
            UpdatedUtc = DateTimeOffset.UnixEpoch,
        });
        vault.Folders.Add(new VaultFolder
        {
            Id = parentId,
            ParentId = Guid.NewGuid(),
            Name = "Parent",
            CreatedUtc = DateTimeOffset.UnixEpoch,
            UpdatedUtc = DateTimeOffset.UnixEpoch,
        });

        Assert.Throws<VaultValidationException>(() => VaultValidation.Validate(vault));
    }
}
