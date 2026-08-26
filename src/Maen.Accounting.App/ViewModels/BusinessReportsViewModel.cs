using System.Collections.ObjectModel;
using Maen.Accounting.App.Data;
using Maen.Accounting.App.Infrastructure;
using Maen.Accounting.Core.Models;
using Maen.Accounting.Core.Services;

namespace Maen.Accounting.App.ViewModels;

public sealed class MonthlySliceViewModel(MonthlySlice slice, MonthlyComparison? comparison)
{
    public string MonthText => slice.Month.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
    public string SalesText => Money.Format(slice.SalesMinor);
    public string PurchasesText => Money.Format(slice.PurchasesMinor);
    public string ReceiptsText => Money.Format(slice.ReceiptsMinor);
    public string SupplierPaymentsText => Money.Format(slice.SupplierPaymentsMinor);
    public string NetCashText => Money.Format(slice.NetCashMinor);
    public string NetCashColor => slice.NetCashMinor >= 0 ? "#137A53" : "#C2413A";
    public MonthlySlice Model => slice;

    private bool HasMoM => comparison is { PreviousSalesMinor: not null };
    public bool HasDelta => HasMoM;
    public string DeltaText => comparison is { PreviousSalesMinor: not null } current
        ? FormatDeltaText(current.SalesDeltaMinor, current.SalesDeltaPercent)
        : string.Empty;
    public string DeltaColor => comparison is not { PreviousSalesMinor: not null } current
        ? "#64748B"
        : (current.SalesDeltaMinor ?? 0) >= 0 ? "#137A53" : "#C2413A";
    public string DeltaArrow => comparison is not { PreviousSalesMinor: not null } current
        ? string.Empty
        : (current.SalesDeltaMinor ?? 0) >= 0 ? "▲" : "▼";
    public string YoYText => comparison is not null && comparison.YearAgoSalesMinor is not null
        ? FormatDeltaText(comparison.SalesYoYDeltaMinor, comparison.SalesYoYDeltaPercent)
        : UiText.Get("T625");
    public string YoYColor => comparison is not null && comparison.YearAgoSalesMinor is not null
        ? (comparison.SalesYoYDeltaPercent >= 0 ? "#137A53" : "#C2413A")
        : "#64748B";

    private static string FormatDeltaText(long? deltaMinor, double percent)
    {
        if (deltaMinor is null)
        {
            return UiText.Get("T625");
        }

        var sign = deltaMinor.Value >= 0 ? $"+{Money.Format(deltaMinor.Value)}" : Money.Format(deltaMinor.Value);
        return $"{sign} ({percent:+0.#;-0.#}%)";
    }
}

public sealed class BusinessReportsViewModel : ObservableObject
{
    private readonly BusinessRepository _repository;
    private string _statusMessage = string.Empty;
    private AuthSession? _session;
    private bool _isBusy;
    private readonly SemaphoreSlim _loadGate = new(1, 1);

    public BusinessReportsViewModel(BusinessRepository repository)
    {
        _repository = repository;
        var today = DateOnly.FromDateTime(DateTime.Today);
        FromDate = new DateTime(today.Year, today.Month, 1);
        ToDate = DateTime.Today;
    }

    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string BestMonthText { get; private set; } = UiText.Get("T080");
    public string WorstMonthText { get; private set; } = UiText.Get("T080");
    public string TotalSalesText { get; private set; } = Money.Format(0);
    public string TotalPurchasesText { get; private set; } = Money.Format(0);
    public string TotalReceiptsText { get; private set; } = Money.Format(0);
    public string TotalSupplierPaymentsText { get; private set; } = Money.Format(0);

    public string ReceivablesAgingTotalText { get; private set; } = Money.Format(0);
    public string ReceivablesAgingCurrentText { get; private set; } = Money.Format(0);
    public string ReceivablesAgingDaysOneToThirtyText { get; private set; } = Money.Format(0);
    public string ReceivablesAgingDaysThirtyOneToSixtyText { get; private set; } = Money.Format(0);
    public string ReceivablesAgingDaysSixtyOneToNinetyText { get; private set; } = Money.Format(0);
    public string ReceivablesAgingDaysOverNinetyText { get; private set; } = Money.Format(0);
    public string PayablesAgingTotalText { get; private set; } = Money.Format(0);
    public bool HasAgingExposure { get; private set; }

    public bool HasComparisons { get; private set; }
    public bool HasYoYComparisons { get; private set; }
    public string ComparisonSalesText { get; private set; } = string.Empty;
    public string ComparisonSalesColor { get; private set; } = "#64748B";
    public string ComparisonPurchasesText { get; private set; } = string.Empty;
    public string ComparisonPurchasesColor { get; private set; } = "#64748B";
    public string ComparisonNetCashText { get; private set; } = string.Empty;
    public string ComparisonNetCashColor { get; private set; } = "#64748B";
    public string ComparisonMoMText { get; private set; } = string.Empty;
    public string ComparisonMoMColor { get; private set; } = "#64748B";
    public string ComparisonYoYText { get; private set; } = string.Empty;
    public string ComparisonYoYColor { get; private set; } = "#64748B";

    public ObservableCollection<MonthlySliceViewModel> MonthlySlices { get; } = new();

    public async Task InitializeAsync(AuthSession session)
    {
        _session = session;
        await LoadAsync();
    }

