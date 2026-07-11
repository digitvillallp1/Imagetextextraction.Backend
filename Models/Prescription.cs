using System;
using System.Collections.Generic;

namespace Imagetextextraction.Backend.Models
{
    public class Prescription
    {
        public Guid Id { get; set; }
        
        // Clinic details
        public string? ClinicName { get; set; }
        public string? ClinicPhone { get; set; }
        
        // Doctor details
        public string? DoctorName { get; set; }
        public string? DoctorRegistration { get; set; }
        
        // Patient details
        public string? PatientName { get; set; }
        public string? PatientAge { get; set; }
        public string? PatientGender { get; set; }
        public string? PatientWeight { get; set; }
        
        // Diagnosis/clinical description
        public string? Diagnosis { get; set; }
        
        public DateTime? DateOfVisit { get; set; }
        public string RawText { get; set; } = string.Empty;
        public string MarkdownText { get; set; } = string.Empty;
        
        public string? ImagePath { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UploadDate { get; set; }
        public int UploadDay { get; set; }
        public int UploadMonth { get; set; }
        public int UploadYear { get; set; }
        public TimeSpan UploadTime { get; set; }

        // Navigation property
        public List<Medication> Medications { get; set; } = new();
    }
}
