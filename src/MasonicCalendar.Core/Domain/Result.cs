namespace MasonicCalendar.Core.Domain;

/// <summary>
/// Result pattern for operations that may fail.
/// </summary>
public record Result<T>(bool Success, T? Data, string? Error)
{
    public static Result<T> Ok(T data) => new(true, data, null);
    public static Result<T> Fail(string error) => new(false, default, error);
}
