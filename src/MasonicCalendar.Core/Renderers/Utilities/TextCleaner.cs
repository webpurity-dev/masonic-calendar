namespace MasonicCalendar.Core.Renderers.Utilities;

/// <summary>
/// Utility class for cleaning and formatting text from CSV data.
/// Used by all renderers to ensure consistent text handling.
/// </summary>
public static class TextCleaner
{
    /// <summary>
    /// Clean name by removing newlines, corruption characters, and excess whitespace.
    /// </summary>
    public static string CleanName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";
        
        // Remove newlines/carriage returns, trim, replace corruption chars
        var cleaned = name.Replace("\r", "").Replace("\n", "").Trim();
        cleaned = cleaned.Replace("•", " ");  // Replace bullet char with space
        cleaned = cleaned.Replace("\ufffd", " ");  // Replace Unicode Replacement Character with space
        
        // Collapse multiple spaces to single space
        while (cleaned.Contains("  "))
            cleaned = cleaned.Replace("  ", " ");
        
        return cleaned;
    }

    /// <summary>
    /// Clean provincial rank by removing special characters and excess whitespace.
    /// </summary>
    public static string CleanProvincialRank(string? rank)
    {
        if (string.IsNullOrWhiteSpace(rank))
            return "";
        
        // Remove commas, brackets, and extra whitespace
        var cleaned = System.Text.RegularExpressions.Regex.Replace(rank, @"[,\(\)\[\]\{\}]", "");
        cleaned = cleaned.Trim();
        
        // Remove extra spaces between words
        while (cleaned.Contains("  "))
            cleaned = cleaned.Replace("  ", " ");
        
        return cleaned;
    }

    /// <summary>
    /// Clean reference/ID by trimming whitespace.
    /// </summary>
    public static string CleanReference(string? reference)
    {
        return string.IsNullOrWhiteSpace(reference) ? "" : reference.Trim();
    }

    /// <summary>
    /// Format a date issued value (rank year, install year, etc).
    /// </summary>
    public static string CleanDateIssued(string? dateIssued)
    {
        if (string.IsNullOrWhiteSpace(dateIssued))
            return "";
        
        var cleaned = dateIssued.Trim();
        // Remove parentheses and extra spaces
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"[()]+", "");
        cleaned = cleaned.Trim();
        
        return cleaned;
    }

    /// <summary>
    /// Extract initials from a full name (e.g., "Neil Jeffrey" -> "N.J.").
    /// </summary>
    public static string ExtractInitialsFromName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return "";

        var cleaned = CleanName(fullName);
        var words = cleaned.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);

        if (words.Length == 0)
            return "";

        var initials = string.Join(".", words.Select(w => w[0])) + ".";
        return initials;
    }

    /// <summary>
    /// Combine surname, initials, and first name for display.
    /// Prefers initials if provided; otherwise extracts from first name.
    /// Example: "White", "", "Neil Jeffrey" -> "White N.J."
    /// Example: "Brookes", "G.L.", "" -> "Brookes G.L."
    /// </summary>
    public static string CombineNameInitialsAndFirstName(string? surname, string? initials, string? firstName)
    {
        if (string.IsNullOrWhiteSpace(surname))
            return "";

        var cleanedSurname = ShortenSurname(CleanName(surname));

        // Determine which initials to use
        string? initialsToUse = null;

        if (!string.IsNullOrWhiteSpace(initials))
        {
            // Use provided initials
            initialsToUse = CleanName(initials)?.Replace(" ", "");
        }
        else if (!string.IsNullOrWhiteSpace(firstName))
        {
            // Extract initials from first name
            initialsToUse = ExtractInitialsFromName(firstName);
        }

        // Combine surname with initials
        if (string.IsNullOrWhiteSpace(initialsToUse))
            return cleanedSurname;

        return $"{cleanedSurname} {initialsToUse}";
    }

    public static string CombineNameAndInitials(string? surname, string? initials)
    {
        // Combine surname and initials for display
        // Format: "Surname I." or just "Surname" if no initials
        if (string.IsNullOrWhiteSpace(surname))
            return "";
        
        var cleaned = ShortenSurname(CleanName(surname));
        
        if (string.IsNullOrWhiteSpace(initials))
            return cleaned ?? "";
        
        var cleanedInitials = CleanName(initials)?.Replace(" ", "");  // Remove spaces from initials
        
        if (string.IsNullOrWhiteSpace(cleanedInitials))
            return cleaned ?? "";
        
        return $"{cleaned} {cleanedInitials}";
    }

    public static string CombineRanks(string? grandRank, string? provRank)
    {
        var cleanGrandRank = string.IsNullOrWhiteSpace(grandRank) ? "" : grandRank.Replace(",","").Trim();
        var cleanProvRank = string.IsNullOrWhiteSpace(provRank) ? "" : provRank.Replace(",","").Trim();

        if(string.IsNullOrWhiteSpace(cleanGrandRank))
            return cleanProvRank ?? "";

        if(string.IsNullOrWhiteSpace(cleanProvRank))
            return cleanGrandRank ?? "";

        return $"{cleanGrandRank}, {cleanProvRank}";
    }

    /// <summary>
    /// Ensures the string ends with a period. Returns empty string for null/whitespace.
    /// </summary>
    public static string EnsureTrailingPeriod(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        var trimmed = value.TrimEnd();
        return trimmed.EndsWith('.') ? trimmed : trimmed + ".";
    }

    public static string CleanPastUnits(string? pastUnits)
    {
        if (string.IsNullOrWhiteSpace(pastUnits))
            return "";

        // Split on commas, trim each token, then rejoin without spaces
        var parts = pastUnits.Split(',', System.StringSplitOptions.RemoveEmptyEntries)
                             .Select(p => p.Trim())
                             .Where(p => p.Length > 0);
        return string.Join(",", parts);
    }

    /// <summary>
    /// Shorten a surname that has more than three words by keeping only the last two words.
    /// E.g. "Andrade De Azeredo Coutinho" (4 words) → "Azeredo Coutinho".
    /// </summary>
    private static string ShortenSurname(string surname)
    {
        var words = surname.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 3)
            return string.Join(" ", words[^2..]);  // Last 2 words
        return surname;
    }
}
