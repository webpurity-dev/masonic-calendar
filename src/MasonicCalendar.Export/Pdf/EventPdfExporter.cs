using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using MasonicCalendar.Core.Domain;

namespace MasonicCalendar.Export.Pdf;

/// <summary>
/// Generates PDF calendar documents from event data using QuestPDF.
/// Community License: Free for non-profits and companies with <$1M revenue.
/// </summary>
public class EventPdfExporter
{
    static EventPdfExporter()
    {
        // Set Community License for this non-profit project
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public void ExportEventsToPdf(List<CalendarEvent> events, string outputPath)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(20);

                page.Header().Column(col =>
                {
                    col.Item().Text("Masonic Calendar Events").FontSize(24).Bold();
                    col.Item().PaddingBottom(10);
                });

                page.Content().Column(column =>
                {
                    // Group events by month
                    var eventsByMonth = events
                        .OrderBy(e => e.EventDate)
                        .GroupBy(e => new { e.EventDate.Year, e.EventDate.Month });

                    foreach (var monthGroup in eventsByMonth)
                    {
                        column.Item().PaddingBottom(15).Column(monthColumn =>
                        {
                            var monthName = new DateTime(monthGroup.Key.Year, monthGroup.Key.Month, 1)
                                .ToString("MMMM yyyy");
                            monthColumn.Item().Text(monthName).FontSize(14).Bold();

                            foreach (var evt in monthGroup)
                            {
                                monthColumn.Item().PaddingLeft(10).PaddingBottom(8).Column(eventColumn =>
                                {
                                    eventColumn.Item().Text($"{evt.EventDate:ddd, MMM d}")
                                        .FontSize(11).Bold();
                                    eventColumn.Item().Text(evt.EventName).FontSize(11);
                                    if (!string.IsNullOrEmpty(evt.Location))
                                        eventColumn.Item().Text($"Location: {evt.Location}")
                                            .FontSize(9);
                                    if (!string.IsNullOrEmpty(evt.Description))
                                        eventColumn.Item().Text($"Description: {evt.Description}")
                                            .FontSize(9);
                                });
                            }
                        });
                    }
                });

                page.Footer().AlignCenter().Text($"Generated on {DateTime.Now:f}").FontSize(9);
            });
        });
        
        document.GeneratePdf(outputPath);
    }
}
