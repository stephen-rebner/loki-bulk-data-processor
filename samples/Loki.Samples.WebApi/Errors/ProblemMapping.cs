using System.Text.Json;
using Loki.BulkDataProcessor.Providers;

namespace Loki.Samples.WebApi.Errors;

/// <summary>The HTTP answer for a failed request: status, title and a detail that is safe to return.</summary>
internal readonly record struct Problem(int StatusCode, string Title, string Detail);

/// <summary>
/// Maps exceptions to HTTP problems. Details are fixed texts written here: never a driver message (it can quote row values or
/// name the server), never a connection string. Loki's own messages are data-free too, but a fixed text keeps the API stable.
/// </summary>
internal static class ProblemMapping
{
    /// <summary>Non-standard "client closed request" (nginx); the caller is gone and will not read it.</summary>
    public const int ClientClosedRequest = 499;

    public static Problem For(Exception exception, bool requestAborted = false) => exception switch
    {
        RequestRejectedException rejected => new(rejected.StatusCode, TitleFor(rejected.StatusCode), rejected.Message),
        OperationCanceledException when requestAborted => new(ClientClosedRequest, "Request cancelled", "The client cancelled the request; nothing was committed by the cancelled save."),
        BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } =>
            new(StatusCodes.Status413PayloadTooLarge, "Payload too large", "The request body is larger than this endpoint accepts."),
        BadHttpRequestException badRequest => new(badRequest.StatusCode, "Bad request", "The request could not be read."),
        // Loki: an invalid JSON payload, or one larger than MaxJsonPayloadBytes (InvalidDataException); malformed JSON
        // (JsonException); a value that does not convert to its column type (FormatException, which reaches the caller as the
        // InnerException of Loki's InvalidOperationException when the conversion happens during the copy).
        InvalidDataException or JsonException or FormatException or InvalidOperationException { InnerException: FormatException } =>
            new(StatusCodes.Status400BadRequest, "Invalid JSON payload", "The JSON payload is invalid, too large, or has a value that does not match its column type."),
        InvalidOperationException when IsColumnMismatch(exception.Message) =>
            new(StatusCodes.Status400BadRequest, "Columns do not match", "A mapped column is missing from the payload or from the table; nothing was saved."),
        // Loki throws ArgumentException (synchronously) for bad arguments, for example an empty collection.
        ArgumentException => new(StatusCodes.Status400BadRequest, "Invalid request", "The request was rejected before any data was written."),
        SimulatedFailureException => new(StatusCodes.Status500InternalServerError, "Simulated failure",
            "The save failed after Loki wrote its rows; the caller rolled back its transaction, so nothing was saved."),
        _ when DuplicateKeyErrors.IsDuplicateKey(exception) =>
            new(StatusCodes.Status409Conflict, "Duplicate key", "A row has a key that already exists; nothing was saved."),
        _ => new(StatusCodes.Status500InternalServerError, "Bulk save failed", "The request failed; see the server log. Nothing was committed by the failed save.")
    };

    /// <summary>The start of Loki's <see cref="BulkCopyErrorMessages.SourceColumnNotFound"/> text, up to the column name.</summary>
    private static readonly string SourceColumnNotFoundPrefix = PrefixOf(BulkCopyErrorMessages.SourceColumnNotFound("column"));

    /// <summary>
    /// Loki's fixed messages for a mapped column that is missing: <see cref="BulkCopyErrorMessages.ColumnMappingMismatch"/> (a
    /// destination column, and on SQL Server also a source column) or <see cref="BulkCopyErrorMessages.SourceColumnNotFound"/>
    /// (a source column, on Oracle).
    /// </summary>
    private static bool IsColumnMismatch(string message) =>
        message.StartsWith(BulkCopyErrorMessages.ColumnMappingMismatch, StringComparison.Ordinal)
        || message.StartsWith(SourceColumnNotFoundPrefix, StringComparison.Ordinal);

    private static string PrefixOf(string message) => message[..message.IndexOf('\'', StringComparison.Ordinal)];

    private static string TitleFor(int statusCode) => statusCode switch
    {
        StatusCodes.Status404NotFound => "Not found",
        StatusCodes.Status503ServiceUnavailable => "Engine not available",
        _ => "Invalid request"
    };
}
