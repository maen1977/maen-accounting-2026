using System.Collections.ObjectModel;
using Maen.Accounting.App.Data;
using Maen.Accounting.App.Infrastructure;
using Maen.Accounting.App.Services;
using Maen.Accounting.Core.Models;
using Maen.Accounting.Core.Services;

namespace Maen.Accounting.App.ViewModels;

public sealed class MainStateViewModel : ObservableObject
{
    private readonly ProfitEntryRepository _repository;
    private readonly BackupService _backupService;
    private readonly FirestoreSyncService _syncService;
    private readonly DeviceIdentityService _deviceIdentity;
    private readonly AuthSessionStore _sessionStore;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private AuthSession? _session;
    private ProfitEntry? _editingEntry;
    private DateTime _entryDate = DateTime.Today;
    private string _salesInput = string.Empty;
    private string _costInput = string.Empty;
    private string _expensesInput = "0";
    private string _notesInput = string.Empty;
    private DateTime _reportMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private string _searchText = string.Empty;
    private bool _isBusy;
    private string _statusMessage = string.Empty;
    private string _syncStatus = "لم تتم المزامنة بعد";
    private string _backupStatus = "لا توجد نسخة محلية بعد";

    public MainStateViewModel(
        ProfitEntryRepository repository,
        BackupService backupService,
        FirestoreSyncService syncService,
        DeviceIdentityService deviceIdentity,
        AuthSessionStore sessionStore)
    {
        _repository = repository;
        _backupService = backupService;
        _syncService = syncService;
        _deviceIdentity = deviceIdentity;
        _sessionStore = sessionStore;
    }

    public ObservableCollection<ProfitEntryItemViewModel> Entries { get; } = [];
    public ObservableCollection<ProfitEntryItemViewModel> RecentEntries { get; } = [];
    public ObservableCollection<ProfitEntryItemViewModel> ReportEntries { get; } = [];

    public event EventHandler? EntryEditorRequested;
    public event EventHandler? SignedOut;

    public string UserEmail => _session?.Email ?? string.Empty;
    public bool IsCloudAccount => _session is { IsLocal: false };
    public string AccountModeText => IsCloudAccount ? "حساب سحابي محمي" : "وضع محلي على هذا الجهاز";

    public DateTime EntryDate { get => _entryDate; set => SetProperty(ref _entryDate, value); }
    public string SalesInput { get => _salesInput; set { if (SetProperty(ref _salesInput, value)) RefreshPreview(); } }
    public string CostInput { get => _costInput; set { if (SetProperty(ref _costInput, value)) RefreshPreview(); } }
    public string ExpensesInput { get => _expensesInput; set { if (SetProperty(ref _expensesInput, value)) RefreshPreview(); } }
    public string NotesInput { get => _notesInput; set => SetProperty(ref _notesInput, value); }
    public DateTime ReportMonth { get => _reportMonth; set { if (SetProperty(ref _reportMonth, new DateTime(value.Year, value.Month, 1))) RebuildReport(); } }
    public string SearchText { get => _searchText; set { if (SetProperty(ref _searchText, value)) RebuildReport(); } }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string SyncStatus { get => _syncStatus; private set => SetProperty(ref _syncStatus, value); }
    public string BackupStatus { get => _backupStatus; private set => SetProperty(ref _backupStatus, value); }
    public string SaveButtonText => _editingEntry is null ? "حفظ السجل" : "تحديث السجل";

    public string PreviewNetText
    {
        get
        {
            Money.TryParse(SalesInput, out var sales);
            Money.TryParse(CostInput, out var cost);
            Money.TryParse(ExpensesInput, out var expenses);
            return Money.Format(checked(sales - cost - expenses));
        }
    }

    public string CurrentMonthNetText => Money.Format(SummarizeCurrentMonth().NetProfitMinor);
    public string CurrentMonthSalesText => Money.Format(SummarizeCurrentMonth().SalesMinor);
    public string CurrentYearNetText => Money.Format(SummarizeCurrentYear().NetProfitMinor);
    public string TotalExpensesText => Money.Format(ProfitCalculator.Summarize(Models()).ExpensesMinor);
    public string ReportNetText => Money.Format(SummarizeReport().NetProfitMinor);
    public string ReportSalesText => Money.Format(SummarizeReport().SalesMinor);
    public string ReportExpensesText => Money.Format(SummarizeReport().ExpensesMinor);
    public string ReportCountText => SummarizeReport().EntriesCount.ToString();

