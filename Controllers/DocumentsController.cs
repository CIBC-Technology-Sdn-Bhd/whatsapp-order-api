using DocumentProcessingApi.Models.Enums;
using DocumentProcessingApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace DocumentProcessingApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentsController : ControllerBase
    {
        private readonly PdfDetectionService _pdfDetectionService;
        private readonly PdfTextExtractionService _pdfTextExtractionService;
        private readonly IOcrService _ocrService;
        private readonly IPdfToImageService _pdfToImageService;

        public DocumentsController(
            PdfDetectionService pdfDetectionService,
            PdfTextExtractionService pdfTextExtractionService,
            IOcrService ocrService,
            IPdfToImageService pdfToImageService)
        {
            _pdfDetectionService = pdfDetectionService;
            _pdfTextExtractionService = pdfTextExtractionService;
            _ocrService = ocrService;
            _pdfToImageService = pdfToImageService;
        }

        [HttpPost("process")]
        public async Task<IActionResult> ProcessDocument(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            
            // Supported image extensions
            var imageExtensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tiff", ".tif" };
            bool isImage = imageExtensions.Contains(extension);

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "TempUploads");
            Directory.CreateDirectory(uploadsFolder);

            var savedFileName = $"{Guid.NewGuid()}{extension}";
            var savedPath = Path.Combine(uploadsFolder, savedFileName);

            await using (var stream = new FileStream(savedPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            Console.WriteLine($"========================================");
            Console.WriteLine($"Processing file: {file.FileName}");
            Console.WriteLine($"File extension: {extension}");
            Console.WriteLine($"File size: {file.Length} bytes");
            Console.WriteLine($"========================================");

            string extractedText;
            string documentType;
            string processingMethod;
            List<string> convertedImagePaths = null;

            if (isImage)
            {
                Console.WriteLine($"[OCR] Image detected - Using Tesseract OCR");
                extractedText = _ocrService.ExtractText(savedPath);
                documentType = "Image";
                processingMethod = "Tesseract OCR";
            }
            else if (extension == ".pdf")
            {
                Console.WriteLine($"[PDF] PDF detected - Detecting PDF type...");
                var pdfKind = _pdfDetectionService.DetectPdfKind(savedPath);

                if (pdfKind == PdfKind.DigitalTextPdf)
                {
                    Console.WriteLine($"[PDF] Digital PDF detected - Using PdfPig");
                    extractedText = _pdfTextExtractionService.ExtractText(savedPath);
                    documentType = "Digital PDF";
                    processingMethod = "PdfPig";
                }
                else if (pdfKind == PdfKind.ScannedPdf)
                {
                    Console.WriteLine($"[PDF] Scanned PDF detected - Using OCR fallback");
                    var result = _pdfTextExtractionService.ExtractTextWithImages(savedPath, out convertedImagePaths);
                    extractedText = result;
                    documentType = "Scanned PDF";
                    processingMethod = "PDFtoImage + Tesseract OCR";
                }
                else
                {
                    Console.WriteLine($"[PDF] Unknown PDF type");
                    extractedText = "Unable to determine PDF type.";
                    documentType = "Unknown PDF";
                    processingMethod = "None";
                }
            }
            else
            {
                Console.WriteLine($"[ERROR] Unsupported file type: {extension}");
                return BadRequest($"Unsupported file type: {extension}. Supported types: PDF, PNG, JPG, JPEG, BMP, TIFF");
            }

            Console.WriteLine($"========================================");
            Console.WriteLine($"Processing completed!");
            Console.WriteLine($"Document Type: {documentType}");
            Console.WriteLine($"Method Used: {processingMethod}");
            Console.WriteLine($"Extracted Text Length: {extractedText?.Length ?? 0} characters");
            Console.WriteLine($"========================================");

            // Build image URLs for downloaded
            var imageUrls = new List<string>();
            if (convertedImagePaths != null && convertedImagePaths.Count > 0)
            {
                foreach (var imgPath in convertedImagePaths)
                {
                    var imgName = Path.GetFileName(imgPath);
                    imageUrls.Add($"/api/documents/images/{imgName}");
                }
            }

            return Ok(new
            {
                success = true,
                originalFileName = file.FileName,
                documentType,
                processingMethod,
                extractedText,
                convertedImages = imageUrls
            });
        }

        /// <summary>
        /// Download converted PDF-to-image files
        /// </summary>
        [HttpGet("images/{imageName}")]
        public IActionResult GetConvertedImage(string imageName)
        {
            var tempImagesFolder = Path.Combine(Directory.GetCurrentDirectory(), "TempPdfImages");
            var imagePath = Path.Combine(tempImagesFolder, imageName);
            
            if (!System.IO.File.Exists(imagePath))
            {
                return NotFound($"Image '{imageName}' not found.");
            }
            
            return PhysicalFile(imagePath, "image/png");
        }
    }
}
