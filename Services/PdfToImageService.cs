using PDFtoImage;
using SkiaSharp;

namespace DocumentProcessingApi.Services;

public interface IPdfToImageService
{
    List<string> ConvertPdfToImages(string pdfPath, string outputFolder, int dpi = 300);
}

public class PdfToImageService : IPdfToImageService
{
    /// <summary>
    /// Converts each page of a PDF to a separate PNG image using PDFtoImage.
    /// </summary>
    public List<string> ConvertPdfToImages(string pdfPath, string outputFolder, int dpi = 300)
    {
        var imagePaths = new List<string>();
        
        Console.WriteLine($"[PdfToImage] Starting PDF to image conversion...");
        Console.WriteLine($"[PdfToImage] PDF Path: {pdfPath}");
        Console.WriteLine($"[PdfToImage] Output Folder: {outputFolder}");
        Console.WriteLine($"[PdfToImage] DPI: {dpi}");
        
        try
        {
            Directory.CreateDirectory(outputFolder);

            using var pdfStream = File.OpenRead(pdfPath);
            var pageImages = Conversion.ToImages(pdfStream, options: new RenderOptions(Dpi: dpi));

            Console.WriteLine($"[PdfToImage] PDF opened and rendered.");

            var pageNumber = 0;
            foreach (var renderedPage in pageImages)
            {
                using var pageImage = renderedPage;
                pageNumber++;
                var imageFileName = $"page_{pageNumber}.png";
                var imageFullPath = Path.Combine(outputFolder, imageFileName);

                Console.WriteLine($"[PdfToImage] Rendered image size: {pageImage.Width}x{pageImage.Height}");

                using var enhancedBitmap = EnhanceImageForOcr(pageImage);
                using var outputStream = File.Create(imageFullPath);
                enhancedBitmap.Encode(outputStream, SKEncodedImageFormat.Png, quality: 100);

                Console.WriteLine($"[PdfToImage] Image saved to: {imageFullPath}");
                Console.WriteLine($"[PdfToImage] File size: {new FileInfo(imageFullPath).Length} bytes");

                imagePaths.Add(imageFullPath);
                Console.WriteLine($"[PdfToImage] Page {pageNumber} converted: {imageFileName}");
            }

            Console.WriteLine($"[PdfToImage] Conversion completed. {imagePaths.Count} images created.");

            return imagePaths;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PdfToImage] ERROR: {ex.Message}");
            Console.WriteLine($"[PdfToImage] Stack Trace: {ex.StackTrace}");
            throw;
        }
    }
    
    /// <summary>
    /// Enhances image quality for better OCR results.
    /// </summary>
    private SKBitmap EnhanceImageForOcr(SKBitmap original)
    {
        var width = original.Width;
        var height = original.Height;

        var result = new SKBitmap(width, height, SKColorType.Gray8, SKAlphaType.Opaque);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var pixel = original.GetPixel(x, y);

                var gray = (int)(pixel.Red * 0.299 + pixel.Green * 0.587 + pixel.Blue * 0.114);
                var contrasted = ApplyContrast(gray, 2.0f);
                var threshold = 180;
                var finalColor = contrasted < threshold ? (byte)0 : (byte)255;

                result.SetPixel(x, y, new SKColor(finalColor, finalColor, finalColor));
            }
        }

        return result;
    }

    /// <summary>
    /// Applies contrast enhancement to a grayscale value.
    /// </summary>
    private int ApplyContrast(int value, float contrast)
    {
        // Normalize to 0-1
        var normalized = value / 255.0f;
        
        // Apply contrast formula
        var enhanced = ((normalized - 0.5f) * contrast) + 0.5f;
        
        // Clamp and convert back
        return (int)(Math.Max(0, Math.Min(1, enhanced)) * 255);
    }
}
