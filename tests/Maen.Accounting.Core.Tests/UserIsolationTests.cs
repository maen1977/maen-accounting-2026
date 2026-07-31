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
    public void Email_normalization_is_stable()
    {
        Assert.Equal("person@example.com", UserIsolation.NormalizeEmail(" Person@Example.COM "));
    }
}
