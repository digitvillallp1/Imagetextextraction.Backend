using Imagetextextraction.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Imagetextextraction.Backend.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Prescription> Prescriptions => Set<Prescription>();
        public DbSet<Medication> Medications => Set<Medication>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Prescription to Medications relationship
            modelBuilder.Entity<Prescription>()
                .HasMany(p => p.Medications)
                .WithOne(m => m.Prescription)
                .HasForeignKey(m => m.PrescriptionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
