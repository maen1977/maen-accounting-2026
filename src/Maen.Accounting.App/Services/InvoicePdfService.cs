using System.Reflection;
#if ANDROID
using Android.Graphics;
using AndroidPdfDocument = Android.Graphics.Pdf.PdfDocument;
#endif
using Maen.Accounting.Core.Models;
using Maen.Accounting.Core.Services;
using PdfSharpCore.Drawing;
using PdfSharpCore.Fonts;
using PdfSharpCore.Pdf;

namespace Maen.Accounting.App.Services;

public sealed class InvoicePdfService
{
    private static readonly object FontLock = new();
    private static bool _fontResolverConfigured;

    public string CreateInvoicePdf(Invoice invoice, CompanyProfile? profile, string contactName, string outputDirectory)
    {
#if ANDROID
        return CreateInvoicePdfAndroid(invoice, profile, contactName, outputDirectory);
#else
        ArgumentNullException.ThrowIfNull(invoice);
        Directory.CreateDirectory(outputDirectory);
        EnsureFontResolver();

        var company = profile?.DisplayName ?? UiText.Get("T923");
        contactName = string.IsNullOrWhiteSpace(contactName) ? UiText.Get("T923") : contactName.Trim();
        var fileName = $"{SanitizeFilePart(company)}-{SanitizeFilePart(invoice.Number)}.pdf";
        var path = Path.Combine(outputDirectory, fileName);

        using var document = new PdfDocument();
        document.Info.Title = $"{UiText.Get("T920")} {invoice.Number}";
        document.Info.Author = company;
        var page = document.AddPage();
        page.Size = PdfSharpCore.PageSize.A4;
        using var graphics = XGraphics.FromPdfPage(page);
        var regular = new XFont("Amiri", 12, XFontStyle.Regular);
        var bold = new XFont("Amiri", 18, XFontStyle.Bold);
        var small = new XFont("Amiri", 10, XFontStyle.Regular);
        var headingBrush = new XSolidBrush(XColor.FromArgb(11, 31, 51));
        var accentBrush = new XSolidBrush(XColor.FromArgb(200, 164, 93));
        var textBrush = XBrushes.Black;
        var linePen = new XPen(XColor.FromArgb(210, 216, 224), 0.8);
        const double right = 550;
        const double left = 45;

        graphics.DrawRectangle(headingBrush, 0, 0, page.Width, 92);
        DrawRight(graphics, company, bold, XBrushes.White, right, 25, 505, 30);
        DrawRight(graphics, profile?.LegalName ?? string.Empty, small, XBrushes.White, right, 57, 505, 20);

        var invoiceTitle = invoice.Type == InvoiceType.Sales ? UiText.Get("T921") : UiText.Get("T922");
        DrawRight(graphics, invoiceTitle, bold, accentBrush, right, 112, 505, 30);
        DrawRight(graphics, $"{UiText.Get("T924")}: {invoice.Number}", regular, textBrush, right, 148, 505, 22);
        DrawRight(graphics, $"{UiText.Get("T925")}: {invoice.IssueDate:yyyy-MM-dd}", regular, textBrush, right, 173, 505, 22);
        DrawRight(graphics, $"{UiText.Get("T926")}: {profile?.RegistrationNumber ?? UiText.Get("T923")}", regular, textBrush, right, 198, 505, 22);
        DrawRight(graphics, $"{UiText.Get("T927")}: {profile?.TaxNumber ?? UiText.Get("T923")}", regular, textBrush, right, 223, 505, 22);
        DrawRight(graphics, $"{UiText.Get("T928")}: {contactName}", regular, textBrush, right, 260, 505, 22);
        DrawRight(graphics, $"{UiText.Get("T929")}: {invoice.DueDate:yyyy-MM-dd}", regular, textBrush, right, 285, 505, 22);

        var y = 335.0;
        graphics.DrawLine(linePen, left, y, right, y);
        DrawRight(graphics, UiText.Get("T930"), bold, headingBrush, right, y + 9, 270, 22);
        DrawRight(graphics, UiText.Get("T931"), bold, headingBrush, 300, y + 9, 205, 22);
        y += 37;
        graphics.DrawLine(linePen, left, y, right, y);

        foreach (var line in invoice.Lines)
        {
            DrawRight(graphics, Money.Format(line.AmountMinor), regular, textBrush, right, y + 10, 190, 26);
            DrawRight(graphics, line.Description, regular, textBrush, 300, y + 10, 205, 26);
            y += 31;
            graphics.DrawLine(linePen, left, y, right, y);
        }

        y += 18;
        DrawRight(graphics, $"{UiText.Get("T932")}: {Money.Format(invoice.SubtotalMinor)}", regular, textBrush, right, y, 505, 24);
        y += 27;
        DrawRight(graphics, $"{UiText.Get("T933")}: {Money.Format(invoice.TaxMinor)}", regular, textBrush, right, y, 505, 24);
        y += 32;
        graphics.DrawRectangle(accentBrush, left, y, right - left, 40);
        DrawRight(graphics, $"{UiText.Get("T934")}: {Money.Format(invoice.TotalMinor)}", bold, XBrushes.White, right - 12, y + 8, 505, 26);

        y += 63;
        if (profile is not null)
        {
            DrawRight(graphics, $"{UiText.Get("T935")}: {Money.Format(profile.CapitalMinor)}", small, textBrush, right, y, 505, 20);
            y += 22;
            DrawRight(graphics, profile.Address, small, textBrush, right, y, 505, 20);
            y += 22;
            DrawRight(graphics, $"{profile.Phone}  {profile.Email}", small, textBrush, right, y, 505, 20);
        }

        document.Save(path);
        return path;
#endif
    }

#if ANDROID
    private static string CreateInvoicePdfAndroid(Invoice invoice, CompanyProfile? profile, string contactName, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        Directory.CreateDirectory(outputDirectory);
        var company = profile?.DisplayName ?? UiText.Get("T923");
        contactName = string.IsNullOrWhiteSpace(contactName) ? UiText.Get("T923") : contactName.Trim();
        var fileName = $"{SanitizeFilePart(company)}-{SanitizeFilePart(invoice.Number)}.pdf";
        var path = Path.Combine(outputDirectory, fileName);

        using var document = new AndroidPdfDocument();
        var pageInfo = new AndroidPdfDocument.PageInfo.Builder(595, 842, 1).Create();
        var page = document.StartPage(pageInfo);
        var canvas = page.Canvas;
        var regular = CreateAndroidPaint(12, Android.Graphics.Color.Black, Paint.Align.Right, false);
        var small = CreateAndroidPaint(10, Android.Graphics.Color.Black, Paint.Align.Right, false);
        var bold = CreateAndroidPaint(18, Android.Graphics.Color.White, Paint.Align.Right, true);
        var heading = CreateAndroidPaint(18, Android.Graphics.Color.Rgb(200, 164, 93), Paint.Align.Right, true);
        var whiteSmall = CreateAndroidPaint(10, Android.Graphics.Color.White, Paint.Align.Right, false);
        var accent = new Paint(PaintFlags.AntiAlias) { Color = Android.Graphics.Color.Rgb(200, 164, 93) };
        accent.SetStyle(Paint.Style.Fill);
        var line = new Paint(PaintFlags.AntiAlias) { Color = Android.Graphics.Color.Rgb(210, 216, 224), StrokeWidth = 1 };
        line.SetStyle(Paint.Style.Stroke);

        canvas.DrawRect(0, 0, 595, 92, new Paint(PaintFlags.AntiAlias) { Color = Android.Graphics.Color.Rgb(11, 31, 51) });
        DrawAndroidRight(canvas, company, bold, 550, 32);
        DrawAndroidRight(canvas, profile?.LegalName ?? string.Empty, whiteSmall, 550, 60);

        var invoiceTitle = invoice.Type == InvoiceType.Sales ? UiText.Get("T921") : UiText.Get("T922");
        DrawAndroidRight(canvas, invoiceTitle, heading, 550, 126);
        DrawAndroidRight(canvas, $"{UiText.Get("T924")}: {invoice.Number}", regular, 550, 162);
        DrawAndroidRight(canvas, $"{UiText.Get("T925")}: {invoice.IssueDate:yyyy-MM-dd}", regular, 550, 187);
        DrawAndroidRight(canvas, $"{UiText.Get("T926")}: {profile?.RegistrationNumber ?? UiText.Get("T923")}", regular, 550, 212);
        DrawAndroidRight(canvas, $"{UiText.Get("T927")}: {profile?.TaxNumber ?? UiText.Get("T923")}", regular, 550, 237);
        DrawAndroidRight(canvas, $"{UiText.Get("T928")}: {contactName}", regular, 550, 274);
        DrawAndroidRight(canvas, $"{UiText.Get("T929")}: {invoice.DueDate:yyyy-MM-dd}", regular, 550, 299);

        var y = 344f;
        canvas.DrawLine(45, y, 550, y, line);
        DrawAndroidRight(canvas, UiText.Get("T930"), heading, 550, y + 26);
        DrawAndroidRight(canvas, UiText.Get("T931"), heading, 300, y + 26);
        y += 52;
        canvas.DrawLine(45, y, 550, y, line);
        foreach (var item in invoice.Lines)
        {
            DrawAndroidRight(canvas, Money.Format(item.AmountMinor), regular, 550, y + 27);
            DrawAndroidRight(canvas, item.Description, regular, 300, y + 27);
            y += 34;
            canvas.DrawLine(45, y, 550, y, line);
        }

        y += 28;
        DrawAndroidRight(canvas, $"{UiText.Get("T932")}: {Money.Format(invoice.SubtotalMinor)}", regular, 550, y);
        y += 29;
        DrawAndroidRight(canvas, $"{UiText.Get("T933")}: {Money.Format(invoice.TaxMinor)}", regular, 550, y);
        y += 18;
        canvas.DrawRect(45, y, 550, y + 48, accent);
        DrawAndroidRight(canvas, $"{UiText.Get("T934")}: {Money.Format(invoice.TotalMinor)}", bold, 538, y + 32);

        y += 79;
        if (profile is not null)
        {
            DrawAndroidRight(canvas, $"{UiText.Get("T935")}: {Money.Format(profile.CapitalMinor)}", small, 550, y);
            y += 24;
            DrawAndroidRight(canvas, profile.Address, small, 550, y);
            y += 24;
            DrawAndroidRight(canvas, $"{profile.Phone}  {profile.Email}", small, 550, y);
        }

        document.FinishPage(page);
        using var stream = File.Create(path);
        document.WriteTo(stream);
        document.Close();
        return path;
    }

