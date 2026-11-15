namespace Loki.BulkDataProcessor.Oracle;

/// <summary>How the Oracle provider turns the destination table and column names it is given into SQL identifiers.</summary>
public enum OracleIdentifierResolution
{
    /// <summary>
    /// Default. Each name (<c>TABLE</c> or <c>SCHEMA.TABLE</c>, and every destination column) must be a simple identifier: a letter,
    /// then letters, digits, <c>_</c>, <c>$</c> or <c>#</c>, at most 128 characters. Anything else is rejected with an
    /// <see cref="System.ArgumentException"/> before any SQL runs. Names are folded to upper case, exactly as Oracle folds unquoted
    /// names, so <c>Posts</c> and <c>POSTS</c> both mean the table created as <c>CREATE TABLE Posts</c>; they are then quoted,
    /// so reserved words such as <c>Date</c> or <c>Comment</c> also work as column names.
    /// </summary>
    Unquoted = 0,

    /// <summary>
    /// Looks the table and its columns up in <c>ALL_TAB_COLUMNS</c> (exact match first, then a single case-insensitive match) and
    /// quotes the names the catalogue returns. Needed for quoted mixed-case objects such as <c>"MixedCase"</c>; wrap a name in double
    /// quotes to require an exact match. The names you pass are only ever sent as bind variables. Costs one dictionary query per
    /// save. Covers tables and views, not synonyms.
    /// </summary>
    CatalogLookup = 1
}
