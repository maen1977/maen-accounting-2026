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

    private void OnToggleOverdueClicked(object? sender, EventArgs e) => _state.ToggleOverdueExpanded();
}
