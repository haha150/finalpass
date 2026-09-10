using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using Finalpass.Core;
using Finalpass.Infrastructure;

namespace Finalpass.App.ViewModels;

public sealed class VaultEntryListItem(VaultEntry entry, string folderPath)
{
    public VaultEntry Entry { get; } = entry;
    public Guid Id => Entry.Id;
    public string Title => Entry.Title;
    public string Username => Entry.Username;
    public string FolderPath { get; } = folderPath;
    public string FavoriteGlyph => Entry.Favorite ? "\uE735" : string.Empty;
}

/// <summary>
/// A folder entry for display, used both by the left-pane folder filter list
/// (where <see cref="Id"/> may be null to mean "All items") and by the entry
/// editor's folder picker (where a null <see cref="Id"/> means "No folder").
/// </summary>
public sealed class FolderListItem(Guid? id, string name, int depth, string noneLabel)
{
    public Guid? Id { get; } = id;
    public string Name { get; } = name;
    public int Depth { get; } = depth;
    public string DisplayName => Id is null
        ? noneLabel
        : new string(' ', Depth * 2) + Name;
}

public sealed class TagListItem(string? name)
{
    public string? Name { get; } = name;

    public string DisplayName => Name ?? "All tags";
}

public enum FilterChipKind
{
    Folder,
    Tag,
    Favorites,
    PasswordSearch,
}

public sealed record FilterChipItem(FilterChipKind Kind, string Label);

/// <summary>
/// Editable wrapper around a <see cref="VaultCustomField"/> for the entry
/// editor's custom-field list. Edits write straight through to the underlying
/// model and notify the owning view model so it can mark the vault dirty.
/// </summary>
public sealed class CustomFieldItem : ObservableObject
{
    private readonly Action _onChanged;
    private string _name;
    private string _value;
    private bool _secret;

    public CustomFieldItem(VaultCustomField field, Action onChanged, bool isReadOnly = false)
    {
        Field = field;
        _onChanged = onChanged;
        _name = field.Name;
        _value = field.Value;
        _secret = field.Secret;
        IsReadOnly = isReadOnly;
    }

    public VaultCustomField Field { get; }

    public bool IsReadOnly { get; }

    public bool IsWritable => !IsReadOnly;

    public string Name
    {
        get => _name;
        set
        {
            if (IsReadOnly)
            {
                return;
            }

            if (SetProperty(ref _name, value))
            {
                Field.Name = value;
                _onChanged();
            }
        }
    }

    public string Value
    {
        get => _value;
        set
        {
            if (IsReadOnly)
            {
                return;
            }

            if (SetProperty(ref _value, value))
            {
                Field.Value = value;
                _onChanged();
            }
        }
    }

    public bool Secret
    {
        get => _secret;
        set
        {
            if (IsReadOnly)
            {
                return;
            }

            if (SetProperty(ref _secret, value))
            {
                Field.Secret = value;
                _onChanged();
                OnPropertyChanged(nameof(NotSecret));
            }
        }
    }

    public bool NotSecret => !Secret;
}

public sealed class MainPageViewModel : ObservableObject, IDisposable
{
    private OpenedVault? _openedVault;
    private VaultEntryListItem? _selectedEntry;
    private string _searchText = string.Empty;
    private bool _includePasswordsInSearch;
    private bool _favoritesOnly;
    private int _sortIndex;
    private bool _sortDescending;
    private string _statusText = "Create or open a vault to begin.";
    private string _entryTitle = string.Empty;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _url = string.Empty;
    private string _notes = string.Empty;
    private string _tagsText = string.Empty;
    private bool _favorite;
    private Guid? _entryFolderId;
    private FolderListItem? _selectedFolder;
    private bool _loadingEditor;
    private bool _refreshingFolders;
    private IReadOnlyList<VaultEntryListItem> _entries = [];
    private TagListItem? _selectedTag;

    public event EventHandler? VaultChanged;

    public IReadOnlyList<VaultEntryListItem> Entries
    {
        get => _entries;
        private set => SetProperty(ref _entries, value);
    }

