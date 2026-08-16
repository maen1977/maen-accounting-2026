using System.Collections.ObjectModel;
using Maen.Accounting.App.Data;
using Maen.Accounting.App.Infrastructure;
using Maen.Accounting.Core.Models;
using Maen.Accounting.Core.Services;

namespace Maen.Accounting.App.ViewModels;

public sealed class MonthlySliceViewModel(MonthlySlice slice)
{
    public string MonthText => slice.Month.ToString("yyyy-MM");
    public string SalesText => Money.Format(slice.SalesMinor);
    public string PurchasesText => Money.Format(slice.PurchasesMinor);
    public string ReceiptsText => Money.Format(slice.ReceiptsMinor);
    public string SupplierPaymentsText => Money.Format(slice.SupplierPaymentsMinor);
    public string NetCashText => Money.Format(slice.NetCashMinor);
    public string NetCashColor => slice.NetCashMinor >= 0 ? "#137A53" : "#C2413A";
    public MonthlySlice Model => slice;
}

public sealed class BusinessReportsViewModel : ObservableObject
{
    private readonly BusinessRepository _repository;
    private string _statusMessage = string.Empty;
    private AuthSession? _session;
    private bool _isBusy;

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

    public ObservableCollection<MonthlySliceViewModel> MonthlySlices { get; } = new();

    public async Task InitializeAsync(AuthSession session)
    {
        _session = session;
        await LoadAsync();
    }

    public async Task LoadAsync()
    {
        if (_session is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
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

            BestMonthText = report.BestMonth.Month.ToString("yyyy-MM");
            WorstMonthText = report.WorstMonth.Month.ToString("yyyy-MM");
            TotalSalesText = Money.Format(report.TotalSalesMinor);
            TotalPurchasesText = Money.Format(report.TotalPurchasesMinor);
            TotalReceiptsText = Money.Format(report.TotalReceiptsMinor);
            TotalSupplierPaymentsText = Money.Format(report.TotalSupplierPaymentsMinor);

            MonthlySlices.Clear();
            foreach (var slice in report.Months)
            {
                MonthlySlices.Add(new MonthlySliceViewModel(slice));
            }

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
        }
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
