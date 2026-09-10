namespace Finalpass.Core;

public enum VaultSortField
{
    Title,
    Username,
    Url,
    Folder,
    CreatedUtc,
    UpdatedUtc,
}

public sealed record VaultQueryOptions(
    string Query = "",
    Guid? FolderId = null,
    string? Tag = null,
    bool FavoritesOnly = false,
    bool IncludePasswords = false,
    VaultSortField SortBy = VaultSortField.Title,
    bool Descending = false);

public sealed record VaultEntryResult(VaultEntry Entry, string FolderPath);

public static class VaultQuery
{
    public static IReadOnlyList<VaultEntryResult> Apply(
        VaultDocument vault,
        VaultQueryOptions options)
    {
        ArgumentNullException.ThrowIfNull(vault);
        ArgumentNullException.ThrowIfNull(options);

        Dictionary<Guid, VaultFolder> folders = vault.Folders.ToDictionary(folder => folder.Id);
        Dictionary<Guid, string> folderPaths = BuildFolderPaths(folders);
        HashSet<Guid>? includedFolders = options.FolderId is Guid folderId
            ? FindFolderAndDescendants(folderId, vault.Folders)
            : null;

        IEnumerable<VaultEntryResult> results = vault.Entries
            .Select((entry, index) => new IndexedResult(
                new VaultEntryResult(
                    entry,
                    entry.FolderId is Guid id && folderPaths.TryGetValue(id, out string? path)
                        ? path
                        : string.Empty),
                index))
            .Where(item => includedFolders is null ||
                (item.Result.Entry.FolderId is Guid id && includedFolders.Contains(id)))
            .Where(item => !options.FavoritesOnly || item.Result.Entry.Favorite)
            .Where(item => string.IsNullOrWhiteSpace(options.Tag) ||
                item.Result.Entry.Tags.Any(tag => EqualsIgnoreCase(tag, options.Tag)))
            .Where(item => Matches(item.Result, options.Query, options.IncludePasswords))
            .OrderBy(item => item, new ResultComparer(options.SortBy, options.Descending))
            .Select(item => item.Result);

        return results.ToArray();
    }

    private static bool Matches(VaultEntryResult result, string query, bool includePasswords)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        string term = query.Trim();
        VaultEntry entry = result.Entry;

        if (Contains(entry.Title, term) ||
            Contains(entry.Username, term) ||
            Contains(entry.Url, term) ||
            Contains(entry.Notes, term) ||
            Contains(result.FolderPath, term) ||
            entry.Tags.Any(tag => Contains(tag, term)) ||
            entry.CustomFields.Any(field =>
                Contains(field.Name, term) || (!field.Secret && Contains(field.Value, term))))
        {
            return true;
        }

        return includePasswords &&
            (Contains(entry.Password, term) ||
             entry.CustomFields.Any(field => field.Secret && Contains(field.Value, term)));
    }

    private static HashSet<Guid> FindFolderAndDescendants(
        Guid rootId,
        IReadOnlyCollection<VaultFolder> folders)
    {
        Dictionary<Guid, List<Guid>> childrenByParent = [];
        foreach (VaultFolder folder in folders)
        {
            if (folder.ParentId is not Guid parentId)
            {
                continue;
            }

            if (!childrenByParent.TryGetValue(parentId, out List<Guid>? children))
            {
                children = [];
                childrenByParent.Add(parentId, children);
            }

            children.Add(folder.Id);
        }

        HashSet<Guid> result = [rootId];
        Stack<Guid> pending = new();
        pending.Push(rootId);
        while (pending.TryPop(out Guid parentId))
        {
            if (!childrenByParent.TryGetValue(parentId, out List<Guid>? children))
            {
                continue;
            }

            foreach (Guid childId in children)
            {
                if (result.Add(childId))
                {
                    pending.Push(childId);
                }
            }
        }

        return result;
    }

    private static Dictionary<Guid, string> BuildFolderPaths(
        IReadOnlyDictionary<Guid, VaultFolder> folders)
    {
        Dictionary<Guid, string> paths = [];
        foreach (Guid folderId in folders.Keys)
        {
            if (paths.ContainsKey(folderId))
            {
                continue;
            }

            List<VaultFolder> unresolved = [];
            HashSet<Guid> visited = [];
            Guid currentId = folderId;
            while (visited.Add(currentId) &&
                !paths.ContainsKey(currentId) &&
                folders.TryGetValue(currentId, out VaultFolder? current))
            {
                unresolved.Add(current);
                if (current.ParentId is not Guid parentId)
                {
                    break;
                }

                currentId = parentId;
            }

            string prefix = paths.GetValueOrDefault(currentId, string.Empty);
            for (int index = unresolved.Count - 1; index >= 0; index--)
            {
                VaultFolder folder = unresolved[index];
                prefix = prefix.Length == 0 ? folder.Name : prefix + " / " + folder.Name;
                paths[folder.Id] = prefix;
            }
        }

        return paths;
    }

    private static bool Contains(string value, string term) =>
        value.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static bool EqualsIgnoreCase(string value, string? other) =>
        string.Equals(value, other, StringComparison.OrdinalIgnoreCase);

    private sealed record IndexedResult(VaultEntryResult Result, int OriginalIndex);

    private sealed class ResultComparer(VaultSortField sortBy, bool descending)
        : IComparer<IndexedResult>
    {
        private static readonly StringComparer TextComparer = StringComparer.OrdinalIgnoreCase;

        public int Compare(IndexedResult? left, IndexedResult? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left is null)
            {
                return -1;
            }

            if (right is null)
            {
                return 1;
            }

            VaultEntryResult x = left.Result;
            VaultEntryResult y = right.Result;
            int comparison = sortBy switch
            {
                VaultSortField.Username => TextComparer.Compare(x.Entry.Username, y.Entry.Username),
                VaultSortField.Url => TextComparer.Compare(x.Entry.Url, y.Entry.Url),
                VaultSortField.Folder => TextComparer.Compare(x.FolderPath, y.FolderPath),
                VaultSortField.CreatedUtc => x.Entry.CreatedUtc.CompareTo(y.Entry.CreatedUtc),
                VaultSortField.UpdatedUtc => x.Entry.UpdatedUtc.CompareTo(y.Entry.UpdatedUtc),
                _ => TextComparer.Compare(x.Entry.Title, y.Entry.Title),
            };

            if (comparison != 0)
            {
                return descending ? -comparison : comparison;
            }

            return left.OriginalIndex.CompareTo(right.OriginalIndex);
        }
    }
}
