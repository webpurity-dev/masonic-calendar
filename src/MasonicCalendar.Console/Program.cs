using MasonicCalendar.Core.Services;
using MasonicCalendar.Core.Domain;
using MasonicCalendar.Export.Pdf;
using MasonicCalendar.Export.Html;
using QuestPDF.Infrastructure;

// Configure QuestPDF license for community use (non-profit)
QuestPDF.Settings.License = LicenseType.Community;

// Get the project root directory (up three levels from the bin directory)
var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var dataPath = Path.Combine(projectRoot, "data");

// Determine data source (default to v1)
var dataSource = "v1";
var sourceIndex = Array.IndexOf(args, "-source");
if (sourceIndex != -1 && sourceIndex + 1 < args.Length)
{
    var source = args[sourceIndex + 1].ToLower();
    if (source == "hermes" || source == "v1")
    {
        dataSource = source;
    }
    else
    {
        Console.WriteLine($"❌ Invalid data source: {source}. Use 'hermes' or 'v1'.");
        return 1;
    }
}

var eventsPath = Path.Combine(dataPath, "sample-events.csv");
var unitsPath = Path.Combine(dataPath, dataSource == "hermes" ? "sample-units.csv" : "sample-units.csv");
var locationsPath = Path.Combine(dataPath, "sample-unit-locations.csv");
var officersPath = Path.Combine(dataPath, "sample-officers.csv");
var unitOfficersPath = Path.Combine(dataPath, dataSource == "hermes" ? "hermes-export.csv" : "sample-unit-officers.csv");
var unitPastMastersPath = Path.Combine(dataPath, dataSource == "hermes" ? "hermes-export.csv" : "sample-unit-pmo.csv");
var unitPMIPath = Path.Combine(dataPath, dataSource == "hermes" ? "hermes-export.csv" : "sample-unit-pmi.csv");
var unitMembersPath = Path.Combine(dataPath, dataSource == "hermes" ? "hermes-export.csv" : "sample-unit-members.csv");
var unitHonraryPath = Path.Combine(dataPath, dataSource == "hermes" ? "hermes-export.csv" : "sample-unit-honorary.csv");

// Output directory (in the project root)
var outputDir = Path.Combine(projectRoot, "output");
if (!Directory.Exists(outputDir))
    Directory.CreateDirectory(outputDir);

