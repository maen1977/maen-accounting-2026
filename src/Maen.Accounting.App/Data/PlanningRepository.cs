using System.Text.Json;
using Maen.Accounting.App.Services;
using Maen.Accounting.Core.Services;

namespace Maen.Accounting.App.Data;

/// <summary>
/// Persistence for personal planning documents: financial plans, obligations, and deposits.
/// Follows the same repository conventions as BusinessRepository.
/// </summary>
public sealed class PlanningRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly UserDatabaseFactory _databaseFactory;
    private readonly AppPreferencesService _preferences;

    public PlanningRepository(
        UserDatabaseFactory databaseFactory,
        AppPreferencesService preferences)
    {
        _databaseFactory = databaseFactory;
        _preferences = preferences;
    }

    public async Task<IReadOnlyList<FinancialPlanRow>> GetPlansAsync(string userId)
    {
        var database = await _databaseFactory.GetAsync(userId, _preferences.StorageScope);
        var rows = await database.Table<FinancialPlanRow>()
            .Where(row => row.UserId == userId && !row.IsDeleted)
            .ToListAsync();
        return rows
            .OrderByDescending(row => row.UpdatedAtUtcTicks)
            .ToArray();
    }

    public async Task UpsertPlanAsync(string userId, FinancialPlanRow row)
    {
        UserIsolation.EnsureOwner(userId, row.UserId);
        var database = await _databaseFactory.GetAsync(userId, _preferences.StorageScope);
        await database.InsertOrReplaceAsync(row);
    }

    public async Task UpsertPlansAsync(string userId, IEnumerable<FinancialPlanRow> rows)
    {
        var materialized = rows.ToArray();
        foreach (var row in materialized)
        {
            UserIsolation.EnsureOwner(userId, row.UserId);
        }

        var database = await _databaseFactory.GetAsync(userId, _preferences.StorageScope);
        await database.RunInTransactionAsync(connection =>
        {
            foreach (var row in materialized)
            {
                connection.InsertOrReplace(row);
            }
        });
    }

    public async Task<IReadOnlyList<ObligationRow>> GetObligationsAsync(string userId)
    {
        var database = await _databaseFactory.GetAsync(userId, _preferences.StorageScope);
        var rows = await database.Table<ObligationRow>()
            .Where(row => row.UserId == userId && !row.IsDeleted)
            .ToListAsync();
        return rows
            .OrderBy(static row => row.StartDateTicks)
            .ThenBy(row => row.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task UpsertObligationAsync(string userId, ObligationRow row)
    {
        UserIsolation.EnsureOwner(userId, row.UserId);
        var database = await _databaseFactory.GetAsync(userId, _preferences.StorageScope);
        await database.InsertOrReplaceAsync(row);
    }

    public async Task UpsertObligationsAsync(string userId, IEnumerable<ObligationRow> rows)
    {
        var materialized = rows.ToArray();
        foreach (var row in materialized)
        {
            UserIsolation.EnsureOwner(userId, row.UserId);
        }

        var database = await _databaseFactory.GetAsync(userId, _preferences.StorageScope);
        await database.RunInTransactionAsync(connection =>
        {
            foreach (var row in materialized)
            {
                connection.InsertOrReplace(row);
            }
        });
    }

    public async Task<IReadOnlyList<DepositRow>> GetDepositsAsync(string userId)
    {
        var database = await _databaseFactory.GetAsync(userId, _preferences.StorageScope);
        var rows = await database.Table<DepositRow>()
            .Where(row => row.UserId == userId && !row.IsDeleted)
            .ToListAsync();
        return rows
            .OrderByDescending(row => row.DepositDateTicks)
            .ToArray();
    }

    public async Task UpsertDepositAsync(string userId, DepositRow row)
    {
        UserIsolation.EnsureOwner(userId, row.UserId);
        var database = await _databaseFactory.GetAsync(userId, _preferences.StorageScope);
        await database.InsertOrReplaceAsync(row);
    }

    public async Task UpsertDepositsAsync(string userId, IEnumerable<DepositRow> rows)
    {
        var materialized = rows.ToArray();
        foreach (var row in materialized)
        {
            UserIsolation.EnsureOwner(userId, row.UserId);
        }

        var database = await _databaseFactory.GetAsync(userId, _preferences.StorageScope);
        await database.RunInTransactionAsync(connection =>
        {
            foreach (var row in materialized)
            {
                connection.InsertOrReplace(row);
            }
        });
    }

    public static List<PlanCategoryLimit> ParseCategoryLimits(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<PlanCategoryLimit>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string SerializeCategoryLimits(IReadOnlyList<PlanCategoryLimit> limits) =>
        JsonSerializer.Serialize(limits ?? [], JsonOptions);

    public static List<DateOnly> ParsePaidOccurrences(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<DateOnly>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string SerializePaidOccurrences(IReadOnlyList<DateOnly> occurrences) =>
        JsonSerializer.Serialize(occurrences ?? [], JsonOptions);
}