    public async Task InitializeAsync(AuthSession session)
    {
        _session = session;
        OnPropertyChanged(nameof(UserEmail));
        OnPropertyChanged(nameof(IsCloudAccount));
        OnPropertyChanged(nameof(AccountModeText));
        await ReloadAsync();
        var info = await _backupService.ReadInfoAsync(session.UserId);
        SetBackupStatus(info);
    }

    public async Task ReloadAsync()
    {
        var session = RequireSession();
        var entries = await _repository.GetVisibleAsync(session.UserId);
        Entries.Clear();
        foreach (var entry in entries)
        {
            Entries.Add(new ProfitEntryItemViewModel(entry));
        }

        RebuildRecent();
        RebuildReport();
        RaiseSummaries();
    }

    public void BeginNewEntry()
    {
        _editingEntry = null;
        EntryDate = DateTime.Today;
        SalesInput = string.Empty;
        CostInput = string.Empty;
        ExpensesInput = "0";
        NotesInput = string.Empty;
        OnPropertyChanged(nameof(SaveButtonText));
        EntryEditorRequested?.Invoke(this, EventArgs.Empty);
    }

    public void BeginEdit(ProfitEntryItemViewModel item)
    {
        _editingEntry = item.Model;
        EntryDate = item.Model.EntryDate.ToDateTime(TimeOnly.MinValue);
        SalesInput = Money.ToDecimal(item.Model.SalesMinor).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        CostInput = Money.ToDecimal(item.Model.CostMinor).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        ExpensesInput = Money.ToDecimal(item.Model.ExpensesMinor).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        NotesInput = item.Model.Notes;
        OnPropertyChanged(nameof(SaveButtonText));
        EntryEditorRequested?.Invoke(this, EventArgs.Empty);
    }

    public async Task SaveCurrentAsync()
    {
        var session = RequireSession();
        if (!Money.TryParse(SalesInput, out var sales) ||
            !Money.TryParse(CostInput, out var cost) ||
            !Money.TryParse(ExpensesInput, out var expenses))
        {
            throw new InvalidOperationException("أدخل أرقامًا مالية صحيحة وغير سالبة.");
        }

        await RunBusyAsync(async () =>
        {
            var date = DateOnly.FromDateTime(EntryDate);
            var existingByDate = await _repository.FindByDateAsync(session.UserId, date);
            var source = _editingEntry ?? existingByDate;
            var now = DateTimeOffset.UtcNow;
            var entry = new ProfitEntry(
                source?.EntryId ?? Guid.NewGuid().ToString("N"),
                session.UserId,
                date,
                sales,
                cost,
                expenses,
                NotesInput.Trim(),
                false,
                source?.CreatedAtUtc ?? now,
                now,
                checked((source?.Version ?? 0) + 1),
                _deviceIdentity.GetOrCreate());

            await _repository.UpsertAsync(session.UserId, entry);
            await WriteBackupAndUpdateAsync();
            await ReloadAsync();
            ResetEditor();
            StatusMessage = "تم حفظ السجل محليًا بدقة.";

            if (!session.IsLocal)
            {
                try
                {
                    await SyncInternalAsync();
                }
                catch
                {
                    StatusMessage = "تم الحفظ محليًا، وتعذرت المزامنة حاليًا.";
                }
            }
        });
    }

    public async Task DeleteAsync(ProfitEntryItemViewModel item)
    {
        var session = RequireSession();
        await RunBusyAsync(async () =>
        {
            var deleted = item.Model.MarkDeleted(DateTimeOffset.UtcNow, _deviceIdentity.GetOrCreate());
            await _repository.UpsertAsync(session.UserId, deleted);
            await WriteBackupAndUpdateAsync();
            await ReloadAsync();
            StatusMessage = "تم حذف السجل محليًا.";
            if (!session.IsLocal)
            {
                try { await SyncInternalAsync(); } catch { StatusMessage = "تم الحذف محليًا، وتعذرت المزامنة حاليًا."; }
            }
        });
    }

    public async Task SyncAsync()
    {
        if (!IsCloudAccount)
        {
            throw new InvalidOperationException("المزامنة متاحة للحساب السحابي فقط.");
        }

        await RunBusyAsync(async () =>
        {
            await SyncInternalAsync();
            StatusMessage = "اكتملت المزامنة دون استبدال البيانات الأحدث.";
        });
    }

    public async Task<string> ExportAsync()
    {
        var session = RequireSession();
        var path = await _backupService.CreateExportCopyAsync(session);
        SetBackupStatus(await _backupService.ReadInfoAsync(session.UserId));
        return path;
    }