// Meetings calendar CLI switch
if (args.Contains("--meetings-calendar"))
{
    // Check for output format (default to PDF)
    var meetingsOutputFormat = "pdf";
    var outputIndex = Array.IndexOf(args, "--output");
    if (outputIndex != -1 && outputIndex + 1 < args.Length)
    {
        var format = args[outputIndex + 1].ToLower();
        if (format == "pdf" || format == "html")
        {
            meetingsOutputFormat = format;
        }
    }
    
    // Check for page size (default to A6)
    var meetingsPageSize = "A6";
    var pageSizeIndex = Array.IndexOf(args, "--pagesize");
    if (pageSizeIndex != -1 && pageSizeIndex + 1 < args.Length)
    {
        var size = args[pageSizeIndex + 1].ToUpper();
        if (size == "A4" || size == "A5" || size == "A6")
        {
            meetingsPageSize = size;
        }
    }
    
    // Check for include Sundays (default to false)
    var includeSundays = args.Contains("--incSunday");
    
    // Check for landscape orientation (default to false for portrait)
    var isLandscape = args.Contains("--landscape");
    
    // Check for from-date parameter (MM-YYYY format, defaults to current month)
    var today = DateOnly.FromDateTime(DateTime.Now);
    DateOnly calendarStartDate = new DateOnly(today.Year, today.Month, 1);
    var fromDateIndex = Array.IndexOf(args, "--from-date");
    if (fromDateIndex != -1 && fromDateIndex + 1 < args.Length)
    {
        var fromDateStr = args[fromDateIndex + 1];
        // Parse MM-YYYY format
        if (fromDateStr.Contains("-") && fromDateStr.Split('-').Length == 2)
        {
            var parts = fromDateStr.Split('-');
            if (int.TryParse(parts[0], out int month) && int.TryParse(parts[1], out int year))
            {
                if (month >= 1 && month <= 12 && year > 1900 && year < 2100)
                {
                    calendarStartDate = new DateOnly(year, month, 1);
                }
            }
        }
    }
    
    var meetingsPath = Path.Combine(dataPath, "sample-unit-meetings.csv");
    var meetingsIngestor = new CsvIngestorService();
    var meetingsResult = meetingsIngestor.ReadUnitMeetingsFromCsv(meetingsPath);
    if (!meetingsResult.Success)
    {
        Console.WriteLine($"❌ Error reading meetings: {meetingsResult.Error}");
        return 1;
    }
    
    // Read units to lookup unit numbers and names
    var meetingsUnitsResult = meetingsIngestor.ReadUnitsFromCsv(unitsPath);
    if (!meetingsUnitsResult.Success)
    {
        Console.WriteLine($"❌ Error reading units: {meetingsUnitsResult.Error}");
        return 1;
    }
    
    Console.WriteLine("🗓️  Masonic Calendar - Meetings Calendar Generator");
    Console.WriteLine($"==========================================");
    Console.WriteLine($"Reading meetings data...");
    Console.WriteLine($"✅ Loaded {meetingsResult.Data!.Count} meeting series");
    Console.WriteLine($"✅ Loaded {meetingsUnitsResult.Data!.Count} units");
    
    // Calculate end date (12 months after start)
    DateOnly calendarEndDate = calendarStartDate.AddMonths(12).AddDays(-1);
    
    // Expand meetings for the range. Need to consider both years involved
    var expandedDates = new List<(MasonicCalendar.Core.Domain.UnitMeeting, DateOnly)>();
    
    // Generate for the start year and the next year (to cover year boundaries)
    foreach (var year in new[] { calendarStartDate.Year, calendarStartDate.AddMonths(12).Year })
    {
        var yearExpanded = MeetingRecurrenceExpander.ExpandMeetings(meetingsResult.Data!, year, calendarStartDate);
        expandedDates.AddRange(yearExpanded);
    }
    
    // Filter to only include dates within the 12-month range and remove duplicates
    var expanded = expandedDates
        .Where(x => x.Item2 >= calendarStartDate && x.Item2 <= calendarEndDate)
        .DistinctBy(x => (x.Item1.Id, x.Item2))
        .ToList();
    
    Console.WriteLine($"✅ Generated {expanded.Count} calendar dates from recurrence rules");
    Console.WriteLine($"📅 Calendar period: {calendarStartDate:MMM yyyy} to {calendarEndDate:MMM yyyy}\n");
    
    // Group meetings by unit and export to CSV
    var meetingUnitDict = meetingsUnitsResult.Data!.ToDictionary(u => u.Id);
    var expandedByUnit = expanded
        .GroupBy(x => x.Item1.UnitId)
        .OrderBy(g => SortKey(g))
        .ToList();
    
    int SortKey(IGrouping<Guid, (MasonicCalendar.Core.Domain.UnitMeeting, DateOnly)> g)
    {
        if (meetingUnitDict.TryGetValue(g.Key, out var unit))
        {
            try
            {
                return Convert.ToInt32(unit.Number);
            }
            catch { }
        }
        return 999999;
    }
    
    // Export to CSV
    var csvDateLabel = $"{calendarStartDate:MM-yyyy}";
    var csvPath = Path.Combine(outputDir, $"meetings-{csvDateLabel}.csv");
    using (var writer = new System.IO.StreamWriter(csvPath))
    {
        writer.WriteLine("Unit Number,Unit Name,Unit Type,Meeting Date,Meeting Title");
        
        foreach (var group in expandedByUnit)
        {
            if (meetingUnitDict.TryGetValue(group.Key, out var unit))
            {
                foreach (var item in group.OrderBy(x => x.Item2))
                {
                    var date = item.Item2.ToString("yyyy-MM-dd");
                    var title = item.Item1.Title.Replace("\"", "\"\""); // Escape quotes
                    writer.WriteLine($"\"{unit.Number}\",\"{unit.Name}\",\"{unit.UnitType}\",\"{date}\",\"{title}\"");
                }
            }
        }
    }
    
    Console.WriteLine($"✅ Exported meetings to CSV: {csvPath}\n");
    
    Console.WriteLine($"\n==========================================");
    var sundayLabel = includeSundays ? "-withsunday" : "";
    if (meetingsOutputFormat == "html")
    {
        Console.WriteLine("Generating meetings calendar HTML...");
        var htmlDateLabel = $"{calendarStartDate:MM-yyyy}";
        var meetingsOutputPath = Path.Combine(outputDir, $"meetings-output-{htmlDateLabel}{sundayLabel}.html");
        var meetingsExporter = new MeetingsCalendarHtmlExporter();
        meetingsExporter.ExportMeetingsToHtml(meetingsResult.Data!, calendarStartDate, meetingsOutputPath, meetingsUnitsResult.Data, includeSundays);
        Console.WriteLine($"✅ Meetings calendar HTML generated: {meetingsOutputPath}");
    }
    else
    {
        Console.WriteLine("Generating meetings calendar PDF...");
        var orientationLabel = isLandscape ? "landscape" : "portrait";
        var pdfDateLabel = $"{calendarStartDate:MM-yyyy}";
        var meetingsOutputPath = Path.Combine(outputDir, $"meetings-output-{pdfDateLabel}-{meetingsPageSize}-{orientationLabel}{sundayLabel}.pdf");
        var meetingsExporter = new MeetingsCalendarExporter();
        meetingsExporter.ExportMeetingsToPdf(meetingsResult.Data!, calendarStartDate, meetingsOutputPath, meetingsUnitsResult.Data, meetingsPageSize, includeSundays, isLandscape);
        Console.WriteLine($"✅ Meetings calendar PDF generated: {meetingsOutputPath}");
    }
    
    Console.WriteLine($"\n✨ Meetings calendar completed successfully!");
    return 0;
}

