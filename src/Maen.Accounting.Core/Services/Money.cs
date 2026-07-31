using System.Globalization;
using System.Text;

namespace Maen.Accounting.Core.Services;

public static class Money
{
    public const int Scale = 100;

    public static long FromDecimal(decimal value) =>
        checked((long)decimal.Round(value * Scale, 0, MidpointRounding.AwayFromZero));

    public static decimal ToDecimal(long minor) => minor / (decimal)Scale;

    public static bool TryParse(string? text, out long minor)
    {
        minor = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = NormalizeDigits(text.Trim())
            .Replace("٬", string.Empty, StringComparison.Ordinal)
            .Replace(",", ".", StringComparison.Ordinal)
            .Replace("٫", ".", StringComparison.Ordinal);

        if (!decimal.TryParse(
                normalized,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out var value))
        {
            return false;
        }

        if (value < 0)
        {
            return false;
        }

        try
        {
            minor = FromDecimal(value);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    public static string Format(long minor, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.GetCultureInfo("ar-JO");
        return ToDecimal(minor).ToString("N2", culture);
    }

    private static string NormalizeDigits(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(character switch
            {
                '٠' or '۰' => '0',
                '١' or '۱' => '1',
                '٢' or '۲' => '2',
                '٣' or '۳' => '3',
                '٤' or '۴' => '4',
                '٥' or '۵' => '5',
                '٦' or '۶' => '6',
                '٧' or '۷' => '7',
                '٨' or '۸' => '8',
                '٩' or '۹' => '9',
                _ => character
            });
        }

        return builder.ToString();
    }
}