    public async Task<int> ImportAsync(string filePath)
    {
        var session = RequireSession();
        return await RunBusyAsync(async () =>
        {
            var count = await _backupService.ImportAsync(session, filePath);
            SetBackupStatus(await _backupService.ReadInfoAsync(session.UserId));
            await ReloadAsync();
            StatusMessage = $"تم استيراد {count} سجلًا.";
            return count;
        });
    }

    public async Task SignOutAsync()
    {
        await _sessionStore.ClearAsync();
        _session = null;
        Entries.Clear();
        RecentEntries.Clear();
        ReportEntries.Clear();
        SignedOut?.Invoke(this, EventArgs.Empty);
    }

    private async Task SyncInternalAsync()
    {
        var result = await _syncService.SyncAsync();
        SyncStatus = $"آخر مزامنة: {result.CompletedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm} — رفع {result.Uploaded}";
        await ReloadAsync();
        await WriteBackupAndUpdateAsync();
    }

    private async Task WriteBackupAndUpdateAsync()
    {
        var info = await _backupService.WriteAsync(RequireSession());
        SetBackupStatus(info);
    }

    private void SetBackupStatus(BackupInfo info)
    {
        BackupStatus = info.Exists
            ? $"آخر نسخة: {info.UpdatedAtUtc?.ToLocalTime():yyyy-MM-dd HH:mm} — {info.EntriesCount} سجل"
            : "لا توجد نسخة محلية بعد";
    }

    private void ResetEditor()
    {
        _editingEntry = null;
        EntryDate = DateTime.Today;
        SalesInput = string.Empty;
        CostInput = string.Empty;
        ExpensesInput = "0";
        NotesInput = string.Empty;
        OnPropertyChanged(nameof(SaveButtonText));
    }

    private void RebuildRecent()
    {
        RecentEntries.Clear();
        foreach (var entry in Entries.Take(5))
        {
            RecentEntries.Add(entry);
        }
    }

    private void RebuildReport()
    {
        ReportEntries.Clear();
        var query = SearchText.Trim();
        foreach (var item in Entries.Where(item =>
                     item.Model.EntryDate.Year == ReportMonth.Year &&
                     item.Model.EntryDate.Month == ReportMonth.Month &&
                     (query.Length == 0 ||
                      item.DateText.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                      item.Model.Notes.Contains(query, StringComparison.OrdinalIgnoreCase))))
        {
            ReportEntries.Add(item);
        }
        RaiseReportSummary();
    }

    private IEnumerable<ProfitEntry> Models() => Entries.Select(static item => item.Model);
    private FinancialSummary SummarizeCurrentMonth()
    {
        var now = DateTime.Today;
        return ProfitCalculator.Summarize(Models().Where(entry => entry.EntryDate.Year == now.Year && entry.EntryDate.Month == now.Month));
    }
    private FinancialSummary SummarizeCurrentYear()
    {
        var now = DateTime.Today;
        return ProfitCalculator.Summarize(Models().Where(entry => entry.EntryDate.Year == now.Year));
    }
    private FinancialSummary SummarizeReport() => ProfitCalculator.Summarize(ReportEntries.Select(static item => item.Model));

    private void RaiseSummaries()
    {
        OnPropertyChanged(nameof(CurrentMonthNetText));
        OnPropertyChanged(nameof(CurrentMonthSalesText));
        OnPropertyChanged(nameof(CurrentYearNetText));
        OnPropertyChanged(nameof(TotalExpensesText));
        RaiseReportSummary();
    }

    private void RaiseReportSummary()
    {
        OnPropertyChanged(nameof(ReportNetText));
        OnPropertyChanged(nameof(ReportSalesText));
        OnPropertyChanged(nameof(ReportExpensesText));
        OnPropertyChanged(nameof(ReportCountText));
    }

    private void RefreshPreview() => OnPropertyChanged(nameof(PreviewNetText));

    private AuthSession RequireSession() =>
        _session ?? throw new InvalidOperationException("لا توجد جلسة مستخدم نشطة.");

    private async Task RunBusyAsync(Func<Task> action)
    {
        await _gate.WaitAsync();
        try
        {
            IsBusy = true;
            await action();
        }
        finally
        {
            IsBusy = false;
            _gate.Release();
        }
    }

    private async Task<T> RunBusyAsync<T>(Func<Task<T>> action)
    {
        await _gate.WaitAsync();
        try
        {
            IsBusy = true;
            return await action();
        }
        finally
        {
            IsBusy = false;
            _gate.Release();
        }
    }
}
