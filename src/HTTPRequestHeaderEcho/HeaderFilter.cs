using Microsoft.Extensions.Primitives;

namespace HTTPRequestHeaderEcho;

public static class HeaderFilter
{
    public static IEnumerable<KeyValuePair<string, StringValues>> WithPrefixFilter(
        this IHeaderDictionary headers, string[] prefixes) =>
        prefixes.Length == 0
            ? headers
            : headers.Where(h =>
                prefixes.Any(p => h.Key.StartsWith(p, StringComparison.OrdinalIgnoreCase)));
}
