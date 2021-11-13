namespace TripleStore.Core;

public static class IdUtilities
{
    public static (string, string) SplitForIndexing(this Uri uri)
    => SplitForIndexing(uri.AbsoluteUri);

    public static (string, string) SplitForIndexing(this string absoluteUri)
    {
        var splitPoint = absoluteUri.LastIndexOf('/');
        if (splitPoint == -1)
        {
            return (null, absoluteUri);
        }
        // split uri into two strings at last '/'
        var prefix = absoluteUri[..splitPoint];
        var suffix = absoluteUri[(splitPoint + 1)..];
        return (prefix, suffix);
    }
}