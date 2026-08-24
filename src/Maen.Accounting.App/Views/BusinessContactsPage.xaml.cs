using Maen.Accounting.App.ViewModels;

namespace Maen.Accounting.App.Views;

public partial class BusinessContactsPage : ContentPage
{
    private readonly BusinessViewModel _viewModel;

    public BusinessContactsPage(BusinessViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    private async void OnSaveContactClicked(object? sender, EventArgs e)
    {
        try
        {
            await _viewModel.SaveContactAsync();
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync(UiText.Get("T292"), exception.Message, UiText.Get("T122"));
        }
    }

    private void OnEditContactClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: ContactItemViewModel item })
        {
            _viewModel.BeginEditContact(item);
        }
    }

    private async void OnDeleteContactClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: ContactItemViewModel item }) return;
        if (!await DisplayAlertAsync(UiText.Get("T838"), UiText.Format("T840", item.Name), UiText.Get("T177"), UiText.Get("T178"))) return;
        try
        {
            await _viewModel.DeleteContactAsync(item);
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync(UiText.Get("T292"), exception.Message, UiText.Get("T122"));
        }
    }
}
