using Imagetextextraction.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Imagetextextraction.Backend.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<ScannedDocument> ScannedDocuments => Set<ScannedDocument>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }
}
