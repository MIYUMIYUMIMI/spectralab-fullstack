
using Microsoft.EntityFrameworkCore;
using SpectraLab.Api.Models;

namespace SpectraLab.Api.Data;

public class SpectraLabDbContext : DbContext
{
    public SpectraLabDbContext(
        DbContextOptions<SpectraLabDbContext> options
    ) : base(options)
    {
    }

    public DbSet<Measurement> Measurements => Set<Measurement>();

    public DbSet<SpectrumPoint> SpectrumPoints => Set<SpectrumPoint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Measurement>()
            .Property(m => m.Name)
            .HasMaxLength(100)
            .IsRequired();

        modelBuilder.Entity<Measurement>()
            .HasMany(m => m.Points)
            .WithOne()
            .HasForeignKey(p => p.MeasurementId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