    private static Paint CreateAndroidPaint(float textSize, Android.Graphics.Color color, Paint.Align align, bool isBold)
    {
        var paint = new Paint(PaintFlags.AntiAlias)
        {
            Color = color,
            TextSize = textSize,
            TextAlign = align,
            Typeface = Typeface.Create("sans-serif", isBold ? TypefaceStyle.Bold : TypefaceStyle.Normal)
        };
        paint.SubpixelText = true;
        return paint;
    }

    private static void DrawAndroidRight(Canvas canvas, string? text, Paint paint, float right, float baseline) =>
        canvas.DrawText(text ?? string.Empty, right, baseline, paint);
#endif

    private static void DrawRight(XGraphics graphics, string? text, XFont font, XBrush brush, double right, double top, double width, double height)
    {
        graphics.DrawString(
            text ?? string.Empty,
            font,
            brush,
            new XRect(right - width, top, width, height),
            XStringFormats.TopRight);
    }

    private static void EnsureFontResolver()
    {
        if (_fontResolverConfigured)
        {
            return;
        }

        lock (FontLock)
        {
            if (_fontResolverConfigured)
            {
                return;
            }

            GlobalFontSettings.FontResolver = new EmbeddedAmiriFontResolver();
            _fontResolverConfigured = true;
        }
    }

    private static string SanitizeFilePart(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string((value ?? string.Empty).Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "invoice" : cleaned;
    }

    private sealed class EmbeddedAmiriFontResolver : IFontResolver
    {
        private static readonly byte[] FontBytes = LoadFontBytes();

        public string DefaultFontName => "Amiri";

        public byte[] GetFont(string faceName) => FontBytes;

        public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
            new("Amiri-Regular.ttf", false, false);

        private static byte[] LoadFontBytes()
        {
            var assembly = typeof(InvoicePdfService).GetTypeInfo().Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith("Amiri-Regular.ttf", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("The Arabic invoice font is not embedded.");
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException("The Arabic invoice font could not be loaded.");
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }
    }
}
