using Maen.Accounting.App.ViewModels;

namespace Maen.Accounting.App.Views;

public partial class BusinessProfilePage : ContentPage
{
    private readonly BusinessViewModel _viewModel;
    private bool _loaded;

    public BusinessProfilePage(BusinessViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded)
        {
            return;
        }

        try
        {
            await _viewModel.LoadCompanyProfileAsync();
            _loaded = true;
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync(UiText.Get("T293"), exception.Message, UiText.Get("T122"));
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        try
        {
            await _viewModel.SaveCompanyProfileAsync();
            _loaded = true;
            await DisplayAlertAsync(UiText.Get("T891"), UiText.Get("T893"), UiText.Get("T122"));
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync(UiText.Get("T293"), exception.Message, UiText.Get("T122"));
        }
    }
}
