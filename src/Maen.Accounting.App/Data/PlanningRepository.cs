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
    private readonly DeviceIdentityService _deviceIdentity;
    public PlanningRepository(
        UserDatabaseFactory databaseFactory,
        AppPreferencesService preferences,
        DeviceIdentityService deviceIdentity)
    {
        _databaseFactory = databaseFactory;
        _preferences = preferences;
        _deviceIdentity = deviceIdentity;
    }

    public async Task<IReadOnlyList<FinancialPlanRow>> GetPlansAsync(string userId)
    {
        var database = await _databaseFactory.GetAsync(userId, _preferences.StorageScope);
        var rows = await database.Table<FinancialPlanRow>()
            .Where(row => row.UserId == userId && !row.IsDeleted)
            .ToListAsync();
        return rows
            .Select(static row => Normalize(row))
            .OrderByDescending(row => row.UpdatedAtUtcTicks)
            .ToArray();
    }

    public async Task UpsertPlanAsync(string userId, FinancialPlanRow row)
    {
        Normalize(row);
        UserIsolation.EnsureOwner(userId, row.UserId);
        var database = await _databaseFactory.GetAsync(userId, _preferences.StorageScope);
        await database.InsertOrReplaceAsync(row);
    }

    public async Task UpsertPlansAsync(string userId, IEnumerable<FinancialPlanRow> rows)
    {
        var materialized = rows.Select(static row => Normalize(row)).ToArray();
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
            .Select(static row => Normalize(row))
            .OrderBy(static row => row.StartDateTicks)
            .ThenBy(row => row.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task UpsertObligationAsync(string userId, ObligationRow row)
    {
        Normalize(row);
        UserIsolation.EnsureOwner(userId, row.UserId);
        var database = await _databaseFactory.GetAsync(userId, _preferences.StorageScope);
        await database.InsertOrReplaceAsync(row);
    }

    public async Task UpsertObligationsAsync(string userId, IEnumerable<ObligationRow> rows)
    {
        var materialized = rows.Select(static row => Normalize(row)).ToArray();
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
            .Select(static row => Normalize(row))
            .OrderByDescending(row => row.DepositDateTicks)
            .ToArray();
    }

    public async Task UpsertDepositAsync(string userId, DepositRow row)
    {
        Normalize(row);
        UserIsolation.EnsureOwner(userId, row.UserId);
        var database = await _databaseFactory.GetAsync(userId, _preferences.StorageScope);
        await database.InsertOrReplaceAsync(row);
    }

    public async Task UpsertDepositsAsync(string userId, IEnumerable<DepositRow> rows)
    {
        var materialized = rows.Select(static row => Normalize(row)).ToArray();
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

    public async Task<IReadOnlyList<SavingsGoalRow>> GetGoalsAsync(string userId)
    {
        var database = await _databaseFactory.GetAsync(userId, _preferences.StorageScope);
        var rows = await database.Table<SavingsGoalRow>()
            .Where(row => row.UserId == userId && !row.IsDeleted)
            .ToListAsync();
        return rows
            .Select(static row => Normalize(row))
            .OrderByDescending(row => row.DeadlineTicks)
            .ThenBy(row => row.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task UpsertGoalAsync(string userId, SavingsGoalRow row)
    {
        Normalize(row);
        UserIsolation.EnsureOwner(userId, row.UserId);
        var database = await _databaseFactory.GetAsync(userId, _preferences.StorageScope);
        await database.InsertOrReplaceAsync(row);
    }

    public async Task UpsertGoalsAsync(string userId, IEnumerable<SavingsGoalRow> rows)
    {
        var materialized = rows.Select(static row => Normalize(row)).ToArray();
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

    public async Task<IReadOnlyList<RecurringMovementRow>> GetRecurringAsync(string userId)
    {
        var database = await _databaseFactory.GetAsync(userId, _preferences.StorageScope);
        var rows = await database.Table<RecurringMovementRow>()
            .Where(row => row.UserId == userId && !row.IsDeleted)
            .ToListAsync();
        return rows
            .Select(static row => Normalize(row))
            .OrderBy(row => row.NextOccurrenceTicks)
            .ThenBy(row => row.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task UpsertRecurringAsync(string userId, RecurringMovementRow row)
    {
        Normalize(row);
        UserIsolation.EnsureOwner(userId, row.UserId);
        var database = await _databaseFactory.GetAsync(userId, _preferences.StorageScope);
        await database.InsertOrReplaceAsync(row);
    }

    public async Task UpsertRecurringAsync(string userId, IEnumerable<RecurringMovementRow> rows)
    {
        var materialized = rows.Select(static row => Normalize(row)).ToArray();
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

    public async Task SoftDeleteRecurringAsync(string userId, string recurringId)
    {
        var database = await _databaseFactory.GetAsync(userId, _preferences.StorageScope);
        var rows = await database.Table<RecurringMovementRow>()
            .Where(row => row.RecurringId == recurringId && row.UserId == userId && !row.IsDeleted)
            .ToListAsync();
        foreach (var row in rows)
        {
            row.IsDeleted = true;
            row.UpdatedAtUtcTicks = DateTimeOffset.UtcNow.UtcDateTime.Ticks;
            row.Version = checked(row.Version + 1);
            row.DeviceId = _deviceIdentity.GetOrCreate();
        }

        await database.UpdateAllAsync(rows);
    }

    public async Task AdvanceOccurrenceAsync(string userId, string recurringId, DateOnly nextOccurrence)
    {
        var database = await _databaseFactory.GetAsync(userId, _preferences.StorageScope);
        var rows = await database.Table<RecurringMovementRow>()
            .Where(row => row.RecurringId == recurringId && row.UserId == userId && !row.IsDeleted)
            .ToListAsync();
        foreach (var row in rows)
        {
            row.NextOccurrenceTicks = nextOccurrence.ToDateTime(TimeOnly.MinValue).Ticks;
            row.UpdatedAtUtcTicks = DateTimeOffset.UtcNow.UtcDateTime.Ticks;
            row.Version = checked(row.Version + 1);
            row.DeviceId = _deviceIdentity.GetOrCreate();
        }

        await database.UpdateAllAsync(rows);
    }

    public async Task<IReadOnlyList<BudgetPlanRow>> GetBudgetPlansAsync(string userId)
    {
        var database = await _databaseFactory.GetAsync(userId, _preferences.StorageScope);
        var rows = await database.Table<BudgetPlanRow>()
            .Where(row => row.UserId == userId && !row.IsDeleted)
            .ToListAsync();
        return rows
            .Select(static row => Normalize(row))
            .OrderByDescending(row => row.YearMonth)
            .ToArray();
    }

    public async Task UpsertBudgetPlanAsync(string userId, BudgetPlanRow row)
    {
        Normalize(row);
        UserIsolation.EnsureOwner(userId, row.UserId);
        var database = await _databaseFactory.GetAsync(userId, _preferences.StorageScope);
        var database2 = database;
        var existing = await database2.Table<BudgetPlanRow>()
            .Where(row => row.YearMonth == row.YearMonth && row.UserId == userId && !row.IsDeleted)
            .ToListAsync();
        foreach (var prior in existing)
        {
            prior.IsActive = false;
            prior.IsDeleted = true;
            prior.UpdatedAtUtcTicks = DateTimeOffset.UtcNow.UtcDateTime.Ticks;
            prior.Version = checked(prior.Version + 1);
            prior.DeviceId = _deviceIdentity.GetOrCreate();
        }

        await database2.UpdateAllAsync(existing);
        await database2.InsertOrReplaceAsync(row);
    }

    private static FinancialPlanRow Normalize(FinancialPlanRow row)
    {
        row.PlanId ??= string.Empty;
        row.UserId ??= string.Empty;
        row.CategoryLimitsJson ??= "[]";
        row.DeviceId ??= string.Empty;
        return row;
    }

    private static ObligationRow Normalize(ObligationRow row)
    {
        row.ObligationId ??= string.Empty;
        row.UserId ??= string.Empty;
        row.Title ??= string.Empty;
        row.Category ??= string.Empty;
        row.Cycle = string.IsNullOrWhiteSpace(row.Cycle) ? "monthly" : row.Cycle;
        row.PaidOccurrencesJson ??= "[]";
        row.DeviceId ??= string.Empty;
        return row;
    }

    private static DepositRow Normalize(DepositRow row)
    {
        row.DepositId ??= string.Empty;
        row.UserId ??= string.Empty;
        row.Title ??= string.Empty;
        row.OwnerName ??= string.Empty;
        row.Kind = string.IsNullOrWhiteSpace(row.Kind) ? "deposit" : row.Kind;
        row.Notes ??= string.Empty;
        row.DeviceId ??= string.Empty;
        return row;
    }

    private static SavingsGoalRow Normalize(SavingsGoalRow row)
    {
        row.GoalId ??= string.Empty;
        row.UserId ??= string.Empty;
        row.Title ??= string.Empty;
        row.Category ??= string.Empty;
        row.DeviceId ??= string.Empty;
        return row;
    }

    private static RecurringMovementRow Normalize(RecurringMovementRow row)
    {
        row.RecurringId ??= string.Empty;
        row.UserId ??= string.Empty;
        row.Title ??= string.Empty;
        row.Category ??= string.Empty;
        row.Kind = string.IsNullOrWhiteSpace(row.Kind) ? "expense" : row.Kind;
        row.Cycle = string.IsNullOrWhiteSpace(row.Cycle) ? "monthly" : row.Cycle;
        row.Notes ??= string.Empty;
        row.DeviceId ??= string.Empty;
        return row;
    }

    private static BudgetPlanRow Normalize(BudgetPlanRow row)
    {
        row.PlanId ??= string.Empty;
        row.UserId ??= string.Empty;
        row.DeviceId ??= string.Empty;
        return row;
    }
}
