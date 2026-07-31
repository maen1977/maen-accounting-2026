namespace Maen.Accounting.App.Services;

public sealed class DeviceIdentityService
{
    private const string Key = "maen_device_id";

    public string GetOrCreate()
    {
        var current = Preferences.Default.Get(Key, string.Empty);
        if (!string.IsNullOrWhiteSpace(current))
        {
            return current;
        }

        current = Guid.NewGuid().ToString("N");
        Preferences.Default.Set(Key, current);
        return current;
    }
}
