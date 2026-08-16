using System.Globalization;
using Microsoft.Maui.Controls;

namespace Maen.Accounting.App;

/// <summary>
/// Converts an obligation event's paid flag to a status badge color (gold for unpaid, green for paid).
/// </summary>
public sealed class ObligationStatusConverter : IValueConverter
{
    public static readonly ObligationStatusConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? "#137A53" : "#C8A45D";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts a non-null value to true for conditional visibility bindings.
/// </summary>
public sealed class NonNullConverter : IValueConverter
{
    public static readonly NonNullConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Inverts a boolean value for inverse visibility bindings.
/// </summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public static readonly InverseBooleanConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Converts an obligation event's paid flag to a two-language status label.
/// </summary>
public sealed class ObligationTextConverter : IValueConverter
{
    public static readonly ObligationTextConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? UiText.Get("T404") : UiText.Get("T402");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
