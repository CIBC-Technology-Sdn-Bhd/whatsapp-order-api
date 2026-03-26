using DocumentProcessingApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// PDF Services
builder.Services.AddScoped<PdfDetectionService>();
builder.Services.AddScoped<IPdfToImageService, PdfToImageService>();
builder.Services.AddScoped<PdfTextExtractionService>();

// OCR Services
builder.Services.AddScoped<IOcrService, OcrService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();