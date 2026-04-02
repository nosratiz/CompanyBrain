using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using CompanyBrain.Utilities;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using HtmlAgilityPack;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;

namespace CompanyBrain.Services;

internal static class DocumentMarkdownConverter
{
    private static readonly Regex HeadingStyleRegex = new("Heading([1-6])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static Task<string> ConvertAsync(string fullPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Path.GetExtension(fullPath).ToLowerInvariant() switch
        {
            ".xlsx" => Task.FromResult(ConvertSpreadsheet(fullPath)),
            ".docx" => Task.FromResult(ConvertWordDocument(fullPath)),
            ".pdf" => Task.FromResult(ConvertPdf(fullPath)),
            ".md" => File.ReadAllTextAsync(fullPath, cancellationToken),
            ".txt" => File.ReadAllTextAsync(fullPath, cancellationToken),
            ".html" or ".htm" => Task.FromResult(ConvertHtmlFile(fullPath)),
            _ => throw new NotSupportedException($"Unsupported document type: {Path.GetExtension(fullPath)}"),
        };
    }

    private static string ConvertSpreadsheet(string fullPath)
    {
        using var workbook = new XLWorkbook(fullPath);
        var builder = new StringBuilder();

        foreach (var worksheet in workbook.Worksheets)
        {
            var range = worksheet.RangeUsed();
            if (range is null)
            {
                continue;
            }

            var rows = range.RowsUsed()
                .Select(row => row.CellsUsed(XLCellsUsedOptions.All)
                    .Select(cell => MarkdownUtilities.EscapeTableCell(cell.GetFormattedString()))
                    .ToList())
                .Where(row => row.Count > 0)
                .ToList();

            if (rows.Count == 0)
            {
                continue;
            }

            var width = rows.Max(row => row.Count);
            foreach (var row in rows)
            {
                while (row.Count < width)
                {
                    row.Add(string.Empty);
                }
            }

            builder.AppendLine($"# {worksheet.Name}");
            builder.AppendLine();
            builder.AppendLine($"| {string.Join(" | ", rows[0])} |");
            builder.AppendLine($"| {string.Join(" | ", Enumerable.Repeat("---", width))} |");

            foreach (var row in rows.Skip(1))
            {
                builder.AppendLine($"| {string.Join(" | ", row)} |");
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string ConvertWordDocument(string fullPath)
    {
        using var document = WordprocessingDocument.Open(fullPath, false);
        var body = document.MainDocumentPart?.Document?.Body
            ?? throw new InvalidOperationException($"The Word document does not contain a body: {fullPath}");

        var builder = new StringBuilder();

        foreach (var paragraph in body.Elements<Paragraph>())
        {
            var text = paragraph.InnerText?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (TryGetHeadingLevel(paragraph, out var level))
            {
                builder.AppendLine($"{new string('#', level)} {text}");
            }
            else if (paragraph.ParagraphProperties?.NumberingProperties is not null)
            {
                builder.AppendLine($"- {text}");
            }
            else
            {
                builder.AppendLine(text);
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static bool TryGetHeadingLevel(Paragraph paragraph, out int level)
    {
        level = 0;
        var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        if (string.IsNullOrWhiteSpace(styleId))
        {
            return false;
        }

        var match = HeadingStyleRegex.Match(styleId);
        if (!match.Success)
        {
            return false;
        }

        level = int.Parse(match.Groups[1].Value);
        return level is >= 1 and <= 6;
    }

    private static string ConvertPdf(string fullPath)
    {
        using var reader = new PdfReader(fullPath);
        using var pdf = new PdfDocument(reader);
        var builder = new StringBuilder();

        for (var pageNumber = 1; pageNumber <= pdf.GetNumberOfPages(); pageNumber++)
        {
            var text = PdfTextExtractor.GetTextFromPage(pdf.GetPage(pageNumber)).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            builder.AppendLine($"## Page {pageNumber}");
            builder.AppendLine();
            builder.AppendLine(text);
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string ConvertHtmlFile(string fullPath)
    {
        var document = new HtmlDocument();
        document.Load(fullPath);

        var boilerplateNodes = document.DocumentNode.SelectNodes("//script|//style|//nav|//footer|//header|//aside|//form|//noscript|//svg");
        if (boilerplateNodes is not null)
        {
            foreach (var node in boilerplateNodes)
            {
                node.Remove();
            }
        }

        var root = document.DocumentNode.SelectSingleNode("//main")
            ?? document.DocumentNode.SelectSingleNode("//article")
            ?? document.DocumentNode.SelectSingleNode("//body")
            ?? document.DocumentNode;

        return HtmlMarkdownConverter.Convert(root, new Uri("file:///"));
    }
}