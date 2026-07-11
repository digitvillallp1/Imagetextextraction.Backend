using System;

namespace Imagetextextraction.Backend.Models
{
    public class Medication
    {
        public Guid Id { get; set; }
        public Guid PrescriptionId { get; set; }
        
        // Medicine details
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "N/A"; // e.g. Syrup, Tablet, Capsule
        public string Dosage { get; set; } = "N/A"; // e.g. 5ml, 500mg
        public string Frequency { get; set; } = "N/A"; // e.g. TDS, Q6H, 1-0-1
        public string Duration { get; set; } = "N/A"; // e.g. 5 Days
        public string Instructions { get; set; } = "N/A"; // e.g. After Food
        
        // AI explanation
        public string? AiExplanation { get; set; }

        // Navigation property
        [System.Text.Json.Serialization.JsonIgnore]
        public Prescription? Prescription { get; set; }
    }
}