    public ObservableCollection<FolderListItem> Folders { get; } = [];
    public ObservableCollection<FolderListItem> FolderOptions { get; } = [];
    public ObservableCollection<TagListItem> Tags { get; } = [];
    public ObservableCollection<FilterChipItem> FilterChips { get; } = [];
    public ObservableCollection<CustomFieldItem> CustomFields { get; } = [];
    public bool IsVaultOpen => _openedVault is not null;
    public bool IsWritable => _openedVault is { IsReadOnly: false };
    public bool IsReadOnly => _openedVault?.IsReadOnly == true;
    public bool HasSelection => SelectedEntry is not null;
    public bool IsDirty { get; private set; }
    public string VaultName => _openedVault?.Session.Document.Name ?? string.Empty;
    public string VaultPath => _openedVault?.Path ?? string.Empty;

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                RefreshEntries();
            }
        }
    }

    public bool IncludePasswordsInSearch
    {
        get => _includePasswordsInSearch;
        set
        {
            if (SetProperty(ref _includePasswordsInSearch, value))
            {
                RefreshFilterChips();
                RefreshEntries();
            }
        }
    }

    public bool FavoritesOnly
    {
        get => _favoritesOnly;
        set
        {
            if (SetProperty(ref _favoritesOnly, value))
            {
                RefreshFilterChips();
                RefreshEntries();
            }
        }
    }

    public int SortIndex
    {
        get => _sortIndex;
        set
        {
            if (SetProperty(ref _sortIndex, value))
            {
                RefreshEntries();
            }
        }
    }

    public bool SortDescending
    {
        get => _sortDescending;
        set
        {
            if (SetProperty(ref _sortDescending, value))
            {
                RefreshEntries();
            }
        }
    }

    public VaultEntryListItem? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (SetProperty(ref _selectedEntry, value))
            {
                LoadEditor(value?.Entry);
                OnPropertyChanged(nameof(HasSelection));
            }
        }
    }

    public string EntryTitle
    {
        get => _entryTitle;
        set
        {
            if (SetEditorProperty(ref _entryTitle, value) && SelectedEntry is not null)
            {
                SelectedEntry.Entry.Title = value;
            }
        }
    }

    public string Username
    {
        get => _username;
        set
        {
            if (SetEditorProperty(ref _username, value) && SelectedEntry is not null)
            {
                SelectedEntry.Entry.Username = value;
            }
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (SetEditorProperty(ref _password, value) && SelectedEntry is not null)
            {
                SelectedEntry.Entry.Password = value;
            }
        }
    }

    public string Url
    {
        get => _url;
        set
        {
            if (SetEditorProperty(ref _url, value) && SelectedEntry is not null)
            {
                SelectedEntry.Entry.Url = value;
            }
        }
    }

    public string Notes
    {
        get => _notes;
        set
        {
            if (SetEditorProperty(ref _notes, value) && SelectedEntry is not null)
            {
                SelectedEntry.Entry.Notes = value;
            }
        }
    }

    public string TagsText
    {
        get => _tagsText;
        set
        {
            if (SetEditorProperty(ref _tagsText, value) && SelectedEntry is not null)
            {
                SelectedEntry.Entry.Tags.Clear();
                SelectedEntry.Entry.Tags.AddRange(ParseTags(value));
            }
        }
    }

    public bool Favorite
    {
        get => _favorite;
        set
        {
            if (SetEditorProperty(ref _favorite, value) && SelectedEntry is not null)
            {
                SelectedEntry.Entry.Favorite = value;
            }
        }
    }

    public Guid? EntryFolderId => _entryFolderId;

    /// <summary>
    /// The folder-picker selection for the entry editor, bound via SelectedItem
    /// rather than SelectedValue/SelectedValuePath (which does not reliably
    /// reflect programmatic changes in the WinUI ComboBox).
    /// </summary>
    public FolderListItem? SelectedFolderOption
    {
        get => FolderOptions.FirstOrDefault(option => option.Id == _entryFolderId);
        set
        {
            // WinUI temporarily clears ComboBox.SelectedItem while its item source
            // is rebuilt. That is view churn, not a request to unfile the entry.
            if (_refreshingFolders)
            {
                return;
            }

            if (!IsWritable && SelectedEntry is not null)
            {
                return;
            }

            Guid? newFolderId = value?.Id;
            if (_entryFolderId == newFolderId)
            {
                return;
            }

            _entryFolderId = newFolderId;
            OnPropertyChanged();
            if (!_loadingEditor && SelectedEntry is not null)
            {
                SelectedEntry.Entry.FolderId = newFolderId;
                MarkDirty();
                RefreshEntries(SelectedEntry.Id);
            }
        }
    }

    public FolderListItem? SelectedFolder
    {
        get => _selectedFolder;
        set
        {
            if (SetProperty(ref _selectedFolder, value))
            {
                RefreshFilterChips();
                RefreshEntries();
            }
        }
    }

    public TagListItem? SelectedTag
    {
        get => _selectedTag;
        set
        {
            if (SetProperty(ref _selectedTag, value))
            {
                RefreshFilterChips();
                RefreshEntries();
            }
        }
    }

    public void SetOpenedVault(OpenedVault openedVault)
    {
        ArgumentNullException.ThrowIfNull(openedVault);
        CloseVault();
        _openedVault = openedVault;
        IsDirty = false;
        OnPropertyChanged(nameof(IsVaultOpen));
        OnPropertyChanged(nameof(IsWritable));
        OnPropertyChanged(nameof(IsReadOnly));
        OnPropertyChanged(nameof(VaultName));
        OnPropertyChanged(nameof(VaultPath));
        RefreshFolders();
        RefreshTags();
        RefreshEntries();
        StatusText = openedVault.IsReadOnly
            ? $"Opened {VaultName} read-only because another process is editing it."
            : $"Opened {VaultName}.";
    }

    public OpenedVault RequireOpenedVault() =>
        _openedVault ?? throw new InvalidOperationException("No vault is open.");

    public void AddEntry()
    {
        EnsureWritable();
        VaultDocument document = RequireOpenedVault().Session.Document;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        VaultEntry entry = new()
        {
            Id = Guid.NewGuid(),
            Title = "New login",
            FolderId = SelectedFolder?.Id,
            CreatedUtc = now,
            UpdatedUtc = now,
        };
        document.Entries.Add(entry);
        MarkDirty(touchSelectedEntry: false);
        RefreshEntries(entry.Id);
        StatusText = "New entry added. Save the vault to persist it.";
    }

    public void AddCustomField()
    {
        EnsureWritable();
        VaultEntry? entry = SelectedEntry?.Entry;
        if (entry is null)
        {
            return;
        }

        VaultCustomField field = new() { Name = "Custom field", Value = string.Empty };
        entry.CustomFields.Add(field);
        CustomFields.Add(new CustomFieldItem(field, () => MarkDirty()));
        MarkDirty();
    }

    public void RemoveCustomField(CustomFieldItem item)
    {
        EnsureWritable();
        VaultEntry? entry = SelectedEntry?.Entry;
        if (entry is null)
        {
            return;
        }

        entry.CustomFields.Remove(item.Field);
        CustomFields.Remove(item);
        MarkDirty();
    }

    public void AddFolder(string name, Guid? parentId)
    {
        EnsureWritable();
        VaultDocument document = RequireOpenedVault().Session.Document;
        VaultFolder folder = VaultOperations.AddFolder(document, name, parentId);
        MarkDirty(touchSelectedEntry: false);
        RefreshFolders();
        StatusText = $"Folder \"{folder.Name}\" created. Save the vault to persist it.";
    }

    public void RenameFolder(Guid folderId, string name)
    {
        EnsureWritable();
        VaultDocument document = RequireOpenedVault().Session.Document;
        VaultOperations.RenameFolder(document, folderId, name);
        VaultFolder folder = document.Folders.Single(item => item.Id == folderId);
        MarkDirty(touchSelectedEntry: false);
        RefreshFolders();
        RefreshEntries();
        StatusText = $"Folder renamed to \"{folder.Name}\". Save the vault to persist it.";
    }

    public void DeleteFolder(Guid folderId)
    {
        EnsureWritable();
        VaultDocument document = RequireOpenedVault().Session.Document;
        VaultFolder folder = document.Folders.Single(item => item.Id == folderId);
        Guid? parentId = folder.ParentId;
        VaultOperations.DeleteFolder(document, folderId);

        if (SelectedFolder?.Id == folderId)
        {
            _selectedFolder = parentId is Guid id
                ? Folders.FirstOrDefault(item => item.Id == id)
                : null;
            OnPropertyChanged(nameof(SelectedFolder));
        }

        MarkDirty(touchSelectedEntry: false);
        RefreshFolders();
        RefreshEntries();
        StatusText = "Folder deleted. Its entries and child folders were kept at the parent level. " +
            "Save the vault to persist it.";
    }

    public bool ApplyEditor()
    {
        if (!IsWritable)
        {
            return true;
        }

        VaultEntry? entry = SelectedEntry?.Entry;
        if (entry is null)
        {
            return true;
        }

        string title = EntryTitle.Trim();
        if (title.Length == 0)
        {
            StatusText = "An entry title is required.";
            return false;
        }

        if (!string.Equals(entry.Title, title, StringComparison.Ordinal))
        {
            entry.Title = title;
            _entryTitle = title;
            OnPropertyChanged(nameof(EntryTitle));
            MarkDirty();
        }

        VaultValidation.Validate(RequireOpenedVault().Session.Document);
        RefreshTags();
        RefreshEntries(entry.Id);
        return true;
    }

    public void MarkSaved()
    {
        IsDirty = false;
        OnPropertyChanged(nameof(IsDirty));
        StatusText = $"Saved {VaultName}.";
    }

    public void DeleteSelected()
    {
        EnsureWritable();
        VaultEntry? entry = SelectedEntry?.Entry;
        if (entry is null)
        {
            return;
        }

        RequireOpenedVault().Session.Document.Entries.Remove(entry);
        MarkDirty(touchSelectedEntry: false);
        RefreshEntries();
        StatusText = "Entry deleted. Save the vault to persist the change.";
    }

    public void CloseVault()
    {
        _openedVault?.Dispose();
        _openedVault = null;
        Entries = [];
        Folders.Clear();
        FolderOptions.Clear();
        Tags.Clear();
        FilterChips.Clear();
        CustomFields.Clear();
        SelectedEntry = null;
        _selectedFolder = null;
        _searchText = string.Empty;
        _includePasswordsInSearch = false;
        _favoritesOnly = false;
        _selectedTag = null;
        IsDirty = false;
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(IncludePasswordsInSearch));
        OnPropertyChanged(nameof(FavoritesOnly));
        OnPropertyChanged(nameof(SelectedTag));
        OnPropertyChanged(nameof(SelectedFolder));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(IsVaultOpen));
        OnPropertyChanged(nameof(IsWritable));
        OnPropertyChanged(nameof(IsReadOnly));
        OnPropertyChanged(nameof(VaultName));
        OnPropertyChanged(nameof(VaultPath));
        StatusText = "Vault locked.";
    }

    public void Dispose()
    {
        CloseVault();
        GC.SuppressFinalize(this);
    }

    private void RefreshEntries(Guid? selectId = null)
    {
        if (_openedVault is null)
        {
            Entries = [];
            return;
        }

        Guid? previousId = selectId ?? SelectedEntry?.Id;
        VaultSortField sort = SortIndex switch
        {
            1 => VaultSortField.Username,
            2 => VaultSortField.Url,
            3 => VaultSortField.Folder,
            4 => VaultSortField.CreatedUtc,
            5 => VaultSortField.UpdatedUtc,
            _ => VaultSortField.Title,
        };
        IReadOnlyList<VaultEntryResult> results = VaultQuery.Apply(
            _openedVault.Session.Document,
            new VaultQueryOptions(
                Query: SearchText,
                FolderId: SelectedFolder?.Id,
                Tag: SelectedTag?.Name,
                FavoritesOnly: FavoritesOnly,
                IncludePasswords: IncludePasswordsInSearch,
                SortBy: sort,
                Descending: SortDescending));

        Entries = results
            .Select(result => new VaultEntryListItem(result.Entry, result.FolderPath))
            .ToArray();

        SelectedEntry = previousId is Guid id
            ? Entries.FirstOrDefault(item => item.Id == id)
            : null;
        StatusText = $"{Entries.Count:N0} of {_openedVault.Session.Document.Entries.Count:N0} entries shown.";
    }

    private void LoadEditor(VaultEntry? entry)
    {
        _loadingEditor = true;
        try
        {
            EntryTitle = entry?.Title ?? string.Empty;
            Username = entry?.Username ?? string.Empty;
            Password = entry?.Password ?? string.Empty;
            Url = entry?.Url ?? string.Empty;
            Notes = entry?.Notes ?? string.Empty;
            TagsText = entry is null ? string.Empty : string.Join(", ", entry.Tags);
            Favorite = entry?.Favorite ?? false;
            _entryFolderId = entry?.FolderId;
            OnPropertyChanged(nameof(EntryFolderId));
            OnPropertyChanged(nameof(SelectedFolderOption));
            CustomFields.Clear();
            if (entry is not null)
            {
                foreach (VaultCustomField field in entry.CustomFields)
                {
                    CustomFields.Add(new CustomFieldItem(field, () => MarkDirty(), IsReadOnly));
                }
            }
        }
        finally
        {
            _loadingEditor = false;
        }
    }

    private void RefreshFolders()
    {
        Guid? previousSelectedFolderId = _selectedFolder?.Id;
        _refreshingFolders = true;
        try
        {
            Folders.Clear();
            FolderOptions.Clear();
            if (_openedVault is null)
            {
                return;
            }

            Folders.Add(new FolderListItem(null, string.Empty, 0, "All items"));
            FolderOptions.Add(new FolderListItem(null, string.Empty, 0, "No folder"));

            List<VaultFolder> ordered = RequireOpenedVault().Session.Document.Folders
                .OrderBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach ((VaultFolder folder, int depth) in FlattenFolders(ordered))
            {
                Folders.Add(new FolderListItem(folder.Id, folder.Name, depth, string.Empty));
                FolderOptions.Add(new FolderListItem(folder.Id, folder.Name, depth, string.Empty));
            }

            _selectedFolder = previousSelectedFolderId is Guid selectedId
                ? Folders.FirstOrDefault(item => item.Id == selectedId)
                : Folders.FirstOrDefault();
        }
        finally
        {
            _refreshingFolders = false;
        }

        OnPropertyChanged(nameof(SelectedFolder));
        OnPropertyChanged(nameof(SelectedFolderOption));
    }

    private void RefreshTags()
    {
        string? previousTag = _selectedTag?.Name;
        Tags.Clear();
        Tags.Add(new TagListItem(null));
        if (_openedVault is not null)
        {
            IEnumerable<string> tags = _openedVault.Session.Document.Entries
                .SelectMany(entry => entry.Tags)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase);
            foreach (string tag in tags)
            {
                Tags.Add(new TagListItem(tag));
            }
        }

        _selectedTag = previousTag is null
            ? Tags.FirstOrDefault()
            : Tags.FirstOrDefault(item =>
                string.Equals(item.Name, previousTag, StringComparison.OrdinalIgnoreCase));
        OnPropertyChanged(nameof(SelectedTag));
        RefreshFilterChips();
    }

    public void ClearFilter(FilterChipKind kind)
    {
        switch (kind)
        {
            case FilterChipKind.Folder:
                SelectedFolder = Folders.FirstOrDefault();
                break;
            case FilterChipKind.Tag:
                SelectedTag = Tags.FirstOrDefault();
                break;
            case FilterChipKind.Favorites:
                FavoritesOnly = false;
                break;
            case FilterChipKind.PasswordSearch:
                IncludePasswordsInSearch = false;
                break;
        }
    }

    public void ClearAllFilters()
    {
        _selectedFolder = Folders.FirstOrDefault();
        _selectedTag = Tags.FirstOrDefault();
        _favoritesOnly = false;
        _includePasswordsInSearch = false;
        OnPropertyChanged(nameof(SelectedFolder));
        OnPropertyChanged(nameof(SelectedTag));
        OnPropertyChanged(nameof(FavoritesOnly));
        OnPropertyChanged(nameof(IncludePasswordsInSearch));
        RefreshFilterChips();
        RefreshEntries();
    }

    private void RefreshFilterChips()
    {
        FilterChips.Clear();
        if (_selectedFolder?.Id is not null)
        {
            FilterChips.Add(new FilterChipItem(
                FilterChipKind.Folder,
                $"Folder: {_selectedFolder.Name}  ×"));
        }

        if (_selectedTag?.Name is string tag)
        {
            FilterChips.Add(new FilterChipItem(FilterChipKind.Tag, $"Tag: {tag}  ×"));
        }

        if (_favoritesOnly)
        {
            FilterChips.Add(new FilterChipItem(FilterChipKind.Favorites, "Favorites  ×"));
        }

        if (_includePasswordsInSearch)
        {
            FilterChips.Add(new FilterChipItem(FilterChipKind.PasswordSearch, "Password search  ×"));
        }
    }

    private static IEnumerable<(VaultFolder Folder, int Depth)> FlattenFolders(
        IReadOnlyList<VaultFolder> folders,
        Guid? parentId = null,
        int depth = 0)
    {
        foreach (VaultFolder folder in folders.Where(f => f.ParentId == parentId))
        {
            yield return (folder, depth);
            foreach ((VaultFolder Folder, int Depth) child in FlattenFolders(folders, folder.Id, depth + 1))
            {
                yield return child;
            }
        }
    }

    private bool SetEditorProperty<T>(
        ref T storage,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (!_loadingEditor && SelectedEntry is not null && !IsWritable)
        {
            return false;
        }

        bool changed = SetProperty(ref storage, value, propertyName);
        if (changed && !_loadingEditor && SelectedEntry is not null)
        {
            MarkDirty();
        }

        return changed && !_loadingEditor;
    }

    private static IEnumerable<string> ParseTags(string value) => value
        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase);

    private void MarkDirty(bool touchSelectedEntry = true)
    {
        EnsureWritable();
        if (touchSelectedEntry && SelectedEntry is not null)
        {
            SelectedEntry.Entry.UpdatedUtc = DateTimeOffset.UtcNow;
        }

        IsDirty = true;
        OnPropertyChanged(nameof(IsDirty));
        VaultChanged?.Invoke(this, EventArgs.Empty);
    }

    private void EnsureWritable()
    {
        if (!IsWritable)
        {
            throw new InvalidOperationException("The vault is open read-only.");
        }
    }
}