// Parse command-line arguments
var outputFormat = "pdf"; // default to PDF
int? filterUnitNumber = null; // default to null (all units)
string? filterUnitType = null; // default to null (all unit types)
var pageSize = "A6"; // default to A6 (A4, A5, A6)

if (args.Length > 0)
{
    var outputIndex = Array.IndexOf(args, "--output");
    if (outputIndex != -1 && outputIndex + 1 < args.Length)
    {
        var format = args[outputIndex + 1].ToLower();
        if (format == "pdf" || format == "html")
        {
            outputFormat = format;
        }
        else
        {
            Console.WriteLine($"❌ Invalid output format: {format}. Use 'pdf' or 'html'.");
            return 1;
        }
    }

    var pageSizeIndex = Array.IndexOf(args, "--pagesize");
    if (pageSizeIndex != -1 && pageSizeIndex + 1 < args.Length)
    {
        var size = args[pageSizeIndex + 1].ToUpper();
        if (size == "A4" || size == "A5" || size == "A6")
        {
            pageSize = size;
        }
        else
        {
            Console.WriteLine($"❌ Invalid page size: {size}. Use 'A4', 'A5', or 'A6'.");
            return 1;
        }
    }

    // Check for unit type filter (e.g., --unit-type craft)
    var unitTypeIndex = Array.IndexOf(args, "--unit-type");
    if (unitTypeIndex != -1 && unitTypeIndex + 1 < args.Length)
    {
        var typeArg = args[unitTypeIndex + 1].ToLower();
        if (typeArg == "craft" || typeArg == "royalarch")
        {
            filterUnitType = typeArg == "craft" ? "Craft" : "RoyalArch";
        }
        else
        {
            Console.WriteLine($"❌ Invalid unit type: {typeArg}. Use 'craft' or 'royalarch'.");
            return 1;
        }
    }

    // Check for unit number filter (e.g., --6827)
    var unitFilterArg = args.FirstOrDefault(a => a.StartsWith("--") && int.TryParse(a.Substring(2), out _));
    if (unitFilterArg != null && int.TryParse(unitFilterArg.Substring(2), out var unitNum))
    {
        filterUnitNumber = unitNum;
    }
}

