namespace Loki.Samples.WebApi.Endpoints;

internal static class JsonUploadLimits
{
    /// <summary>
    /// 10 MB: Loki buffers a JSON payload in memory, so payloads from callers need a limit (README "Options",
    /// <c>MaxJsonPayloadBytes</c>). Start from a few times your largest legitimate payload.
    /// </summary>
    public const long MaxPayloadBytes = 10 * 1024 * 1024;
}
