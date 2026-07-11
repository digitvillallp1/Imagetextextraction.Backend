using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Imagetextextraction.Backend.Services
{
    public class GeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly ILogger<GeminiService> _logger;

        public GeminiService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiKey = configuration["GEMINI_API_KEY"] ?? throw new ArgumentNullException("GEMINI_API_KEY is not configured.");
        }

        public class StructuredPrescriptionResult
        {
            public string clinicName { get; set; } = "N/A";
            public string clinicPhone { get; set; } = "N/A";
            public string doctorName { get; set; } = "N/A";
            public string doctorRegistration { get; set; } = "N/A";
            public string patientName { get; set; } = "N/A";
            public string patientAge { get; set; } = "N/A";
            public string patientGender { get; set; } = "N/A";
            public string patientWeight { get; set; } = "N/A";
            public string diagnosis { get; set; } = "N/A";
            public string? dateOfVisit { get; set; }
            public string rawText { get; set; } = string.Empty;
            public string markdownText { get; set; } = string.Empty;
            public List<StructuredMedication> medications { get; set; } = new();
        }

        public class StructuredMedication
        {
            public string name { get; set; } = string.Empty;
            public string type { get; set; } = "N/A";
            public string dosage { get; set; } = "N/A";
            public string frequency { get; set; } = "N/A";
            public string duration { get; set; } = "N/A";
            public string instructions { get; set; } = "N/A";
        }

        public async Task<StructuredPrescriptionResult> ProcessDocumentAsync(byte[] fileBytes, string mimeType)
        {
            try
            {
                var base64Data = Convert.ToBase64String(fileBytes);

                var prompt = @"Analyze the provided image or PDF of a doctor's prescription.
You must return a JSON object with the following structure:
{
  ""clinicName"": ""Clinic/Hospital name (string)"",
  ""clinicPhone"": ""Phone number if visible (string)"",
  ""doctorName"": ""Doctor name with titles (string)"",
  ""doctorRegistration"": ""Registration number if visible (string)"",
  ""patientName"": ""Patient name (string)"",
  ""patientAge"": ""Patient age (string)"",
  ""patientGender"": ""Patient gender (string)"",
  ""patientWeight"": ""Patient weight (string)"",
  ""diagnosis"": ""Diagnosis / Clinical description / Symptoms like URTI, cough (string)"",
  ""dateOfVisit"": ""Date of visit in YYYY-MM-DD format (string, or null if not found)"",
  ""rawText"": ""Exact text transcription of all handwriting on the prescription (string)"",
  ""markdownText"": ""Exact text transcription. DO NOT generate any tables. Just plain text (string)""
}

Ensure all keys are populated. If a value is not found, use 'N/A' or null where appropriate. Do not return any other text, only the JSON.";

                // Build request body with generationConfig requesting application/json
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new object[]
                            {
                                new { text = prompt },
                                new
                                {
                                    inlineData = new
                                    {
                                        mimeType = mimeType,
                                        data = base64Data
                                    }
                                }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        responseMimeType = "application/json"
                    }
                };

                var jsonRequest = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-pro:generateContent?key={_apiKey}";
                
                _logger.LogInformation("Sending request to Gemini API (JSON mode)...");
                var response = await _httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Gemini API error response: {Error}", errorContent);
                    throw new HttpRequestException($"Gemini API returned error: {response.StatusCode} - {response.ReasonPhrase}");
                }

                var responseString = await response.Content.ReadAsStringAsync();
                
                using var jsonDoc = JsonDocument.Parse(responseString);
                var root = jsonDoc.RootElement;

                if (root.TryGetProperty("candidates", out var candidates) && 
                    candidates.GetArrayLength() > 0 &&
                    candidates[0].TryGetProperty("content", out var candidateContent) &&
                    candidateContent.TryGetProperty("parts", out var parts) &&
                    parts.GetArrayLength() > 0 &&
                    parts[0].TryGetProperty("text", out var textProp))
                {
                    var rawJsonResult = textProp.GetString() ?? "{}";
                    
                    try 
                    {
                        // Sanitize invalid \u escape sequences
                        rawJsonResult = System.Text.RegularExpressions.Regex.Replace(rawJsonResult, @"\\u(?![0-9a-fA-F]{4})", @"\\u");
                        // Remove markdown codeblock if present
                        if (rawJsonResult.StartsWith("```json")) {
                            rawJsonResult = rawJsonResult.Substring(7);
                            if (rawJsonResult.EndsWith("```")) rawJsonResult = rawJsonResult.Substring(0, rawJsonResult.Length - 3);
                        }
                        
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var result = JsonSerializer.Deserialize<StructuredPrescriptionResult>(rawJsonResult, options);
                        
                        return result ?? new StructuredPrescriptionResult();
                    }
                    catch (Exception jsonEx)
                    {
                        _logger.LogWarning(jsonEx, "Failed to parse Gemini JSON. Returning raw text as fallback.");
                        
                        var cleanText = rawJsonResult;
                        
                        // Try to extract just the markdownText manually
                        int startIdx = rawJsonResult.IndexOf("\"markdownText\": \"");
                        if (startIdx != -1)
                        {
                            startIdx += 17;
                            int endIdx = rawJsonResult.IndexOf("\",", startIdx);
                            // Look for the next key
                            int medIdx = rawJsonResult.IndexOf("\"medications\":", startIdx);
                            if (medIdx != -1) 
                            {
                                endIdx = rawJsonResult.LastIndexOf("\",", medIdx);
                            }

                            if (endIdx != -1 && endIdx > startIdx)
                            {
                                cleanText = rawJsonResult.Substring(startIdx, endIdx - startIdx);
                                cleanText = cleanText.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");
                            }
                        }
                        
                        if (cleanText == rawJsonResult)
                        {
                            // If we couldn't extract, strip all JSON keys to make it readable
                            cleanText = System.Text.RegularExpressions.Regex.Replace(cleanText, @"\""[a-zA-Z]+\""\s*:\s*", "");
                            cleanText = System.Text.RegularExpressions.Regex.Replace(cleanText, @"[{}\[\]\"",]", "");
                            cleanText = cleanText.Replace("\\n", "\n");
                        }

                        return new StructuredPrescriptionResult 
                        {
                            rawText = cleanText,
                            markdownText = $"*Note: The AI struggled to format the result perfectly. Here is the raw extracted text:*\n\n{cleanText}",
                            medications = new List<StructuredMedication>()
                        };
                    }
                }

                throw new Exception("Failed to parse response structure from Gemini API");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Gemini API");
                throw;
            }
        }

        public async Task<string> GenerateExplanationAsync(string prescriptionText)
        {
            try
            {
                var prompt = $@"You are a helpful and experienced medical assistant and pharmacist.
Review the following digitized doctor prescription text:

{prescriptionText}

For each medication mentioned in the prescription, provide a brief, easy-to-understand explanation covering:
1. **What it is**: (e.g., ""Calpol is paracetamol, used to treat fever and mild-to-moderate pain."")
2. **Standard Usage Instructions**: What the frequency means (e.g., ""Q6H means every 6 hours; TDS means three times a day"").
3. **Important Safety Warnings**: Side effects, precautions (e.g., ""Do not take on an empty stomach"", ""Consult a doctor if symptoms persist"").

Format your explanation in beautiful Markdown.
Include a prominent warning at the beginning:
""**Disclaimer**: This explanation is for educational purposes only. Always consult your doctor or pharmacist before starting any medication.""";

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new object[]
                            {
                                new { text = prompt }
                            }
                        }
                    }
                };

                var jsonRequest = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-pro:generateContent?key={_apiKey}";
                var response = await _httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Gemini API returned error: {response.StatusCode}");
                }

                var responseString = await response.Content.ReadAsStringAsync();
                using var jsonDoc = JsonDocument.Parse(responseString);
                
                return jsonDoc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString() ?? "Could not generate medication explanation.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Gemini API for medication explanation");
                throw;
            }
        }
    }
}
