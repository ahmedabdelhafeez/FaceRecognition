using FaceRecognition.Entities;
using Microsoft.EntityFrameworkCore;

namespace FaceRecognition.Data;

public sealed class FaceDbContext : DbContext
{
    public FaceDbContext(DbContextOptions<FaceDbContext> options) : base(options) { }

    public DbSet<EmployeeFaceEmbedding> FaceEmbeddings => Set<EmployeeFaceEmbedding>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EmployeeFaceEmbedding>(e =>
        {
            e.HasKey(x => x.EmployeeId);
            e.Property(x => x.EmployeeName).IsRequired().HasMaxLength(200);
            e.Property(x => x.EmbeddingBytes).IsRequired();
            e.Property(x => x.CreatedAt).IsRequired();
            e.Property(x => x.UpdatedAt).IsRequired();
        });
    }
}