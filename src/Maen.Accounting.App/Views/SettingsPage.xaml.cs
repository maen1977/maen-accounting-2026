using Maen.Accounting.App.Services;
using Maen.Accounting.App.ViewModels;

namespace Maen.Accounting.App.Views;

public partial class SettingsPage : ContentPage
{
    private readonly MainStateViewModel _state;
    private readonly AppPreferencesService _preferences;
    private readonly SessionCoordinator _coordinator;

    public SettingsPage(
        MainStateViewModel state,
        AppPreferencesService preferences,
        SessionCoordinator coordinator)
    {
        InitializeComponent();
        BindingContext = _state = state;
        _preferences = preferences;
        _coordinator = coordinator;
    }

    private async void OnChooseExperienceClicked(object? sender, EventArgs e)
    {
        var confirmed = await DisplayAlertAsync(
            UiText.Get("T123"),
            UiText.Get("T124"),
            UiText.Get("T125"),
            UiText.Get("T122"));
        if (!confirmed) return;

        _preferences.ResetOnboarding();
        _coordinator.ShowOnboarding();
    }

    private async void OnSyncClicked(object? sender, EventArgs e)
    {
        try
        {
            await _state.SyncAsync();
            await DisplayAlertAsync(UiText.Get("T045"), _state.StatusMessage, UiText.Get("T122"));
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync(UiText.Get("T121"), exception.Message, UiText.Get("T122"));
        }
    }

    private async void OnExportClicked(object? sender, EventArgs e)
    {
        try
        {
            var path = await _state.ExportAsync();
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = UiText.Get("T092"),
                File = new ShareFile(path)
            });
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync(UiText.Get("T121"), exception.Message, UiText.Get("T122"));
        }
    }

    private async void OnImportClicked(object? sender, EventArgs e)
    {
        try
        {
            var selected = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = UiText.Get("T017")
            });
            if (selected is null) return;
            if (!await DisplayAlertAsync(UiText.Get("T017"), UiText.Get("T124"), UiText.Get("T062"), UiText.Get("T122"))) return;
            var cachedPath = Path.Combine(FileSystem.CacheDirectory, $"import_{Guid.NewGuid():N}.json");
            await using (var source = await selected.OpenReadAsync())
            await using (var destination = File.Create(cachedPath))
            {
                await source.CopyToAsync(destination);
            }
            var count = await _state.ImportAsync(cachedPath);
            await DisplayAlertAsync(UiText.Get("T120"), UiText.Format("T320", count), UiText.Get("T122"));
        }
        catch (Exception exception)
        {
            var message = exception is InvalidDataException or InvalidOperationException
                ? UiText.Get("T321")
                : exception.Message;
            await DisplayAlertAsync(UiText.Get("T121"), message, UiText.Get("T122"));
        }
    }

    private async void OnSignOutClicked(object? sender, EventArgs e)
    {
        if (!await DisplayAlertAsync(UiText.Get("T055"), UiText.Get("T124"), UiText.Get("T055"), UiText.Get("T122"))) return;
        await _state.SignOutAsync();
    }
}
