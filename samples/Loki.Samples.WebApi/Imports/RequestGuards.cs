using Loki.Samples.WebApi.Errors;

namespace Loki.Samples.WebApi.Imports;

/// <summary>Request checks that answer 400 before any database work.</summary>
internal static class RequestGuards
{
    /// <summary>The most rows one request may generate or send (keeps the sample's memory use bounded).</summary>
    public const int MaxRows = 100_000;

    public static int ThrowIfNotARowCount(int count, string name) =>
        count is >= 1 and <= MaxRows ? count : throw RequestRejectedException.BadRequest($"{name} must be between 1 and {MaxRows}.");

    public static string ThrowIfBlankOrLonger(string? value, int maxLength, string name) =>
        string.IsNullOrWhiteSpace(value) || value.Length > maxLength
            ? throw RequestRejectedException.BadRequest($"{name} is required and must be at most {maxLength} characters.")
            : value;

    public static string? ThrowIfLonger(string? value, int maxLength, string name) =>
        value is not null && value.Length > maxLength
            ? throw RequestRejectedException.BadRequest($"{name} must be at most {maxLength} characters.")
            : value;
}
