using System.Globalization;

namespace Maen.Accounting.Core.Services;

/// <summary>
/// Offline multi-currency conversion between user-defined currencies. Rates are
/// stored as rational pairs (numerator/denominator) for exact arithmetic and are
/// never fetched from the network; a fixed seed table ships for common currencies.
/// </summary>
public static class CurrencyConverter
{
    public const string DefaultCurrencyCode = "JOD";

    public static IReadOnlyList<CurrencyProfile> SeedProfiles { get; } =
    [
        new("JOD", "د.أ", 1.0m),
        new("USD", "$", 0.709m),
        new("EUR", "€", 0.78m),
        new("GBP", "£", 0.91m),
        new("SAR", "﷼", 0.189m),
        new("AED", "د.إ", 0.193m),
        new("KWD", "د.ك", 2.32m),
        new("EGP", "ج.م", 0.014m),
        new("TRY", "₺", 0.022m),
        new("CNY", "¥", 0.098m),
    ];

    /// <summary>Converts an amount expressed in <paramref name="from"/> into <paramref name="to"/> using <paramref name="rates"/>.</summary>
    public static long Convert(
        long amountMinor,
        string from,
        string to,
        IReadOnlyDictionary<string, CurrencyRate> rates)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(from);
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        if (amountMinor == 0 || string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            return amountMinor;
        }

        var rate = ResolveRate(from, to, rates);
        if (rate is null)
        {
            throw new InvalidOperationException(
                $"No conversion rate available between {from} and {to}.");
        }

        var converted = rate.Convert(amountMinor);
        return (long)Math.Round(converted, MidpointRounding.AwayFromZero);
    }

    public static CurrencyRate? ResolveRate(
        string from,
        string to,
        IReadOnlyDictionary<string, CurrencyRate> rates)
    {
        if (rates.TryGetValue(SeedPairKey(from, to), out var direct))
        {
            return direct;
        }

        // Inverse: if a rate exists for (to → from), flipping it converts (from → to).
        if (rates.TryGetValue(SeedPairKey(to, from), out var inverse))
        {
            return new CurrencyRate(inverse.DenominatorNumerator, inverse.NumeratorNumerator);
        }

        return null;
    }

    /// <summary>Chain conversion through an anchor currency when no direct/inverse rate exists.</summary>
    public static long ConvertThroughAnchor(
        long amountMinor,
        string from,
        string to,
        string anchor,
        IReadOnlyDictionary<string, CurrencyRate> rates)
    {
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            return amountMinor;
        }

        var first = Convert(amountMinor, from, anchor, rates);
        return Convert(first, anchor, to, rates);
    }

    public static string SeedPairKey(string baseCurrency, string quoteCurrency) =>
        string.Concat(baseCurrency.ToUpperInvariant(), quoteCurrency.ToUpperInvariant());

    /// <summary>Builds an initial rate dictionary from seed profiles against a chosen base currency.
    /// Each profile's <see cref="CurrencyProfile.ValuePerBaseUnit"/> expresses one unit of the profile
    /// currency in units of the base currency (e.g. 1 USD = 0.709 JOD when JOD is the base),
    /// so converting from base to profile divides by that value.</summary>
    public static Dictionary<string, CurrencyRate> FromSeedProfiles(
        string baseCurrency,
        IReadOnlyList<CurrencyProfile> profiles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseCurrency);
        if (!profiles.Any(profile => string.Equals(profile.Code, baseCurrency, StringComparison.OrdinalIgnoreCase)))
        {
            return [];
        }

        const long Precision = 10_000L; // rational denominator precision
        var rates = new Dictionary<string, CurrencyRate>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in profiles)
        {
            if (string.Equals(profile.Code, baseCurrency, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (profile.ValuePerBaseUnit <= 0)
            {
                continue;
            }

            // converted = amount * 1 / value  →  rational: Numerator = Precision, Denominator = value * Precision
            rates[SeedPairKey(baseCurrency, profile.Code)] = new CurrencyRate(
                NumeratorNumerator: Precision,
                DenominatorNumerator: (long)decimal.Round(profile.ValuePerBaseUnit * Precision, 0, MidpointRounding.AwayFromZero));
        }

        return rates;
    }

    public static CurrencyProfile? ResolveProfile(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        foreach (var profile in SeedProfiles)
        {
            if (string.Equals(profile.Code, code, StringComparison.OrdinalIgnoreCase))
            {
                return profile;
            }
        }

        return null;
    }

    public static string FormatWithSymbol(long minor, CurrencyProfile? profile = null, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.GetCultureInfo("ar-JO");
        var text = Money.Format(minor, culture);
        return profile is null ? text : $"{text} {profile.Symbol}".Trim();
    }
}

public sealed record CurrencyProfile(string Code, string Symbol, decimal ValuePerBaseUnit)
{
    public string DisplayName => string.Equals(Code, CurrencyConverter.DefaultCurrencyCode, StringComparison.OrdinalIgnoreCase)
        ? Code
        : $"{Code} ({Symbol})";
}

    /// <summary>Exact rational conversion rate: converted = amount * Numerator / Denominator. Denominator must be positive.</summary>
    public sealed record CurrencyRate(long NumeratorNumerator, long DenominatorNumerator)
    {
        public decimal Convert(decimal amount)
        {
            if (DenominatorNumerator <= 0)
            {
                throw new InvalidOperationException("Currency rate has a non-positive denominator.");
            }

            return checked(amount * NumeratorNumerator / DenominatorNumerator);
        }

        public string RatioText => $"{NumeratorNumerator} / {DenominatorNumerator}";
    }
