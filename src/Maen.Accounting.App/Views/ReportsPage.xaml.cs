using Maen.Accounting.App.ViewModels;

namespace Maen.Accounting.App.Views;

public partial class ReportsPage : ContentPage
{
    private readonly MainStateViewModel _state;

    public ReportsPage(MainStateViewModel state)
    {
        InitializeComponent();
        BindingContext = _state = state;
    }

    private void OnEditClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: ProfitEntryItemViewModel item })
        {
            _state.BeginEdit(item);
        }
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: ProfitEntryItemViewModel item }) return;
        if (!await DisplayAlertAsync("حذف السجل", $"هل تريد حذف سجل {item.DateText}؟", "حذف", "إلغاء")) return;
        try { await _state.DeleteAsync(item); }
        catch (Exception exception) { await DisplayAlertAsync("تعذر الحذف", exception.Message, "حسنًا"); }
    }
}
