using Maen.Accounting.Core.Services;

namespace Maen.Accounting.Core.Tests;

public sealed class UserIsolationTests
{
    [Fact]
    public void Different_users_get_different_database_files()
    {
        Assert.NotEqual(UserIsolation.DatabaseFileName("uid-a"), UserIsolation.DatabaseFileName("uid-b"));
    }

    [Fact]
    public void Wallet_scope_is_isolated_from_other_experiences()
    {
        var wallet = UserIsolation.DatabaseFileName("uid-a", "wallet");
        var personal = UserIsolation.DatabaseFileName("uid-a", "personal");
        var business = UserIsolation.DatabaseFileName("uid-a", "business");

        Assert.Contains("_wallet.db3", wallet);
        Assert.NotEqual(wallet, personal);
        Assert.NotEqual(wallet, business);
    }

    [Fact]
    public void Email_normalization_is_stable()
    {
        Assert.Equal("person@example.com", UserIsolation.NormalizeEmail(" Person@Example.COM "));
    }

    [Fact]
    public void Device_local_user_id_is_stable_and_isolated()
    {
        Assert.Equal(
            UserIsolation.LocalDeviceUserId("device-a"),
            UserIsolation.LocalDeviceUserId("device-a"));
        Assert.NotEqual(
            UserIsolation.LocalDeviceUserId("device-a"),
            UserIsolation.LocalDeviceUserId("device-b"));
    }
}
