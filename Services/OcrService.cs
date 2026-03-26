using SkiaSharp;
using Tesseract;

namespace DocumentProcessingApi.Services;

public interface IOcrService
{
    string ExtractText(string filePath);
    List<string> ExtractTextFromImages(List<string> imagePaths);
}
 
public class OcrService : IOcrService
{
    private readonly string _tessdataPath;

    public OcrService()
    {
        // Get the current directory where the app is running
        var currentDirectory = Directory.GetCurrentDirectory();
        _tessdataPath = Path.Combine(currentDirectory, "Tessdata");
    }

    public string ExtractText(string filePath)
    {
        try
        {
            var preparedImagePath = PrepareImageForOcr(filePath);
            return ExtractTextFromImages(new List<string> { preparedImagePath }).FirstOrDefault() ?? string.Empty;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OCR Service] ERROR: {ex.Message}");
            Console.WriteLine($"[OCR Service] Stack trace: {ex.StackTrace}");
            return $"Error during OCR: {ex.Message}";
        }
    }

    public List<string> ExtractTextFromImages(List<string> imagePaths)
    {
        Console.WriteLine($"[OCR Service] Starting OCR processing...");
        Console.WriteLine($"[OCR Service] Tessdata Path: {_tessdataPath}");

        if (!Directory.Exists(_tessdataPath))
        {
            Console.WriteLine("[OCR Service] ERROR: Tessdata folder does not exist!");
            return new List<string> { "Error: Tessdata folder not found" };
        }

        var traineddataPath = Path.Combine(_tessdataPath, "eng.traineddata");
        if (!File.Exists(traineddataPath))
        {
            Console.WriteLine($"[OCR Service] ERROR: eng.traineddata not found at: {traineddataPath}");
            return new List<string> { $"Error: traineddata not found at {traineddataPath}" };
        }

        Console.WriteLine($"[OCR Service] Tessdata verified: {traineddataPath}");
        Console.WriteLine($"[OCR Service] Total images queued: {imagePaths.Count}");

        var results = new List<string>();

        try
        {
            using var engine = new TesseractEngine(_tessdataPath, "eng", EngineMode.Default);
            Console.WriteLine("[OCR Service] Tesseract engine initialized");

            foreach (var imagePath in imagePaths)
            {
                Console.WriteLine($"[OCR Service] Image Path: {imagePath}");
                Console.WriteLine($"[OCR Service] File exists: {File.Exists(imagePath)}");

                using var img = Pix.LoadFromFile(imagePath);
                Console.WriteLine("[OCR Service] Image loaded successfully");
                Console.WriteLine($"[OCR Service] Image dimensions: {img.Width}x{img.Height}");
                Console.WriteLine($"[OCR Service] Image depth: {img.Depth} bpp");

                using var page = engine.Process(img);
                Console.WriteLine("[OCR Service] OCR processing completed");
                Console.WriteLine($"[OCR Service] Confidence: {page.GetMeanConfidence():F2}%");

                var text = page.GetText()?.Trim() ?? string.Empty;
                Console.WriteLine($"[OCR Service] Raw text length: {text.Length}");
                Console.WriteLine($"[OCR Service] Raw text preview: {(text.Length > 100 ? text.Substring(0, 100) : text)}");

                results.Add(text);
            }

            return results;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OCR Service] ERROR: {ex.Message}");
            Console.WriteLine($"[OCR Service] Stack trace: {ex.StackTrace}");
            return new List<string> { $"Error during OCR: {ex.Message}" };
        }
    }

    private string PrepareImageForOcr(string filePath)
    {
        Console.WriteLine($"[OCR Service] Preparing image for OCR: {filePath}");

        using var inputStream = File.OpenRead(filePath);
        using var codec = SKCodec.Create(inputStream);
        if (codec == null)
        {
            Console.WriteLine("[OCR Service] Unable to decode image. Using original file.");
            return filePath;
        }

        var info = codec.Info;
        using var original = SKBitmap.Decode(codec);
        if (original == null)
        {
            Console.WriteLine("[OCR Service] Failed to load image bitmap. Using original file.");
            return filePath;
        }

        using var enhanced = EnhanceImageForOcr(original);

        var preparedFolder = Path.Combine(Directory.GetCurrentDirectory(), "TempOcrImages");
        Directory.CreateDirectory(preparedFolder);

        var preparedPath = Path.Combine(preparedFolder, $"{Path.GetFileNameWithoutExtension(filePath)}_prepared.png");
        using var outputStream = File.Create(preparedPath);
        enhanced.Encode(outputStream, SKEncodedImageFormat.Png, quality: 100);

        Console.WriteLine($"[OCR Service] Prepared OCR image saved: {preparedPath}");
        return preparedPath;
    }

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

    private int ApplyContrast(int value, float contrast)
    {
        var normalized = value / 255.0f;
        var enhanced = ((normalized - 0.5f) * contrast) + 0.5f;
        return (int)(Math.Max(0, Math.Min(1, enhanced)) * 255);
    }
}
