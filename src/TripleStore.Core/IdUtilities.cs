namespace TripleStore.Core;

public static class IdUtilities
{
    public static (string, string) SplitForIndexing(this Uri uri)
    => SplitForIndexing(uri.OriginalString);

    public static (string, string) SplitForIndexing(this string absoluteUri)
    {
        // Determine the start of the authority to avoid counting slashes in the scheme (e.g., "https://")
        var schemeSep = absoluteUri.IndexOf("://");
        var authorityStart = schemeSep >= 0 ? schemeSep + 3 : 0;

        // Special-case for file URIs like "file:///C:/path" to keep the scheme-only prefix "file://"
        if (schemeSep >= 0 && absoluteUri.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
        {
            // Prefix should be "file://" and suffix everything after the third slash
            var prefixSpecial = absoluteUri.Substring(0, schemeSep + 3);
            var suffixSpecial = absoluteUri.Substring(schemeSep + 4);
            return (prefixSpecial, suffixSpecial);
        }

        // Find the last slash ('/') after the authority
        var lastForwardSlash = absoluteUri.LastIndexOf('/');
        if (lastForwardSlash < authorityStart)
        {
            lastForwardSlash = -1; // ignore slashes that are part of the scheme
        }

        // Also consider backslashes ('\\') for inputs that use them
        var lastBackSlash = absoluteUri.LastIndexOf('\\');

        var splitPoint = Math.Max(lastForwardSlash, lastBackSlash);
        if (splitPoint == -1)
        {
            return (null, absoluteUri);
        }

        // split string into two parts at the split point
        var prefix = absoluteUri[..splitPoint];
        var suffix = absoluteUri[(splitPoint + 1)..];
        return (prefix, suffix);
    }
}
