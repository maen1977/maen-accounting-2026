using Maen.Accounting.App.ViewModels;

namespace Maen.Accounting.App.Views;

public partial class SettingsPage : ContentPage
{
    private readonly MainStateViewModel _state;

    public SettingsPage(MainStateViewModel state)
    {
        InitializeComponent();
        BindingContext = _state = state;
    }

    private async void OnSyncClicked(object? sender, EventArgs e)
    {
        try
        {
            await _state.SyncAsync();
            await DisplayAlertAsync("المزامنة", _state.StatusMessage, "حسنًا");
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync("تعذر المزامنة", exception.Message, "حسنًا");
        }
    }

    private async void OnExportClicked(object? sender, EventArgs e)
    {
        try
        {
            var path = await _state.ExportAsync();
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "نسخة معن للمحاسبة",
                File = new ShareFile(path)
            });
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync("تعذر التصدير", exception.Message, "حسنًا");
        }
    }

    private async void OnImportClicked(object? sender, EventArgs e)
    {
        try
        {
            var selected = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "اختر ملف نسخة JSON"
            });
            if (selected is null) return;
            if (!await DisplayAlertAsync("استيراد النسخة", "سيتم دمج السجلات مع بيانات الحساب الحالي دون حذف الأحدث. متابعة؟", "استيراد", "إلغاء")) return;
            var cachedPath = Path.Combine(FileSystem.CacheDirectory, $"import_{Guid.NewGuid():N}.json");
            await using (var source = await selected.OpenReadAsync())
            await using (var destination = File.Create(cachedPath))
            {
                await source.CopyToAsync(destination);
            }
            var count = await _state.ImportAsync(cachedPath);
            await DisplayAlertAsync("تم الاستيراد", $"تمت معالجة {count} سجلًا.", "حسنًا");
        }
        catch (Exception exception)
        {
            await DisplayAlertAsync("تعذر الاستيراد", exception.Message, "حسنًا");
        }
    }

    private async void OnSignOutClicked(object? sender, EventArgs e)
    {
        if (!await DisplayAlertAsync("تسجيل الخروج", "ستبقى بيانات الحساب محفوظة في قاعدته المنفصلة على الجهاز.", "خروج", "إلغاء")) return;
        await _state.SignOutAsync();
    }
}
