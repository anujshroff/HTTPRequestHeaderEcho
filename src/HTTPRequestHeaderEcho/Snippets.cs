using System.Text;

namespace HTTPRequestHeaderEcho;

public static class Snippets
{
    public static string Curl(string url, IReadOnlyList<KeyValuePair<string, string>> headers)
    {
        var sb = new StringBuilder();
        sb.Append("curl -i");
        foreach (var (name, value) in headers)
        {
            sb.Append(" \\\n  -H ");
            sb.Append(PosixQuote($"{name}: {value}"));
        }
        sb.Append(" \\\n  ");
        sb.Append(PosixQuote(url));
        return sb.ToString();
    }

    public static string PowerShell(string url, IReadOnlyList<KeyValuePair<string, string>> headers)
    {
        var sb = new StringBuilder();
        sb.Append("Invoke-RestMethod -Uri ");
        sb.Append(PowerShellQuote(url));
        if (headers.Count > 0)
        {
            sb.Append(" `\n  -Headers @{\n");
            foreach (var (name, value) in headers)
            {
                sb.Append("    ");
                sb.Append(PowerShellQuote(name));
                sb.Append(" = ");
                sb.Append(PowerShellQuote(value));
                sb.Append('\n');
            }
            sb.Append("  }");
        }
        return sb.ToString();
    }

    private static string PosixQuote(string s) => "'" + s.Replace("'", "'\\''") + "'";

    private static string PowerShellQuote(string s) => "'" + s.Replace("'", "''") + "'";
}
