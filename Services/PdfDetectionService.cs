using DocumentProcessingApi.Models.Enums;
using UglyToad.PdfPig;

namespace DocumentProcessingApi.Services
{
    public class PdfDetectionService
    {
        public PdfKind DetectPdfKind(string filePath)
        {
            try
            {
                using var document = PdfDocument.Open(filePath);

                var combinedText = new List<string>();

                foreach (var page in document.GetPages().Take(3))
                {
                    var text = page.Text?.Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        combinedText.Add(text);
                    }
                }

                var merged = string.Join(" ", combinedText).Trim();

                if (!string.IsNullOrWhiteSpace(merged) && merged.Length > 30)
                {
                    return PdfKind.DigitalTextPdf;
                }

                return PdfKind.ScannedPdf;
            }
            catch
            {
                return PdfKind.Unknown;
            }
        }
    }
}
