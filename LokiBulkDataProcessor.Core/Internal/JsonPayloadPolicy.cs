using System.Text.Json;

namespace Loki.BulkDataProcessor.Internal;

/// <summary>How JSON payloads are accepted, read once from <c>LokiBulkDataProcessorOptions</c> at registration (singleton, immutable).</summary>
/// <param name="RequireDataMapping">When true, a JSON save with no registered DataMapping throws before connecting.</param>
/// <param name="MaxPayloadBytes">Largest payload read, in bytes; null means no limit.</param>
internal sealed record JsonPayloadPolicy(bool RequireDataMapping, long? MaxPayloadBytes)
{
    /// <summary>Nesting limit for the parser; Loki's schema needs a depth of 3, so 64 is generous.</summary>
    internal const int MaxDepth = 64;

    public static JsonPayloadPolicy Default { get; } = new(RequireDataMapping: false, MaxPayloadBytes: null);

    /// <summary>Explicit parser options: depth limit and the default (disallowed) comment and trailing-comma handling.</summary>
    public static JsonDocumentOptions DocumentOptions => new()
    {
        MaxDepth = MaxDepth,
        CommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false
    };
}
