using System.Text;

namespace Maen.Accounting.Core.Services;

/// <summary>
/// Deterministic free-form input sanitizer: removes control characters and
/// trims input to safe length limits so that persisted data stays clean and
/// the database is protected from pathological inputs.
/// </summary>
public static class InputSanitizer
{
    public const int NameMaxLength = 80;
    public const int NotesMaxLength = 300;
    public const int NumberMaxLength = 40;

    public static string SanitizeName(string input) => Sanitize(input, NameMaxLength);

    public static string SanitizeNotes(string input) => Sanitize(input, NotesMaxLength);

    public static string SanitizeNumber(string input) => Sanitize(input, NumberMaxLength);

    private static string Sanitize(string input, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var builder = new StringBuilder(input.Length);
        foreach (var character in input)
        {
            if (char.IsControl(character) && character != '\n') continue;
            builder.Append(character);
        }

        var trimmed = builder.ToString().Trim();
        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}
