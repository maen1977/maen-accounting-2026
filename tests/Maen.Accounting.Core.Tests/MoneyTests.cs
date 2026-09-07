using Maen.Accounting.Core.Services;

namespace Maen.Accounting.Core.Tests;

public sealed class MoneyTests
{
    [Theory]
    [InlineData("12.34", 1234)]
    [InlineData("١٢٫٣٤", 1234)]
    [InlineData("12,34", 1234)]
    [InlineData("0.005", 1)]
    public void Parses_money_without_binary_floating_point(string input, long expected)
    {
        Assert.True(Money.TryParse(input, out var minor));
        Assert.Equal(expected, minor);
    }

    [Fact]
    public void Formats_money_with_three_decimal_places()
    {
        Assert.Equal("12.340", Money.Format(1234, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("")]
    public void Rejects_invalid_or_negative_money(string input) =>
        Assert.False(Money.TryParse(input, out _));
}
