namespace Loki.Samples.WebApi.Errors;

/// <summary>
/// A request this sample refuses before any data is written, with the HTTP status to answer. Its message is written by this
/// code and never contains caller input, a connection string or row data, so it is safe to return.
/// </summary>
internal sealed class RequestRejectedException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public static RequestRejectedException BadRequest(string message) => new(StatusCodes.Status400BadRequest, message);

    public static RequestRejectedException NotFound(string message) => new(StatusCodes.Status404NotFound, message);

    public static RequestRejectedException Unavailable(string message) => new(StatusCodes.Status503ServiceUnavailable, message);
}
