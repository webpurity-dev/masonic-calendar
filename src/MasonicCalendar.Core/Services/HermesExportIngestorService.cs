using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using MasonicCalendar.Core.Domain;

namespace MasonicCalendar.Core.Services;

/// <summary>
/// Ingests unit data from hermes-export.csv (v2 schema).
/// Single CSV file with Type column to distinguish: Off (Officers), PMO (Past Masters), 
/// PMI (Joining Past Masters), Mem (Members), Hon (Honorary members).
/// </summary>
public class HermesExportIngestorService
{
    private static CsvConfiguration CreateCsvConfig()
    {
        return new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            HeaderValidated = null,
            MissingFieldFound = null
        };
    }

    /// <summary>
    /// Cleans up name by removing invalid/garbled characters (e.g., replacement character, control chars).
    /// Preserves spaces between initials but collapses multiple spaces.
    /// </summary>
    private static string CleanName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        // Remove replacement character (�) and other control characters
        var cleaned = new System.Text.StringBuilder();
        var lastWasSpace = false;

        foreach (var ch in name)
        {
            // Keep ASCII letters, digits, spaces, hyphens, apostrophes, and periods
            if ((ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z') || 
                (ch >= '0' && ch <= '9') || ch == '-' || ch == '\'' || ch == '.')
            {
                cleaned.Append(ch);
                lastWasSpace = false;
            }
            else if (ch == ' ' && !lastWasSpace)
            {
                // Only append space if the last character wasn't a space
                cleaned.Append(ch);
                lastWasSpace = true;
            }
            // Skip replacement character (U+FFFD), other non-ASCII characters, and duplicate spaces
        }

        return cleaned.ToString().Trim();
    }

    /// <summary>
    /// Parse name in "LastName, Initials" format into separate LastName and Initials.
    /// Example: "Hughes, W   " -> LastName: "Hughes", Initials: "W"
    /// Example: "White, N J " -> LastName: "White", Initials: "N J"
    /// </summary>
    private static (string lastName, string initials) ParseName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return (string.Empty, string.Empty);

        var parts = fullName.Split(',');
        if (parts.Length != 2)
            return (CleanName(fullName), string.Empty);

        var lastName = CleanName(parts[0]);
        var initials = CleanName(parts[1]);

        return (lastName, initials);
    }

    /// <summary>
    /// Parse CSV line handling quoted fields properly.
    /// </summary>
    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var currentField = new System.Text.StringBuilder();
        var inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var ch = line[i];

            if (ch == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (ch == ',' && !inQuotes)
            {
                fields.Add(currentField.ToString().Trim());
                currentField.Clear();
            }
            else
            {
                currentField.Append(ch);
            }
        }

        // Add the last field
        fields.Add(currentField.ToString().Trim());

        return fields;
    }

    /// <summary>
    /// Read all data from hermes-export.csv and return organized collections.
    /// Manually parses CSV handling quoted fields properly.
    /// </summary>
    public Result<HermesExportData> ReadHermesExportCsv(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return Result<HermesExportData>.Fail($"File not found: {filePath}");

            var lines = File.ReadAllLines(filePath);
            if (lines.Length == 0)
                return Result<HermesExportData>.Fail("CSV file is empty.");

            // Parse header
            var headers = ParseCsvLine(lines[0]);
            var headerDict = new Dictionary<string, int>();
            for (int i = 0; i < headers.Count; i++)
            {
                headerDict[headers[i]] = i;
            }

            if (!headerDict.ContainsKey("Type") || !headerDict.ContainsKey("Unit"))
                return Result<HermesExportData>.Fail("Required headers (Type, Unit) not found.");

            var unitOfficersWithNums = new List<(UnitOfficer officer, int unitNum)>();
            var unitPastMastersWithNums = new List<(UnitPastMaster master, int unitNum)>();
            var unitPMIWithNums = new List<(UnitPMI pmi, int unitNum)>();
            var unitMembersWithNums = new List<(UnitMember member, int unitNum)>();
            var unitHonraryWithNums = new List<(UnitHonrary honorary, int unitNum)>();

            // Parse data rows
            for (int lineNum = 1; lineNum < lines.Length; lineNum++)
            {
                var values = ParseCsvLine(lines[lineNum]);
                if (values.Count < headerDict.Values.Max() + 1)
                    continue;

                var record = new Dictionary<string, string>();
                foreach (var kvp in headerDict)
                {
                    var header = kvp.Key;
                    var index = kvp.Value;
                    record[header] = index < values.Count ? values[index] : string.Empty;
                }

                var typeVal = record.ContainsKey("Type") ? record["Type"] : string.Empty;
                if (string.IsNullOrWhiteSpace(typeVal))
                    continue;

                typeVal = typeVal.Trim();
                
                // Get unit number
                if (!record.ContainsKey("Unit") || !int.TryParse(record["Unit"], out var unitNum))
                    continue;

                switch (typeVal)
                {
                    case "Off":
                        ProcessOfficers(new[] { record }, unitOfficersWithNums, unitNum);
                        break;
                    case "PMO":
                        ProcessPastMasters(new[] { record }, unitPastMastersWithNums, unitNum);
                        break;
                    case "PMI":
                        ProcessJoiningPastMasters(new[] { record }, unitPMIWithNums, unitNum);
                        break;
                    case "Mem":
                        ProcessMembers(new[] { record }, unitMembersWithNums, unitNum);
                        break;
                    case "Hon":
                        ProcessHonoraryMembers(new[] { record }, unitHonraryWithNums, unitNum);
                        break;
                }
            }

            var data = new HermesExportData
            {
                UnitOfficers = unitOfficersWithNums,
                UnitPastMasters = unitPastMastersWithNums,
                UnitPMI = unitPMIWithNums,
                UnitMembers = unitMembersWithNums,
                UnitHonrary = unitHonraryWithNums
            };

            return Result<HermesExportData>.Ok(data);
        }
        catch (Exception ex)
        {
            return Result<HermesExportData>.Fail($"Error reading Hermes export CSV: {ex.Message}");
        }
    }

    private static void ProcessOfficers(IEnumerable<Dictionary<string, string>> records, List<(UnitOfficer, int)> results, int unitNum)
    {
        foreach (var record in records)
        {
            var name = record["Name"].Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var (lastName, initials) = ParseName(name);
            var position = record.ContainsKey("FN01") ? CleanName(record["FN01"]) : string.Empty;
            var posNo = record.ContainsKey("PosNo") && int.TryParse(record["PosNo"], out var pn) ? pn : 0;
            
            var officer = new UnitOfficer
            {
                Id = Guid.NewGuid(),
                UnitId = Guid.Empty,
                OfficerId = Guid.Empty,
                LastName = lastName,
                Initials = initials,
                Position = position,
                PosNo = posNo
            };

            results.Add((officer, unitNum));
        }
    }

    private static void ProcessPastMasters(IEnumerable<Dictionary<string, string>> records, List<(UnitPastMaster, int)> results, int unitNum)
    {
        foreach (var record in records)
        {
            var name = record["Name"].Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var (lastName, initials) = ParseName(name);
            
            var installed = record.ContainsKey("FN01") ? record["FN01"].Trim() : string.Empty;
            var provRank = record.ContainsKey("FN13") ? record["FN13"].Trim() : string.Empty;
            var provRankIssued = record.ContainsKey("FN14") ? record["FN14"].Trim() : string.Empty;

            var pastMaster = new UnitPastMaster
            {
                Id = Guid.NewGuid(),
                UnitId = Guid.Empty,
                LastName = lastName,
                Initials = initials,
                Installed = installed,
                ProvRank = provRank,
                ProvRankIssued = provRankIssued
            };

            results.Add((pastMaster, unitNum));
        }
    }

    private static void ProcessJoiningPastMasters(IEnumerable<Dictionary<string, string>> records, List<(UnitPMI, int)> results, int unitNum)
    {
        foreach (var record in records)
        {
            var name = record["Name"].Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var (lastName, initials) = ParseName(name);
            
            var installed = record.ContainsKey("FN01") ? record["FN01"].Trim() : string.Empty;
            var provRank = record.ContainsKey("FN12") ? record["FN12"].Trim() : string.Empty;
            var provRankIssued = record.ContainsKey("FN13") ? record["FN13"].Trim() : string.Empty;

            var pmi = new UnitPMI
            {
                Id = Guid.NewGuid(),
                UnitId = Guid.Empty,
                LastName = lastName,
                Initials = initials,
                ProvRank = provRank,
                ProvRankIssued = provRankIssued
            };

            results.Add((pmi, unitNum));
        }
    }

    private static void ProcessMembers(IEnumerable<Dictionary<string, string>> records, List<(UnitMember, int)> results, int unitNum)
    {
        foreach (var record in records)
        {
            var name = record["Name"].Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var (lastName, initials) = ParseName(name);
            
            var joined = record.ContainsKey("FN01") ? record["FN01"].Trim() : string.Empty;
            var provRank = record.ContainsKey("FN12") ? record["FN12"].Trim() : string.Empty;

            var member = new UnitMember
            {
                Id = Guid.NewGuid(),
                UnitId = Guid.Empty,
                LastName = lastName,
                Initials = initials,
                Joined = joined,
                ProvRank = provRank
            };

            results.Add((member, unitNum));
        }
    }

    private static void ProcessHonoraryMembers(IEnumerable<Dictionary<string, string>> records, List<(UnitHonrary, int)> results, int unitNum)
    {
        foreach (var record in records)
        {
            var name = record["Name"].Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var (lastName, initials) = ParseName(name);
            
            var grandRank = record.ContainsKey("FN13") ? record["FN13"].Trim() : string.Empty;
            var provRank = record.ContainsKey("FN14") ? record["FN14"].Trim() : string.Empty;

            var honorary = new UnitHonrary
            {
                Id = Guid.NewGuid(),
                UnitId = Guid.Empty,
                LastName = lastName,
                Initials = initials,
                GrandRank = grandRank,
                ProvRank = provRank
            };

            results.Add((honorary, unitNum));
        }
    }
}

/// <summary>
/// Container for all parsed Hermes export data with unit numbers for resolving unit IDs.
/// </summary>
public class HermesExportData
{
    public List<(UnitOfficer officer, int unitNumber)> UnitOfficers { get; set; } = new();
    public List<(UnitPastMaster master, int unitNumber)> UnitPastMasters { get; set; } = new();
    public List<(UnitPMI pmi, int unitNumber)> UnitPMI { get; set; } = new();
    public List<(UnitMember member, int unitNumber)> UnitMembers { get; set; } = new();
    public List<(UnitHonrary honorary, int unitNumber)> UnitHonrary { get; set; } = new();
}
