using Maen.Accounting.App.ViewModels;

namespace Maen.Accounting.App.Views;

public partial class PersonalDashboardPage : ContentPage
{
    private readonly MainStateViewModel _state;

    public PersonalDashboardPage(MainStateViewModel state)
    {
        InitializeComponent();
        BindingContext = _state = state;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!string.IsNullOrWhiteSpace(_state.UserEmail))
        {
            await _state.ReloadAsync();
        }
    }

    private void OnAddClicked(object? sender, EventArgs e) => _state.BeginNewEntry();
}
