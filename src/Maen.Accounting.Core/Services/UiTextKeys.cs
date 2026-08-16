namespace Maen.Accounting.Core.Services;

/// <summary>
/// Fallback English captions for UI keys used by core services (CSV exports).
/// The app layer (UiText) provides the localized bilingual version.
/// </summary>
internal static class UiTextKeys
{
    public static string Get(string key) => key switch
    {
        "T510" => "Total",
        "T504" => "Monthly Sales",
        "T505" => "Monthly Purchases",
        "T506" => "Monthly Receipts",
        "T507" => "Supplier Payments",
        "T508" => "Net Cash Flow",
        _ => key
    };
}
