using Maen.Accounting.Core.Models;

namespace Maen.Accounting.Core.Services;

/// <summary>
/// Maintains a registry of user-defined categories with colors and usage counts,
/// computed purely from existing entries — no UI or persistence dependency.
/// </summary>
public static class CategoryRegistryCalculator
{
    public static IReadOnlyList<CategoryRegistryItem> ComputeRegistry(
        IEnumerable<CategoryEntry> entries,
        IReadOnlyCollection<string> defaults)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(defaults);

        var defaultsList = defaults
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select((name, index) => new CategoryRegistryItem(
                Category: name.Trim(),
                ColorHex: DefaultColors[index % DefaultColors.Length],
                EntriesCount: 0,
                LastUsedDate: null))
            .ToList();

        var lookup = new Dictionary<string, CategoryRegistryItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in defaultsList)
        {
            lookup[item.Category] = item;
        }

        foreach (var entry in entries.Where(static item => !string.IsNullOrWhiteSpace(item.Category)))
        {
            var name = entry.Category.Trim();
            if (!lookup.TryGetValue(name, out var item))
            {
                item = new CategoryRegistryItem(
                    Category: name,
                    ColorHex: DefaultColors[(defaultsList.Count + lookup.Count) % DefaultColors.Length],
                    EntriesCount: 0,
                    LastUsedDate: entry.EntryDate);
                lookup[name] = item;
                defaultsList.Add(item);
                continue;
            }

            item = item with { EntriesCount = checked(item.EntriesCount + 1) };
            if (item.LastUsedDate == null || entry.EntryDate > item.LastUsedDate)
            {
                item = item with { LastUsedDate = entry.EntryDate };
            }

            lookup[name] = item;
            defaultsList[defaultsList.FindIndex(existing =>
                string.Equals(existing.Category, name, StringComparison.OrdinalIgnoreCase))] = item;
        }

        return defaultsList
            .OrderByDescending(item => item.EntriesCount)
            .ThenBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static readonly string[] DefaultColors =
    [
        "#C8A45D", "#5B8DB8", "#8E6FB0", "#4C9B74", "#C0664E",
        "#3E7C8D", "#B8860B", "#6A8F4E", "#A95A72", "#556B7E"
    ];
}

public sealed record CategoryEntry(string Category, DateOnly EntryDate);

public sealed record CategoryRegistryItem(
    string Category,
    string ColorHex,
    int EntriesCount,
    DateOnly? LastUsedDate);
