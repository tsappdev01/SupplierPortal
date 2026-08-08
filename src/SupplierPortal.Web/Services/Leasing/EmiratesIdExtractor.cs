using System.Linq;
using System.Text.RegularExpressions;

namespace SupplierPortal.Web.Services.Leasing;

/// <summary>Parses the OCR text of a UAE Emirates ID card into the ID number, expiry date and holder name.
/// Best-effort: any field that cannot be found is returned null.</summary>
public static partial class EmiratesIdExtractor
{
    public record Result(string? Number, string? Expiry, string? Name);

    // 784-YYYY-NNNNNNN-C, with or without the dashes / spaces.
    [GeneratedRegex(@"784[\s-]?\d{4}[\s-]?\d{7}[\s-]?\d", RegexOptions.Compiled)]
    private static partial Regex IdNumberRegex();

    // Dates like 12/03/2030 or 12-03-2030.
    [GeneratedRegex(@"\b(\d{2})[/-](\d{2})[/-](\d{4})\b", RegexOptions.Compiled)]
    private static partial Regex DateRegex();

    // "Name" / "Holder" label, then skip any Arabic text / punctuation up to the first run of Latin words
    // (the English name), capturing up to 5 words. The word separator is [ \t] (NOT \s) so the capture stays
    // on the name's line and does not bleed into the following "Date of Birth" line.
    [GeneratedRegex(@"(?:Full\s*Name|Name|Holder|Card\s*Holder)\s*[:.]?[^A-Za-z\n]{0,25}([A-Z][A-Za-z'’.\-]+(?:[ \t]+[A-Za-z'’.\-]+){0,4})", RegexOptions.Compiled)]
    private static partial Regex NameRegex();

    // Words that belong to the *next* field on the card — trimmed if the OCR line ran them into the name.
    private static readonly HashSet<string> NameStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "Date", "of", "Birth", "Nationality", "Sex", "Gender", "Issuing", "Issue", "Expiry",
        "Signature", "Resident", "Card", "Number", "Emirates", "ID", "Occupation"
    };

    public static Result Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new Result(null, null, null);

        string? number = null;
        var idm = IdNumberRegex().Match(text);
        if (idm.Success)
        {
            var digits = Regex.Replace(idm.Value, @"[^\d]", "");
            if (digits.Length == 15)
                number = $"{digits[..3]}-{digits.Substring(3, 4)}-{digits.Substring(7, 7)}-{digits[14]}";
        }

        // Expiry: prefer a date near an "Expiry"/"Expiring" label, else the latest date on the card.
        string? expiry = null;
        var expiryLabel = Regex.Match(text, @"(?:Expiry|Expiring|Expire[sd]?|Date of Expiry|Card Expiry|Valid\s*Until)\D{0,40}(\d{2}[/-]\d{2}[/-]\d{4})", RegexOptions.IgnoreCase);
        if (expiryLabel.Success)
        {
            expiry = expiryLabel.Groups[1].Value;
        }
        else
        {
            DateTime? latest = null; string? latestRaw = null;
            foreach (Match m in DateRegex().Matches(text))
            {
                if (DateTime.TryParse($"{m.Groups[3].Value}-{m.Groups[2].Value}-{m.Groups[1].Value}", out var d)
                    && (latest is null || d > latest))
                {
                    latest = d; latestRaw = m.Value;
                }
            }
            expiry = latestRaw;
        }

        string? name = null;
        var nm = NameRegex().Match(text);
        if (nm.Success)
        {
            // Drop any trailing words that actually belong to the next field (e.g. "… Khan Date of Birth").
            var words = nm.Groups[1].Value
                .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();
            while (words.Count > 1 && NameStopWords.Contains(words[^1]))
                words.RemoveAt(words.Count - 1);
            var cleaned = string.Join(' ', words).Trim();
            if (cleaned.Length >= 2) name = cleaned;
        }

        return new Result(number, expiry, name);
    }
}
