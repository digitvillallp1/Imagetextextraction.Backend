using System;

namespace Imagetextextraction.Backend.Models
{
    public class ScannedDocument
    {
        public Guid Id { get; set; }
        
        /// <summary>
        /// e.g. "PRESCRIPTION", "INVOICE", "GENERAL"
        /// </summary>
        public string DocumentType { get; set; } = "GENERAL";

        // First-class queryable columns
        public string? PersonName { get; set; }
        public string? PhoneNumber { get; set; }
        
        /// <summary>
        /// The date printed on the document (Invoice Date, Visit Date, etc.)
        /// </summary>
        public DateTime? DocumentDate { get; set; }

        /// <summary>
        /// Unified extracted text (combining raw and markdown for simplicity)
        /// </summary>
        public string ExtractedText { get; set; } = string.Empty;

        /// <summary>
        /// Everything else (Medicines, Totals, Items) goes here as a JSON string
        /// </summary>
        public string? ExtractedJson { get; set; }

        public string? ImagePath { get; set; }

        /// <summary>
        /// Standardized upload timestamp in IST
        /// </summary>
        public DateTime UploadedAt { get; set; }
    }
}
