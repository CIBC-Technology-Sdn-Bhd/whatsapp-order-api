using UglyToad.PdfPig;
using System.Text;

namespace DocumentProcessingApi.Services
{
	public class PdfTextExtractionService
	{
		private readonly IOcrService _ocrService;
		private readonly IPdfToImageService _pdfToImageService;

		public PdfTextExtractionService(IOcrService ocrService, IPdfToImageService pdfToImageService)
		{
			_ocrService = ocrService;
			_pdfToImageService = pdfToImageService;
		}

		public string ExtractText(string filePath)
		{
			Console.WriteLine($"[PdfPig Service] Starting PDF text extraction...");
			Console.WriteLine($"[PdfPig Service] PDF Path: {filePath}");
			
			var sb = new StringBuilder();

			try
			{
				using var document = PdfDocument.Open(filePath);
				Console.WriteLine($"[PdfPig Service] PDF opened successfully");
				Console.WriteLine($"[PdfPig Service] Total pages: {document.NumberOfPages}");

				foreach (var page in document.GetPages())
				{
					sb.AppendLine($"--- Page {page.Number} ---");
					sb.AppendLine(page.Text);
					sb.AppendLine();
					Console.WriteLine($"[PdfPig Service] Extracted text from page {page.Number}");
				}

				var result = sb.ToString();
				var charCount = result.Length;
				
				Console.WriteLine($"[PdfPig Service] Extraction completed. Total characters: {charCount}");
				
				// If extracted text is too short (less than 50 chars), it's likely a scanned PDF
				// Trigger OCR fallback for better results
				if (charCount < 50)
				{
					Console.WriteLine($"[PdfPig Service] Extracted text too short ({charCount} chars) - triggering OCR fallback...");
					return ExtractTextWithOcr(filePath);
				}
				
				return result;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[PdfPig Service] Direct extraction failed: {ex.Message}");
				Console.WriteLine($"[PdfPig Service] Falling back to OCR...");
				return ExtractTextWithOcr(filePath);
			}
		}

        /// <summary>
        /// Fallback method: converts PDF pages to images and runs OCR on each page
        /// Returns extracted text and outputs image paths for download
        /// </summary>
        public string ExtractTextWithImages(string filePath, out List<string> imagePaths)
        {
            Console.WriteLine($"[PdfPig Service] Starting OCR fallback for scanned PDF...");
            
            var sb = new StringBuilder();
            imagePaths = new List<string>();
            
            // Use a permanent folder for debugging and download (not deleted)
            var tempFolder = Path.Combine(Directory.GetCurrentDirectory(), "TempPdfImages");
            Directory.CreateDirectory(tempFolder);
            
            // Clean old images first
            try
            {
                foreach (var oldFile in Directory.GetFiles(tempFolder, "*.png"))
                {
                    File.Delete(oldFile);
                }
                Console.WriteLine($"[PdfPig Service] Cleaned old images from {tempFolder}");
            }
            catch { }
            
            try
            {
                // Convert PDF pages to images
                imagePaths = _pdfToImageService.ConvertPdfToImages(filePath, tempFolder);
                Console.WriteLine($"[PdfPig Service] Images created: {imagePaths.Count}");
                
                // Preserve page labels here, while the OCR service reuses a single engine internally.
                var ocrResults = _ocrService.ExtractTextFromImages(imagePaths);

                // Run OCR on each image
                for (int i = 0; i < imagePaths.Count; i++)
                {
                    var imagePath = imagePaths[i];
                    var pageNumber = Path.GetFileNameWithoutExtension(imagePath).Replace("page_", "");
                    Console.WriteLine($"[PdfPig Service] Processing image: {imagePath}");
                    Console.WriteLine($"[PdfPig Service] Image file exists: {File.Exists(imagePath)}");
                    Console.WriteLine($"[PdfPig Service] Image file size: {new FileInfo(imagePath).Length} bytes");
                    
                    sb.AppendLine($"--- Page {pageNumber} (OCR) ---");

                    var ocrText = i < ocrResults.Count ? ocrResults[i] : string.Empty;
                    Console.WriteLine($"[PdfPig Service] OCR returned {ocrText.Length} characters");
                    sb.AppendLine(ocrText);
                    sb.AppendLine();
                    
                    Console.WriteLine($"[PdfPig Service] OCR completed for page {pageNumber}");
                }
                
                var result = sb.ToString().Trim();
                Console.WriteLine($"[PdfPig Service] OCR fallback completed. Total characters: {result.Length}");
                
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PdfPig Service] OCR fallback ERROR: {ex.Message}");
                Console.WriteLine($"[PdfPig Service] Stack trace: {ex.StackTrace}");
                imagePaths = new List<string>();
                return $"Error during OCR fallback: {ex.Message}";
            }
        }

        /// <summary>
        /// Fallback method: converts PDF pages to images and runs OCR on each page (internal use)
        /// </summary>
        private string ExtractTextWithOcr(string filePath)
        {
            var imagePaths = new List<string>();
            return ExtractTextWithImages(filePath, out imagePaths);
        }
	}
}
