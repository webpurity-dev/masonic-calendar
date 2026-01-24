using Microsoft.EntityFrameworkCore;
using MasonicCalendar.Core.Domain;

namespace MasonicCalendar.Data;

/// <summary>
/// Entity Framework Core context for the Masonic Calendar application.
/// </summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();

    public DbSet<UnitLocation> UnitLocations => Set<UnitLocation>();

    public DbSet<Unit> Units => Set<Unit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure CalendarEvent
        modelBuilder.Entity<CalendarEvent>(entity =>
        {
            entity.HasKey(e => e.EventId);
            entity.Property(e => e.EventName).IsRequired();
            entity.Property(e => e.EventDate).IsRequired();
        });

        // Configure UnitLocation
        modelBuilder.Entity<UnitLocation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.AddressLine1).IsRequired();
        });

        // Configure Unit
        modelBuilder.Entity<Unit>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Number).IsRequired();
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.Location).IsRequired();
            entity.Property(e => e.LocationId).IsRequired();

            // Configure foreign key relationship
            entity.HasOne(u => u.LocationDetails)
                .WithMany()
                .HasForeignKey(u => u.LocationId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