    public async Task LoadAsync()
    {
        await _loadGate.WaitAsync();
        try
        {
            if (_session is null)
            {
                return;
            }

            IsBusy = true;
            var userId = _session.UserId;
            if (string.IsNullOrWhiteSpace(userId))
            {
                StatusMessage = UiText.Get("T080");
                return;
            }
            var invoices = await _repository.GetInvoicesAsync(userId);
            var payments = await _repository.GetPaymentsAsync(userId);
            var fromMonth = DateOnly.FromDateTime(FromDate);
            var toMonth = DateOnly.FromDateTime(ToDate);
            var report = BusinessMonthlyReportCalculator.Build(invoices, payments, fromMonth, toMonth);

            BestMonthText = report.BestMonth.Month.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
            WorstMonthText = report.WorstMonth.Month.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
            TotalSalesText = Money.Format(report.TotalSalesMinor);
            TotalPurchasesText = Money.Format(report.TotalPurchasesMinor);
            TotalReceiptsText = Money.Format(report.TotalReceiptsMinor);
            TotalSupplierPaymentsText = Money.Format(report.TotalSupplierPaymentsMinor);

            var months = report.Months.ToArray();
            var comparisons = BusinessComparisonCalculator.Compare(months);
            var comparisonBySlice = comparisons.ToDictionary(item => item.Slice, item => item);
            RebuildComparisons(comparisons);
            MonthlySlices.Clear();
            foreach (var slice in months)
            {
                comparisonBySlice.TryGetValue(slice, out var item);
                MonthlySlices.Add(new MonthlySliceViewModel(slice, item));
            }

            var aging = DebtAgingCalculator.Age(invoices, payments, DateOnly.FromDateTime(DateTime.Today));
            ReceivablesAgingTotalText = Money.Format(aging.Receivables.TotalMinor);
            ReceivablesAgingCurrentText = Money.Format(aging.Receivables.CurrentMinor);
            ReceivablesAgingDaysOneToThirtyText = Money.Format(aging.Receivables.DaysOneToThirtyMinor);
            ReceivablesAgingDaysThirtyOneToSixtyText = Money.Format(aging.Receivables.DaysThirtyOneToSixtyMinor);
            ReceivablesAgingDaysSixtyOneToNinetyText = Money.Format(aging.Receivables.DaysSixtyOneToNinetyMinor);
            ReceivablesAgingDaysOverNinetyText = Money.Format(aging.Receivables.DaysOverNinetyMinor);
            PayablesAgingTotalText = Money.Format(aging.Payables.TotalMinor);
            HasAgingExposure = aging.Receivables.TotalMinor > 0 || aging.Payables.TotalMinor > 0;

            StatusMessage = UiText.Get("T538");
        }
        catch
        {
            StatusMessage = UiText.Get("T121");
            throw;
        }
        finally
        {
            IsBusy = false;
            _loadGate.Release();
        }
    }

    private void RebuildComparisons(IReadOnlyList<MonthlyComparison> comparisons)
    {
        var latest = comparisons.Count == 0 ? null : comparisons[^1];
        if (latest is null || latest.PreviousSalesMinor is null)
        {
            HasComparisons = false;
            HasYoYComparisons = false;
            return;
        }

        HasComparisons = true;
        HasYoYComparisons = latest.YearAgoSalesMinor is not null;

        ComparisonSalesText = FormatComparisonText(latest.SalesDeltaMinor, latest.SalesDeltaPercent, UiText.Get("T655"), UiText.Get("T656"));
        ComparisonSalesColor = latest.SalesDeltaMinor >= 0 ? "#137A53" : "#C2413A";
        ComparisonPurchasesText = UiText.Get("T625");
        ComparisonPurchasesColor = "#64748B";
        ComparisonNetCashText = FormatComparisonText(latest.NetCashDeltaMinor, latest.NetCashDeltaPercent, UiText.Get("T655"), UiText.Get("T656"));
        ComparisonNetCashColor = latest.NetCashDeltaMinor >= 0 ? "#137A53" : "#C2413A";
        ComparisonMoMText = UiText.Format("T536") + $" {(latest.SalesDeltaPercent >= 0 ? "+" : "")}{latest.SalesDeltaPercent:0.#}%";
        ComparisonMoMColor = latest.SalesDeltaPercent >= 0 ? "#2F7DB8" : "#C2413A";
        ComparisonYoYText = latest.YearAgoSalesMinor is null
            ? UiText.Get("T625")
            : $"{(latest.SalesYoYDeltaPercent >= 0 ? "+" : "")}{latest.SalesYoYDeltaPercent:0.#}%";
        ComparisonYoYColor = latest.SalesYoYDeltaPercent >= 0 ? "#6B3BB8" : "#C2413A";
    }

    private static string FormatComparisonText(long? delta, double percent, string upWord, string downWord)
    {
        if (delta is null)
        {
            return UiText.Get("T625");
        }

        var sign = delta.Value >= 0 ? $"+{Money.Format(delta.Value)}" : Money.Format(delta.Value);
        return $"{sign} ({percent:+0.#;-0.#}%) {(delta.Value >= 0 ? upWord : downWord)}";
    }

    public string ExportCsv()
    {
        var slices = MonthlySlices.Select(item => item.Model).ToList();
        if (slices.Count == 0)
        {
            StatusMessage = UiText.Get("T080");
            return string.Empty;
        }
        var csv = BusinessReportCsvExporter.Build(slices);
        StatusMessage = UiText.Get("T538");
        return csv;
    }
}