// Generate filename based on unit filter and page size
var filenameIdentifier = filterUnitNumber.HasValue ? filterUnitNumber.Value.ToString() : (filterUnitType != null ? filterUnitType.ToLower() : "all-units");
var sourceLabel = dataSource == "hermes" ? "hermes" : "v1";
var unitsOutputPath = Path.Combine(outputDir, $"units-output-{filenameIdentifier}-{sourceLabel}-{pageSize}.{outputFormat}");

Console.WriteLine("🗓️  Masonic Calendar - CSV to Output Converter");
Console.WriteLine($"==========================================");
Console.WriteLine($"Output Format: {outputFormat.ToUpper()}");
Console.WriteLine($"Data Source: {(dataSource == "hermes" ? "Hermes Export (v2)" : "Standard CSV (v1)")}\n");

var ingestor = new CsvIngestorService();

// Read Calendar Events
Console.WriteLine("Reading calendar events...");
var eventsResult = ingestor.ReadEventsFromCsv(eventsPath);
if (!eventsResult.Success)
{
    Console.WriteLine($"❌ Error reading events: {eventsResult.Error}");
    return 1;
}
Console.WriteLine($"✅ Loaded {eventsResult.Data!.Count} events");

// Read Locations
Console.WriteLine("Reading unit locations...");
var locationsResult = ingestor.ReadLocationsFromCsv(locationsPath);
if (!locationsResult.Success)
{
    Console.WriteLine($"❌ Error reading locations: {locationsResult.Error}");
    return 1;
}
var locationDict = locationsResult.Data!.ToDictionary(l => l.Id);
Console.WriteLine($"✅ Loaded {locationDict.Count} locations");

// Read Units
Console.WriteLine("Reading units...");
var unitsResult = ingestor.ReadUnitsFromCsv(unitsPath);
if (!unitsResult.Success)
{
    Console.WriteLine($"❌ Error reading units: {unitsResult.Error}");
    return 1;
}
var unitIdDict = unitsResult.Data!.ToDictionary(u => u.Id);
Console.WriteLine($"✅ Loaded {unitsResult.Data!.Count} units");

// Read Hermes export if using that data source
Result<HermesExportData>? hermesResult = null;
if (dataSource == "hermes")
{
    Console.WriteLine("Reading Hermes export CSV...");
    var hermesIngestor = new HermesExportIngestorService();
    hermesResult = hermesIngestor.ReadHermesExportCsv(unitOfficersPath);
    if (!hermesResult.Success)
    {
        Console.WriteLine($"❌ Error reading Hermes export: {hermesResult.Error}");
        return 1;
    }
    Console.WriteLine($"✅ Loaded Hermes data: {hermesResult.Data!.UnitOfficers.Count} officers, " +
        $"{hermesResult.Data!.UnitPastMasters.Count} PMOs, {hermesResult.Data!.UnitPMI.Count} PMIs, " +
        $"{hermesResult.Data!.UnitMembers.Count} members, {hermesResult.Data!.UnitHonrary.Count} honorary");
}

// Read Officers (only needed for v1)
List<UnitOfficer> unitOfficersData;
Dictionary<Guid, Officer> officerDict;

