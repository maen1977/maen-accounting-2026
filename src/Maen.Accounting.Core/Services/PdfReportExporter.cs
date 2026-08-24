using System.Diagnostics;
using System.Text;

namespace Maen.Accounting.Core.Services;

/// <summary>
/// Renders an RTL-friendly HTML report and converts it to a PDF file using the
/// best available local HTML-to-PDF renderer (wkhtmltopdf first, then weasyprint),
/// returning the produced file path or throwing when no renderer is installed.
/// </summary>
public static class PdfReportExporter
{
    public static string Renderer =>
        TryRendererPath("wkhtmltopdf") is not null ? "wkhtmltopdf"
        : TryRendererPath("weasyprint") is not null ? "weasyprint"
        : string.Empty;

    public static bool IsAvailable => !string.IsNullOrEmpty(Renderer);

    public static string BuildReportHtml(
        string title,
        string subtitle,
        IReadOnlyList<(string Label, string Value)> rows,
        IReadOnlyList<(string, string, string)>? table = null)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html dir=\"rtl\" lang=\"ar\"><head><meta charset=\"utf-8\"/>")
          .Append("<style>")
          .Append("body{font-family:'Noto Naskh Arabic','Noto Sans Arabic','DejaVu Sans',sans-serif;padding:24px;color:#1a1a1a;}")
          .Append("h1{color:#047857;font-size:22px;margin-bottom:4px;}")
          .Append("h2{color:#555;font-size:14px;font-weight:normal;margin-top:0;}")
          .Append("table{width:100%;border-collapse:collapse;margin-top:16px;}")
          .Append("th{background:#047857;color:#fff;padding:8px 10px;font-size:13px;}")
          .Append("td{padding:7px 10px;border-bottom:1px solid #e5e7eb;font-size:13px;}")
          .Append("tr:nth-child(even) td{background:#f0fdf4;}")
          .Append(".row{display:flex;justify-content:space-between;padding:6px 2px;border-bottom:1px dashed #d1d5db;font-size:14px;}")
          .Append(".row b{color:#047857;}")
          .Append(".footer{margin-top:20px;font-size:11px;color:#9ca3af;text-align:center;}")
          .Append("</style></head><body>")
          .Append(System.Globalization.CultureInfo.InvariantCulture, $"<h1>{Escape(title)}</h1><h2>{Escape(subtitle)}</h2>");

        foreach (var (label, value) in rows)
        {
            sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"<div class=\"row\"><span>{Escape(label)}</span><b>{Escape(value)}</b></div>");
        }

        if (table is not null && table.Count > 0)
        {
            sb.Append("<table><tr>");
            foreach (var header in TupleElements(table[0]))
            {
                sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"<th>{Escape(header)}</th>");
            }

            sb.Append("</tr>");
            foreach (var row in table.Skip(1))
            {
                sb.Append("<tr>");
                foreach (var cell in TupleElements(row))
                {
                    sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"<td>{Escape(cell)}</td>");
                }

                sb.Append("</tr>");
            }

            sb.Append("</table>");
        }

        sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"<div class=\"footer\">Maen Accounting — {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm} UTC</div>")
          .Append("</body></html>");

        return sb.ToString();
    }

    /// <summary>Writes <paramref name="html"/> to a PDF file and returns its absolute path.</summary>
    public static async Task<string> ExportAsync(string html, string outputFileName, string? workDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(html);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFileName);

        var renderer = Renderer;
        if (string.IsNullOrEmpty(renderer))
        {
            throw new InvalidOperationException(
                "No HTML-to-PDF renderer is installed (tried wkhtmltopdf and weasyprint).");
        }

        var workDir = workDirectory ?? Path.GetTempPath();
        var htmlPath = Path.Combine(workDir, $"{Path.GetFileNameWithoutExtension(outputFileName)}.html");
        var pdfPath = Path.Combine(workDir, outputFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? outputFileName : $"{outputFileName}.pdf");

        await File.WriteAllTextAsync(htmlPath, html, Encoding.UTF8);
        try
        {
            if (string.Equals(renderer, "wkhtmltopdf", StringComparison.Ordinal))
            {
                await RunAsync("wkhtmltopdf", $"--enable-local-file-access --no-stop-slow-scripts \"{htmlPath}\" \"{pdfPath}\"");
            }
            else
            {
                await RunAsync("weasyprint", $"\"{htmlPath}\" \"{pdfPath}\"");
            }

            if (!File.Exists(pdfPath) || new FileInfo(pdfPath).Length == 0)
            {
                throw new InvalidOperationException($"Renderer {renderer} produced an empty PDF.");
            }
        }
        finally
        {
            if (File.Exists(htmlPath))
            {
                File.Delete(htmlPath);
            }
        }

        return pdfPath;
    }

    private static string? TryRendererPath(string name)
    {
        try
        {
            var start = new ProcessStartInfo(name, "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(start);
            if (process is null)
            {
                return null;
            }

            process.WaitForExit(5_000);
            return process.ExitCode == 0 ? name : null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task RunAsync(string fileName, string arguments)
    {
        var start = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(start)
            ?? throw new InvalidOperationException($"Failed to start {fileName}.");
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"{fileName} exited with code {process.ExitCode}: {error}");
        }
    }

    private static IEnumerable<string> TupleElements((string, string, string) tuple)
    {
        yield return tuple.Item1;
        yield return tuple.Item2;
        yield return tuple.Item3;
    }

    private static string Escape(string text) =>
        text.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
}
