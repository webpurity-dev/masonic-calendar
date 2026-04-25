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

        // For "Surname Parts, Initials" format: shorten surnames longer than 3 words
        var commaIndex = cleaned.IndexOf(',');
        if (commaIndex > 0)
        {
            var surname = cleaned[..commaIndex];
            var rest = cleaned[commaIndex..]; // includes the comma
            cleaned = ShortenSurname(surname) + rest;
        }
        
        // Remove any names that consist only of punctuation/whitespace (e.g., ",", "  , ", etc.)
        var withoutPunctuation = System.Text.RegularExpressions.Regex.Replace(cleaned, @"[^\w\s]", "").Trim();
        if (string.IsNullOrWhiteSpace(withoutPunctuation))
            return "";
        
        return cleaned;
    }

    /// <summary>
    /// Clean free-text fields (meeting dates, warrant text, etc.) — strips newlines and
    /// collapses whitespace but does NOT apply surname-shortening logic.
    /// Use <see cref="CleanName"/> only for person name fields.
    /// </summary>
    public static string CleanText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        var cleaned = text.Replace("\r", "").Replace("\n", "").Trim();
        cleaned = cleaned.Replace("•", " ");
        cleaned = cleaned.Replace("\ufffd", " ");

        while (cleaned.Contains("  "))
            cleaned = cleaned.Replace("  ", " ");

        return cleaned;
    }

    /// <summary>
    /// Clean provincial rank by removing commas and excess whitespace.
    /// If rank has two words, wraps the second word in brackets (unless already bracketed).
    /// Examples: "ProvGM Dorset" → "ProvGM (Dorset)", "ProvGM (Dorset)" → "ProvGM (Dorset)"
    /// </summary>
    public static string CleanProvincialRank(string? rank)
    {
        if (string.IsNullOrWhiteSpace(rank))
            return "";
        
        // Remove commas but preserve existing brackets
        var cleaned = rank.Replace(",", "").Trim();
        
        // Remove extra spaces between words
        while (cleaned.Contains("  "))
            cleaned = cleaned.Replace("  ", " ");
        
        // Split by spaces to count words
        var parts = cleaned.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        
        // If we have exactly 2 space-separated parts and the second doesn't start with (
        if (parts.Length == 2 && !parts[1].StartsWith("("))
        {
            return $"{parts[0]} ({parts[1]})";
        }
        
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