if (dataSource == "v1")
{
    Console.WriteLine("Reading officers...");
    var officersResult = ingestor.ReadOfficersFromCsv(officersPath);
    if (!officersResult.Success)
    {
        Console.WriteLine($"❌ Error reading officers: {officersResult.Error}");
        return 1;
    }
    officerDict = officersResult.Data!.ToDictionary(o => o.Id);
    Console.WriteLine($"✅ Loaded {officerDict.Count} officer positions");

    Console.WriteLine("Reading unit officers...");
    var unitOfficersResult = ingestor.ReadUnitOfficersFromCsv(unitOfficersPath);
    if (!unitOfficersResult.Success)
    {
        Console.WriteLine($"❌ Error reading unit officers: {unitOfficersResult.Error}");
        return 1;
    }
    unitOfficersData = unitOfficersResult.Data!;
    
    // For v1 data, populate PosNo from Officer.Order and Position from Officer.Abbreviation
    foreach (var uo in unitOfficersData)
    {
        if (officerDict.TryGetValue(uo.OfficerId, out var officer))
        {
            if (uo.PosNo == 0)
            {
                uo.PosNo = officer.Order;
            }
            if (string.IsNullOrWhiteSpace(uo.Position))
            {
                uo.Position = officer.Abbreviation;
            }
        }
    }
    
    Console.WriteLine($"✅ Loaded {unitOfficersData.Count} unit officer assignments");
}
else
{
    unitOfficersData = new List<UnitOfficer>();
    officerDict = new Dictionary<Guid, Officer>();
    Console.WriteLine("Reading unit officers from Hermes export...");
    
    // Create mapping from unit number to unit ID
    var unitNumberToId = new Dictionary<int, Guid>();
    foreach (var unit in unitsResult.Data!)
    {
        if (!unitNumberToId.ContainsKey(unit.Number))
            unitNumberToId[unit.Number] = unit.Id;
    }
    
    // Resolve unit IDs for all hermes data
    var hermesData = hermesResult!.Data!;
    var resolvedOfficers = new List<UnitOfficer>();
    foreach (var (officer, unitNum) in hermesData.UnitOfficers)
    {
        if (unitNumberToId.TryGetValue(unitNum, out var unitId))
        {
            officer.UnitId = unitId;
        }
        resolvedOfficers.Add(officer);
    }
    
    unitOfficersData = resolvedOfficers;
    Console.WriteLine($"✅ Loaded {unitOfficersData.Count} unit officer assignments");
}

// Read Unit Past Masters
Console.WriteLine("Reading unit past masters...");
List<UnitPastMaster> unitPastMastersData;
if (dataSource == "v1")
{
    var unitPastMastersResult = ingestor.ReadUnitPastMastersFromCsv(unitPastMastersPath);
    if (!unitPastMastersResult.Success)
    {
        Console.WriteLine($"❌ Error reading unit past masters: {unitPastMastersResult.Error}");
        return 1;
    }
    unitPastMastersData = unitPastMastersResult.Data!;
}
else
{
    var unitNumberToId = unitsResult.Data!.GroupBy(u => u.Number).ToDictionary(g => g.Key, g => g.First().Id);
    var hermesData = hermesResult!.Data!;
    var resolvedPastMasters = new List<UnitPastMaster>();
    foreach (var (pastMaster, unitNum) in hermesData.UnitPastMasters)
    {
        if (unitNumberToId.TryGetValue(unitNum, out var unitId))
        {
            pastMaster.UnitId = unitId;
        }
        resolvedPastMasters.Add(pastMaster);
    }
    unitPastMastersData = resolvedPastMasters;
}
Console.WriteLine($"✅ Loaded {unitPastMastersData.Count} unit past master records");

// Read Unit PMI (Joining Past Masters)
Console.WriteLine("Reading joining past masters...");
List<UnitPMI> unitPMIData;
if (dataSource == "v1")
{
    var unitPMIResult = ingestor.ReadUnitPMIFromCsv(unitPMIPath);
    if (!unitPMIResult.Success)
    {
        Console.WriteLine($"❌ Error reading joining past masters: {unitPMIResult.Error}");
        return 1;
    }
    unitPMIData = unitPMIResult.Data!;
}
else
{
    var unitNumberToId = unitsResult.Data!.GroupBy(u => u.Number).ToDictionary(g => g.Key, g => g.First().Id);
    var hermesData = hermesResult!.Data!;
    var resolvedPMI = new List<UnitPMI>();
    foreach (var (pmi, unitNum) in hermesData.UnitPMI)
    {
        if (unitNumberToId.TryGetValue(unitNum, out var unitId))
        {
            pmi.UnitId = unitId;
        }
        resolvedPMI.Add(pmi);
    }
    unitPMIData = resolvedPMI;
}
Console.WriteLine($"✅ Loaded {unitPMIData.Count} joining past master records");

