using System.Globalization;
using System.Text.RegularExpressions;

namespace LeaseLense.Application.Common;

public static partial class DisplayTextFormatter
{
    [GeneratedRegex(@"[_\-\s]+")]
    private static partial Regex SeparatorRegex();

    public static string ToTitleLabel(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return string.Empty;
        }

        var normalized = SeparatorRegex().Replace(rawValue.Trim(), " ");
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized.ToLowerInvariant());
    }
}
