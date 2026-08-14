using Maen.Accounting.App.ViewModels;

namespace Maen.Accounting.App.Views;

public partial class AccountingPage : ContentPage
{
    private readonly AccountingViewModel _viewModel;

    public AccountingPage(AccountingViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    private async void OnRefreshClicked(object? sender, EventArgs e)
    {
        try
        {
            await _viewModel.ReloadAsync();
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync("تعذر تحديث المحاسبة", exception.Message, "حسنًا");
        }
    }

    private async void OnSaveJournalClicked(object? sender, EventArgs e)
    {
        try
        {
            await _viewModel.SaveJournalEntryAsync();
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync("تعذر ترحيل القيد", exception.Message, "حسنًا");
        }
    }

    private async void OnMigrateClicked(object? sender, EventArgs e)
    {
        if (!await DisplayAlertAsync(
                "ترحيل السجلات القديمة",
                "سيتم إنشاء قيود محاسبية متوازنة من السجلات اليومية الحالية دون حذفها. هل تريد المتابعة؟",
                "ترحيل",
                "إلغاء"))
        {
            return;
        }

        try
        {
            await _viewModel.MigrateLegacyEntriesAsync();
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync("تعذر الترحيل", exception.Message, "حسنًا");
        }
    }
}
