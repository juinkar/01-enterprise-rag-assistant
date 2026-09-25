using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace EnterpriseRagAssistant.Services;

public sealed class PdfTextExtractor
{
    public IReadOnlyList<(int PageNumber, string Text)> Extract(Stream pdfStream)
    {
        using var document = PdfDocument.Open(pdfStream);
        var pages = new List<(int, string)>();

        foreach (var page in document.GetPages())
        {
            var text = ContentOrderTextExtractor.GetText(page);
            if (!string.IsNullOrWhiteSpace(text))
            {
                pages.Add((page.Number, text.Trim()));
            }
        }

        return pages;
    }
}