// Read Unit Members
Console.WriteLine("Reading members...");
List<UnitMember> unitMembersData;
if (dataSource == "v1")
{
    var unitMembersResult = ingestor.ReadUnitMembersFromCsv(unitMembersPath);
    if (!unitMembersResult.Success)
    {
        Console.WriteLine($"❌ Error reading members: {unitMembersResult.Error}");
        return 1;
    }
    unitMembersData = unitMembersResult.Data!;
}
else
{
    var unitNumberToId = unitsResult.Data!.GroupBy(u => u.Number).ToDictionary(g => g.Key, g => g.First().Id);
    var hermesData = hermesResult!.Data!;
    var resolvedMembers = new List<UnitMember>();
    foreach (var (member, unitNum) in hermesData.UnitMembers)
    {
        if (unitNumberToId.TryGetValue(unitNum, out var unitId))
        {
            member.UnitId = unitId;
        }
        resolvedMembers.Add(member);
    }
    unitMembersData = resolvedMembers;
}
Console.WriteLine($"✅ Loaded {unitMembersData.Count} member records");

// Read Unit Honorary Members
Console.WriteLine("Reading honorary members...");
List<UnitHonrary> unitHonraryData;
if (dataSource == "v1")
{
    var unitHonraryResult = ingestor.ReadUnitHonraryFromCsv(unitHonraryPath);
    if (!unitHonraryResult.Success)
    {
        Console.WriteLine($"❌ Error reading honorary members: {unitHonraryResult.Error}");
        return 1;
    }
    unitHonraryData = unitHonraryResult.Data!;
}
else
{
    var unitNumberToId = unitsResult.Data!.GroupBy(u => u.Number).ToDictionary(g => g.Key, g => g.First().Id);
    var hermesData = hermesResult!.Data!;
    var resolvedHonrary = new List<UnitHonrary>();
    foreach (var (honorary, unitNum) in hermesData.UnitHonrary)
    {
        if (unitNumberToId.TryGetValue(unitNum, out var unitId))
        {
            honorary.UnitId = unitId;
        }
        resolvedHonrary.Add(honorary);
    }
    unitHonraryData = resolvedHonrary;
}
Console.WriteLine($"✅ Loaded {unitHonraryData.Count} honorary member records\n");

// Filter units if a specific unit number was requested
var unitsToExport = unitsResult.Data;
if (filterUnitNumber.HasValue)
{
    unitsToExport = unitsResult.Data.Where(u => u.Number == filterUnitNumber.Value).ToList();
    if (unitsToExport.Count == 0)
    {
        Console.WriteLine($"❌ No unit found with number {filterUnitNumber}");
        return 1;
    }
    Console.WriteLine($"Filtering to unit number: {filterUnitNumber}");
    Console.WriteLine($"Units to export: {unitsToExport.Count}\n");
}
else if (filterUnitType != null)
{
    unitsToExport = unitsResult.Data.Where(u => u.UnitType == filterUnitType).ToList();
    if (unitsToExport.Count == 0)
    {
        Console.WriteLine($"❌ No units found with type {filterUnitType}");
        return 1;
    }
    Console.WriteLine($"Filtering to unit type: {filterUnitType}");
    Console.WriteLine($"Units to export: {unitsToExport.Count}\n");
}
else
{
    Console.WriteLine($"Generating for all units");
    Console.WriteLine($"Units to export: {unitsToExport.Count}\n");
}

// Generate output
try
{
    if (outputFormat == "pdf")
    {
        Console.WriteLine("Generating unit pages PDF...");
        var unitExporter = new UnitPdfExporter(pageSize: pageSize);
        unitExporter.ExportUnitsToPdf(unitsToExport, locationDict, unitOfficersData, officerDict, unitPastMastersData, unitPMIData, unitMembersData, unitHonraryData, unitsOutputPath);
        Console.WriteLine($"✅ Units PDF generated: {unitsOutputPath} ({pageSize})");
    }
    else if (outputFormat == "html")
    {
        Console.WriteLine("Generating unit pages HTML...");
        var unitExporter = new UnitPdfExporter();
        unitExporter.ExportUnitsToHtml(unitsToExport, locationDict, unitOfficersData, officerDict, unitPastMastersData, unitPMIData, unitMembersData, unitHonraryData, unitsOutputPath);
        Console.WriteLine($"✅ Units HTML generated: {unitsOutputPath}");
    }
    
    Console.WriteLine($"   Units included: {unitsToExport.Count}");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error generating output: {ex.Message}");
    return 1;
}

Console.WriteLine("\n✨ Output generation completed successfully!");
return 0;
