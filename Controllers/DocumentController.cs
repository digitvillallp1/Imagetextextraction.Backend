using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Text.Json;
using SixLabors.ImageSharp.Formats.Jpeg;
using Imagetextextraction.Backend.Data;
using Imagetextextraction.Backend.Hubs;
using Imagetextextraction.Backend.Models;
using Imagetextextraction.Backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Generic;

namespace Imagetextextraction.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly GeminiService _geminiService;
        private readonly IHubContext<PrescriptionHub> _hubContext; // We can keep using PrescriptionHub for now so frontend doesn't break
        private readonly ILogger<DocumentController> _logger;
        private readonly IMemoryCache _cache;

        public DocumentController(
            ApplicationDbContext context,
            GeminiService geminiService,
            IHubContext<PrescriptionHub> hubContext,
            ILogger<DocumentController> logger,
            IMemoryCache cache)
        {
            _context = context;
            _geminiService = geminiService;
            _hubContext = hubContext;
            _logger = logger;
            _cache = cache;
        }

        // Endpoint: POST /api/document/upload
        // Notice we changed route from /api/prescription/upload to /api/document/upload
        [HttpPost("upload")]
        public async Task<IActionResult> UploadDocument([FromForm] IFormFile file, [FromForm] string? connectionId, [FromForm] string? sessionId)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown_ip";
            var cacheKey = $"rate_limit_{ip}";

            var timestamps = _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(3);
                return new List<DateTime>();
            });

            timestamps?.RemoveAll(t => t < DateTime.UtcNow.AddHours(-3));

            if (timestamps != null && timestamps.Count >= 3)
            {
                return StatusCode(429, new { message = "You can only upload 3 documents per 3 hours. Please try again later." });
            }

            timestamps.Add(DateTime.UtcNow);
            _cache.Set(cacheKey, timestamps, TimeSpan.FromHours(3));

            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded");
            }

            try
            {
                async Task SendProgress(string message)
                {
                    if (!string.IsNullOrEmpty(connectionId))
                    {
                        _logger.LogInformation("SignalR Update to {ConnectionId}: {Msg}", connectionId, message);
                        await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveProgress", message);
                    }
                }

                await SendProgress("Uploading document...");

                byte[] fileBytes;
                using (var ms = new MemoryStream())
                {
                    await file.CopyToAsync(ms);
                    fileBytes = ms.ToArray();
                }

                await SendProgress("AI is reading the document...");
                var structuredResult = await _geminiService.ProcessDocumentAsync(fileBytes, file.ContentType);
                await SendProgress("Extracting text...");

                // Convert to Indian Standard Time (IST)
                DateTime utcNow = DateTime.UtcNow;
                TimeZoneInfo istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
                DateTime istTime = TimeZoneInfo.ConvertTimeFromUtc(utcNow, istZone);

                string docType = (structuredResult.documentType ?? "GENERAL").ToLower();
                string dateString = istTime.ToString("yyyy-MM-dd_HH-mm-ss");

                string? imagePath = null;
                try 
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    if (file.ContentType.StartsWith("image/"))
                    {
                        var fileName = $"{docType}_{dateString}.jpg";
                        var filePath = Path.Combine(uploadsFolder, fileName);
                        using (var image = Image.Load(fileBytes))
                        {
                            var encoder = new JpegEncoder { Quality = 60 };
                            image.Save(filePath, encoder);
                        }
                        imagePath = $"/uploads/{fileName}";
                    }
                    else if (file.ContentType == "application/pdf")
                    {
                        var fileName = $"{docType}_{dateString}.pdf";
                        var filePath = Path.Combine(uploadsFolder, fileName);
                        await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);
                        imagePath = $"/uploads/{fileName}";
                    }
                }
                catch (Exception fileEx)
                {
                    _logger.LogWarning(fileEx, "Failed to save file.");
                }

                DateTime? docDate = null;
                if (!string.IsNullOrEmpty(structuredResult.documentDate) && 
                    DateTime.TryParse(structuredResult.documentDate, out var parsedDate))
                {
                    docDate = DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);
                }

                var document = new ScannedDocument
                {
                    Id = Guid.NewGuid(),
                    DocumentType = structuredResult.documentType ?? "GENERAL",
                    PersonName = structuredResult.personName == "N/A" ? null : structuredResult.personName,
                    PhoneNumber = structuredResult.phoneNumber == "N/A" ? null : structuredResult.phoneNumber,
                    DocumentDate = docDate,
                    ExtractedText = string.IsNullOrEmpty(structuredResult.markdownText) ? structuredResult.rawText : structuredResult.markdownText,
                    ExtractedJson = JsonSerializer.Serialize(structuredResult.extractedData),
                    ImagePath = imagePath,
                    UploadedAt = DateTime.SpecifyKind(istTime, DateTimeKind.Utc),
                    SessionId = sessionId
                };

                await SendProgress("Almost done...");

                _context.ScannedDocuments.Add(document);
                await _context.SaveChangesAsync();

                await SendProgress("Scan completed successfully!");

                return Ok(document);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing document upload");
                
                if (!string.IsNullOrEmpty(connectionId))
                {
                    await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveProgress", $"Error: {ex.Message}");
                }

                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // Endpoint: GET /api/document/history
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory()
        {
            try
            {
                var history = await _context.ScannedDocuments
                    .OrderByDescending(d => d.UploadedAt)
                    .ToListAsync();

                return Ok(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching history");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDocument(Guid id)
        {
            try
            {
                var document = await _context.ScannedDocuments.FindAsync(id);
                if (document == null)
                {
                    return NotFound();
                }

                _context.ScannedDocuments.Remove(document);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting document");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

    }
}
