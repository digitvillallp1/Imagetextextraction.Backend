using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
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

namespace Imagetextextraction.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PrescriptionController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly GeminiService _geminiService;
        private readonly IHubContext<PrescriptionHub> _hubContext;
        private readonly ILogger<PrescriptionController> _logger;

        public PrescriptionController(
            ApplicationDbContext context,
            GeminiService geminiService,
            IHubContext<PrescriptionHub> hubContext,
            ILogger<PrescriptionController> logger)
        {
            _context = context;
            _geminiService = geminiService;
            _hubContext = hubContext;
            _logger = logger;
        }

        // Endpoint: POST /api/prescription/upload
        [HttpPost("upload")]
        public async Task<IActionResult> UploadPrescription([FromForm] IFormFile file, [FromForm] string? connectionId)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded");
            }

            try
            {
                // Helper method to send progress to client via SignalR
                async Task SendProgress(string message)
                {
                    if (!string.IsNullOrEmpty(connectionId))
                    {
                        _logger.LogInformation("SignalR Update to {ConnectionId}: {Msg}", connectionId, message);
                        await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveProgress", message);
                    }
                }

                await SendProgress("File uploaded. Reading buffer...");

                byte[] fileBytes;
                string? imagePath = null;
                
                using (var ms = new MemoryStream())
                {
                    await file.CopyToAsync(ms);
                    fileBytes = ms.ToArray();
                }

                // Image compression and saving logic
                try 
                {
                    if (file.ContentType.StartsWith("image/"))
                    {
                        await SendProgress("Compressing and saving image...");
                        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                        
                        var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}.jpg";
                        var filePath = Path.Combine(uploadsFolder, fileName);
                        
                        using (var image = Image.Load(fileBytes))
                        {
                            var encoder = new JpegEncoder { Quality = 60 }; // compress to 60% quality
                            image.Save(filePath, encoder);
                        }
                        
                        imagePath = $"/uploads/{fileName}";
                    }
                }
                catch (Exception imgEx)
                {
                    _logger.LogWarning(imgEx, "Failed to compress or save image.");
                }

                await SendProgress("Gemini AI analyzing the document...");

                var structuredResult = await _geminiService.ProcessDocumentAsync(fileBytes, file.ContentType);

                await SendProgress("Parsing document details...");

                // Parse Visit Date
                DateTime? visitDate = null;
                if (!string.IsNullOrEmpty(structuredResult.dateOfVisit) && 
                    DateTime.TryParse(structuredResult.dateOfVisit, out var parsedDate))
                {
                    visitDate = DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);
                }

                var now = DateTime.UtcNow;
                var prescription = new Prescription
                {
                    Id = Guid.NewGuid(),
                    ClinicName = structuredResult.clinicName,
                    ClinicPhone = structuredResult.clinicPhone,
                    DoctorName = structuredResult.doctorName,
                    DoctorRegistration = structuredResult.doctorRegistration,
                    PatientName = structuredResult.patientName,
                    PatientAge = structuredResult.patientAge,
                    PatientGender = structuredResult.patientGender,
                    PatientWeight = structuredResult.patientWeight,
                    Diagnosis = structuredResult.diagnosis,
                    DateOfVisit = visitDate,
                    RawText = structuredResult.rawText,
                    MarkdownText = structuredResult.markdownText,
                    ImagePath = imagePath,
                    CreatedAt = now,
                    UploadDate = now.Date,
                    UploadDay = now.Day,
                    UploadMonth = now.Month,
                    UploadYear = now.Year,
                    UploadTime = now.TimeOfDay,
                    Medications = structuredResult.medications.Select(m => new Medication
                    {
                        Id = Guid.NewGuid(),
                        Name = m.name,
                        Type = m.type,
                        Dosage = m.dosage,
                        Frequency = m.frequency,
                        Duration = m.duration,
                        Instructions = m.instructions
                    }).ToList()
                };

                await SendProgress("Finalizing results...");

                _context.Prescriptions.Add(prescription);
                await _context.SaveChangesAsync();

                await SendProgress("Scan completed successfully!");

                return Ok(prescription);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing prescription upload");
                
                if (!string.IsNullOrEmpty(connectionId))
                {
                    await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveProgress", $"Error: {ex.Message}");
                }

                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // Endpoint: GET /api/prescription/history
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory()
        {
            try
            {
                var history = await _context.Prescriptions
                    .Include(p => p.Medications)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();

                return Ok(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching history");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // Endpoint: POST /api/prescription/explain
        [HttpPost("explain")]
        public async Task<IActionResult> ExplainMedications([FromBody] ExplainRequest request)
        {
            if (string.IsNullOrEmpty(request.PrescriptionText))
            {
                return BadRequest("Prescription text is required");
            }

            try
            {
                var explanation = await _geminiService.GenerateExplanationAsync(request.PrescriptionText);
                return Ok(new { explanation });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating explanation");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // Endpoint: DELETE /api/prescription/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePrescription(Guid id)
        {
            try
            {
                var prescription = await _context.Prescriptions.FindAsync(id);
                if (prescription == null)
                {
                    return NotFound();
                }

                _context.Prescriptions.Remove(prescription);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting prescription");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }

    public class ExplainRequest
    {
        public string PrescriptionText { get; set; } = string.Empty;
    }
}
