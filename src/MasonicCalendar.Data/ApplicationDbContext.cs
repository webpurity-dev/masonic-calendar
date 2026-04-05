using Microsoft.EntityFrameworkCore;
using MasonicCalendar.Core.Domain;

namespace MasonicCalendar.Data;

/// <summary>
/// Entity Framework Core context for the Masonic Calendar application.
/// </summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();

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
    }
}
