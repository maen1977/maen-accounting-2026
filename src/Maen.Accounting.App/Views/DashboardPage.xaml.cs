using Maen.Accounting.App.ViewModels;

namespace Maen.Accounting.App.Views;

public partial class DashboardPage : ContentPage
{
    private readonly MainStateViewModel _state;

    public DashboardPage(MainStateViewModel state)
    {
        InitializeComponent();
        BindingContext = _state = state;
    }

    private async void OnRefresh(object? sender, EventArgs e)
    {
        try { await _state.ReloadAsync(); }
        catch (Exception exception) { await DisplayAlertAsync(UiText.Get("T291"), exception.Message, UiText.Get("T122")); }
        finally { if (sender is RefreshView refreshView) refreshView.IsRefreshing = false; }
    }

    private void OnAddClicked(object? sender, EventArgs e) => _state.BeginNewEntry();

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
        if (!await DisplayAlertAsync(UiText.Get("T175"), UiText.Format("T176", item.DateText), UiText.Get("T177"), UiText.Get("T178"))) return;
        try
        {
            await _state.DeleteAsync(item);
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync(UiText.Get("T179"), exception.Message, UiText.Get("T180"));
        }
    }

    private void OnToggleOverdueClicked(object? sender, EventArgs e) => _state.ToggleOverdueExpanded();
}
