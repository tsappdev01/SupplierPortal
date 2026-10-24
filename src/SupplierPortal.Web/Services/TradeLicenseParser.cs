using System.Globalization;
using System.Text.RegularExpressions;

namespace SupplierPortal.Web.Services;

/// <summary>One owner/member row of a Dubai DED trade/professional licence, as printed in the
/// "License Members" / "الأطراف" table (or the older "Partners" / "ملحق الشركاء" annex).</summary>
public sealed record LicenseMember(
    string Name,
    string? PersonNo,
    string? Nationality,
    string? Role,
    string? Share);

/// <summary>One row of the licence's activities table ("License Activities" / "نشاط الرخصة").</summary>
public sealed record LicenseActivity(string Name, string? Status);

/// <summary>Everything the parser can read from a Dubai DED trade/professional licence. Every field is
/// best-effort: a field that isn't present or can't be recognised on a given licence is left null (or
/// an empty list), so a non-standard layout never throws. The most reliable fields on these bilingual
/// licences are <see cref="LicenseNumber"/>, <see cref="DunsNumber"/>, <see cref="LegalType"/> and
/// <see cref="Members"/>; the remaining text fields are read from a window around their English label
/// and may be blank on unusual layouts.</summary>
public sealed class ParsedLicense
{
    public string? LicenseNumber { get; set; }
    public string? BusinessName { get; set; }
    public string? LicenseCategory { get; set; }
    public string? LegalType { get; set; }
    public string? IssueDate { get; set; }
    public string? ExpiryDate { get; set; }
    public string? MainLicenseNumber { get; set; }
    public string? DunsNumber { get; set; }
    public string? RegisterNumber { get; set; }
    public string? DcciNumber { get; set; }
    public List<LicenseActivity> Activities { get; set; } = [];
    public List<LicenseMember> Members { get; set; } = [];

    // Extra fields captured by the Claude-vision reader (and left null by the regex parser when not found),
    // so one result type covers DED mainland and free-zone layouts alike.
    public string? LegalName { get; set; }
    public string? TradeName { get; set; }
    /// <summary>Issuing authority / free zone: DED, JAFZA, DAFZA, Creative City, DMCC, ...</summary>
    public string? IssuingAuthority { get; set; }
    /// <summary>License kind: Commercial / Professional / Industrial, etc.</summary>
    public string? LicenseKind { get; set; }
    /// <summary>Paid capital exactly as printed (digits only, e.g. "300000"); mapped to decimal on save.</summary>
    public string? PaidCapital { get; set; }
    public string? CapitalCurrency { get; set; }
    public string? PoBox { get; set; }
    public string? ParcelId { get; set; }
    public string? Premises { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? Address { get; set; }
}

/// <summary>Reusable, dependency-free extractor for Dubai DED trade/professional licences. Give it the
/// licence's text (from the PDF text layer, or from OCR for scanned/image licences) and it returns a
/// <see cref="ParsedLicense"/>. Pure and side-effect-free, so it can be unit-tested on its own and
/// reused anywhere (upload pipeline, an "extraction preview" screen, a batch tool, …).</summary>
public static class TradeLicenseParser
{
    // ---- Identifiers ---------------------------------------------------------------------------

    // License number: alphanumeric, right after a "License No./Number" label. The filler is
    // punctuation/whitespace only so a "TL-" style prefix isn't eaten off the value.
    private static readonly Regex LicenseNoLabeled = new(
        @"(?:Trade\s*Licen[cs]e|Licen[cs]e)\s*(?:No\.?|Number)\s*[:\-.]{0,2}\s*([A-Za-z0-9][A-Za-z0-9\-\/]{2,19})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MainLicenseNoLabeled = new(
        @"Main\s*Licen[cs]e\s*(?:No\.?|Number|Nr\.?)\s*[:\-.]{0,2}\s*([A-Za-z0-9][A-Za-z0-9\-\/]{2,19})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // D&B D-U-N-S numbers are always 9 digits; read the 9-digit run nearest the label (Singleline so
    // the label and value can be on different lines), with a bare 9-digit fallback.
    private static readonly Regex DunsLabeled = new(
        @"D\W*U\W*N\W*S.*?\b(\d{9})\b",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex DunsBare = new(@"\b(\d{9})\b", RegexOptions.Compiled);

    private static readonly Regex Date = new(@"\b(\d{2}[/.]\d{2}[/.]\d{4})\b", RegexOptions.Compiled);

    // License kind printed as the document heading ("Commercial License", "Industrial License", ...).
    private static readonly Regex LicenseKindLine = new(
        @"\b(Commercial|Industrial|Professional|Tourism|Agricultural|Craftsmanship)\s+Licen[cs]e\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Textual date ("27 January 2016") used by some free-zone licences instead of dd/MM/yyyy.
    private static readonly Regex TextDate = new(
        @"\b(\d{1,2})\s+(January|February|March|April|May|June|July|August|September|October|November|December)\s+(\d{4})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CapsName = new(
        @"\b[A-Z][A-Z0-9]*(?:\s+[A-Z0-9()&.,'\-]+){1,8}\b", RegexOptions.Compiled);
    private static readonly Regex RegisterNoLabeled = new(
        @"Register\s*No\.?\s*[:\-.]{0,2}\s*(\d{4,10})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DcciNoLabeled = new(
        @"DCCI\s*No\.?\s*[:\-.]{0,2}\s*(\d{4,10})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // ---- Members / partners --------------------------------------------------------------------

    private static readonly Regex PartnerRow = new(
        @"(\d{4,7})\s+([A-Za-z][^\n%\d]*[A-Za-z.)])[^%]*?(\d{1,3}(?:\.\d+)?)\s*%",
        RegexOptions.Compiled);
    private static readonly Regex MemberName = new(
        @"\b[A-Z][A-Z]+(?:\s+[A-Z][A-Z.'&()\-]+){1,6}\b", RegexOptions.Compiled);
    private static readonly Regex SerialNumber = new(@"\b\d{4,7}\b", RegexOptions.Compiled);
    private static readonly Regex SharePercent = new(@"(\d{1,3}(?:\.\d+)?)\s*%", RegexOptions.Compiled);

    private static readonly string[] KnownNationalities =
    [
        "United Arab Emirates", "Saudi Arabia", "Oman", "Kuwait", "Bahrain", "Qatar", "Yemen",
        "Egypt", "Jordan", "Lebanon", "Syria", "Sudan", "Iraq", "Iran", "Palestine",
        "India", "Pakistan", "Bangladesh", "Sri Lanka", "Nepal", "Philippines", "China",
        "United Kingdom", "United States", "Canada", "Germany", "France"
    ];

    // Longest-first so "General Manager" wins over "Manager", "Shares Owner" over "Owner", etc.
    private static readonly string[] KnownRoles =
    [
        "Shares Owner", "Managing Partner", "General Manager", "Managing Director",
        "Sole Proprietor", "Authorized Signatory", "Local Service Agent",
        "Manager", "Partner", "Director", "Owner", "Agent"
    ];

    private static readonly string[] StatusWords = ["Active", "Inactive", "Expired", "Cancelled"];

    // Legal types as printed on DED licences, longest-first so the most specific phrase wins. Matched
    // anywhere in the text because the bilingual layout prints the value before the "Legal Type" label
    // in the PDF text layer (and after it in OCR output), which a label-window read gets wrong.
    private static readonly string[] KnownLegalTypes =
    [
        "Free Zone Limited Liability Company", "Private Joint Stock Company", "Public Joint Stock Company",
        "Limited Liability Company", "Branch of a Foreign Company", "Branch of Foreign Company",
        "Free Zone Establishment", "Free Zone Company", "One Person Company", "Sole Proprietorship LLC",
        "Sole Proprietorship", "Sole Establishment", "Civil Company", "Partnership Company", "Partnership"
    ];

    // Issuing authority / free zone, by a distinctive token anywhere in the text. Most specific first,
    // so "Dubai Production City" wins over a bare "Dubai" and a free zone wins over the mainland default.
    private static readonly (string Needle, string Name)[] Authorities =
    [
        ("Dubai Production City", "Dubai Production City (TECOM)"),
        ("Dubai Media City", "Dubai Media City (TECOM)"),
        ("Dubai Internet City", "Dubai Internet City (TECOM)"),
        ("Dubai Studio City", "Dubai Studio City (TECOM)"),
        ("Dubai Silicon Oasis", "Dubai Silicon Oasis"),
        ("Dubai Healthcare City", "Dubai Healthcare City"),
        ("Dubai Airport Free", "DAFZA"), ("DAFZA", "DAFZA"),
        ("Jebel Ali Free", "JAFZA"), ("JAFZA", "JAFZA"),
        ("DMCC", "DMCC"), ("Multi Commodities", "DMCC"),
        ("Creative City", "Fujairah Creative City"),
        ("Dubai South", "Dubai South"),
        ("RAKEZ", "RAKEZ"), ("Ras Al Khaimah Economic", "RAKEZ"),
        ("SAIF", "Sharjah (SAIF Zone)"),
        ("Economic Development", "Dubai DET (Department of Economy and Tourism)"),
        ("Economy and Tourism", "Dubai DET (Department of Economy and Tourism)"),
    ];

    /// <summary>Parses a licence's text into a <see cref="ParsedLicense"/>. Never throws; unreadable or
    /// absent fields are left null / empty.</summary>
    public static ParsedLicense Parse(string? text)
    {
        var result = new ParsedLicense();
        if (string.IsNullOrWhiteSpace(text))
        {
            return result;
        }

        result.LicenseNumber = FirstGroup(LicenseNoLabeled, text);
        result.MainLicenseNumber = FirstGroup(MainLicenseNoLabeled, text) ?? result.LicenseNumber;

        var duns = DunsLabeled.Match(text);
        if (!duns.Success) { duns = DunsBare.Match(text); }
        result.DunsNumber = duns.Success ? duns.Groups[1].Value : null;

        var kindMatch = LicenseKindLine.Match(text);
        result.LicenseKind = kindMatch.Success
            ? CultureInfo.InvariantCulture.TextInfo.ToTitleCase(kindMatch.Groups[1].Value.ToLowerInvariant())
            : null;
        result.IssuingAuthority = DetectAuthority(text);

        result.LegalType = FirstContained(text, KnownLegalTypes)
            ?? LabeledText(text, "Legal Type", "Legal Status", "الشكل القانون");
        result.LicenseCategory = LabeledText(text, "License Category", "فئة الرخصة");
        result.BusinessName = LabeledName(text, "Business Name", "Company Name", "Operating Name", "الاسم التجاري", "اسم الشركة");
        (result.IssueDate, result.ExpiryDate) = ExtractIssueExpiry(text);
        result.RegisterNumber = FirstGroup(RegisterNoLabeled, text);
        result.DcciNumber = FirstGroup(DcciNoLabeled, text);

        result.Activities = ExtractActivities(text);
        result.Members = ExtractMembers(text);
        return result;
    }

    // ---- Field helpers -------------------------------------------------------------------------

    private static string? FirstGroup(Regex regex, string text)
    {
        var m = regex.Match(text);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    /// <summary>Reads a short free-text value printed next to one of the given (English/Arabic) labels.
    /// Searches a window on both sides of the label so it works whether the value prints after the label
    /// (typical OCR order) or before it (the RTL text-layer order).</summary>
    private static string? LabeledText(string text, params string[] labels)
    {
        foreach (var label in labels)
        {
            var i = text.IndexOf(label, StringComparison.OrdinalIgnoreCase);
            if (i < 0) { continue; }
            var after = Slice(text, i + label.Length, i + label.Length + 60);
            var value = FirstLatinPhrase(after);
            if (value is not null) { return value; }
            var before = Slice(text, i - 60, i);
            value = FirstLatinPhrase(before, last: true);
            if (value is not null) { return value; }
        }
        return null;
    }

    /// <summary>Reads an ALL-CAPS Latin name (e.g. a business name) near one of the given labels.</summary>
    private static string? LabeledName(string text, params string[] labels)
    {
        foreach (var label in labels)
        {
            var i = text.IndexOf(label, StringComparison.OrdinalIgnoreCase);
            if (i < 0) { continue; }
            var window = Slice(text, i - 80, i + label.Length + 80);
            var m = CapsName.Match(window);
            while (m.Success)
            {
                var name = Regex.Replace(m.Value, @"\s+", " ").Trim();
                if (name.Length >= 3 && !name.Contains("LICENSE", StringComparison.OrdinalIgnoreCase)
                    && !name.Contains("NAME", StringComparison.OrdinalIgnoreCase))
                {
                    return name;
                }
                m = m.NextMatch();
            }
        }
        return null;
    }

    private static string? LabeledDate(string text, params string[] labels)
    {
        foreach (var label in labels)
        {
            var i = text.IndexOf(label, StringComparison.OrdinalIgnoreCase);
            if (i < 0) { continue; }
            var window = Slice(text, i, i + 60);
            var m = Date.Match(window);
            if (m.Success) { return m.Groups[1].Value; }
            window = Slice(text, i - 40, i);
            m = Date.Match(window);
            if (m.Success) { return m.Groups[1].Value; }
        }
        return null;
    }

    /// <summary>Reads issue + expiry dates. On the modern DET/DED layout the two dates print together
    /// (expiry first, then issue) far from their "Issue Date"/"Expiry Date" labels, so a label-window
    /// read misses them. A DET licence runs from its (oldest) issue date to its (newest) expiry date, so
    /// of the dates in the body - excluding the "Print Date" footer, and tolerating an intermediate
    /// "remarks" date - the earliest is the issue date and the latest the expiry date. The labelled read
    /// is tried first (older layouts) and kept as the fallback. Handles dd/MM/yyyy and "27 January 2016".</summary>
    private static (string? issue, string? expiry) ExtractIssueExpiry(string text)
    {
        var labelledIssue = LabeledDate(text, "Issue Date", "تاريخ الاصدار", "تاريخ الإصدار");
        var labelledExpiry = LabeledDate(text, "Expiry Date", "تاريخ الانتهاء", "تاريخ النتهاء");
        if (labelledIssue is not null && labelledExpiry is not null && labelledIssue != labelledExpiry)
        {
            return (labelledIssue, labelledExpiry);
        }

        // Confine the search to the top "License Details" block: issue/expiry always print there, above
        // the members/activities tables and the Print Date / Remarks footer - so a member's serial-year,
        // an establishment/remarks date or the print date can never be mistaken for issue/expiry.
        var body = text;
        var cut = EarliestIndexOf(body, "License Members", "الاطراف", "الأطراف", "License Activities",
            "نشاط الرخصة", "Print Date", "تاريخ الطباعة", "Remarks", "الملاحظات");
        if (cut > 0) { body = body[..cut]; }

        var found = new List<(string raw, DateTime dt)>();
        foreach (Match m in Date.Matches(body))
        {
            if (TryParseLicenseDate(m.Groups[1].Value, out var dt)) { found.Add((m.Groups[1].Value, dt)); }
        }
        foreach (Match m in TextDate.Matches(body))
        {
            if (TryParseLicenseDate(m.Value, out var dt)) { found.Add((m.Value.Trim(), dt)); }
        }
        if (found.Count >= 2)
        {
            var earliest = found.OrderBy(p => p.dt).First().raw;
            var latest = found.OrderByDescending(p => p.dt).First().raw;
            if (earliest != latest) { return (earliest, latest); }
        }
        return (labelledIssue, labelledExpiry);
    }

    private static bool TryParseLicenseDate(string s, out DateTime dt)
    {
        s = s.Replace('.', '/').Trim();
        string[] formats = ["dd/MM/yyyy", "d/M/yyyy", "d MMMM yyyy", "dd MMMM yyyy"];
        return DateTime.TryParseExact(s, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt);
    }

    /// <summary>Identifies the issuing authority / free zone from a distinctive token in the text,
    /// defaulting to Dubai DET when the mainland economic-development wording is present.</summary>
    private static string? DetectAuthority(string text)
    {
        foreach (var (needle, name) in Authorities)
        {
            if (text.Contains(needle, StringComparison.OrdinalIgnoreCase)) { return name; }
        }
        return null;
    }

    /// <summary>First run of Latin words in a fragment (a small free-text value like "Civil Company").</summary>
    private static string? FirstLatinPhrase(string fragment, bool last = false)
    {
        var matches = Regex.Matches(fragment, @"[A-Za-z][A-Za-z ()/&.,'\-]{2,60}[A-Za-z)]");
        if (matches.Count == 0) { return null; }
        var value = (last ? matches[^1] : matches[0]).Value;
        value = Regex.Replace(value, @"\s+", " ").Trim().Trim('.', ',', '-', '/').Trim();
        return value.Length >= 3 ? value : null;
    }

    // ---- Activities ----------------------------------------------------------------------------

    private static List<LicenseActivity> ExtractActivities(string text)
    {
        var activities = new List<LicenseActivity>();
        var start = FirstIndexOf(text, "License Activities", "نشاط الرخصة", "Activity");
        if (start < 0) { return activities; }
        var scope = text[start..];
        var end = EarliestIndexOf(scope, "الملاحظات", "Remarks", "Print Date", "الطراف", "License Members");
        if (end > 0) { scope = scope[..end]; }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in scope.Split('\n'))
        {
            var line = rawLine.Trim();
            var status = StatusWords.FirstOrDefault(s => line.Contains(s, StringComparison.OrdinalIgnoreCase));
            var namePart = FirstLatinPhrase(line);
            if (namePart is null || namePart.Length < 4
                || namePart.Equals("Activity", StringComparison.OrdinalIgnoreCase)
                || StatusWords.Contains(namePart, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }
            // Drop a trailing status word folded onto the activity name.
            foreach (var s in StatusWords)
            {
                if (namePart.EndsWith(" " + s, StringComparison.OrdinalIgnoreCase))
                {
                    namePart = namePart[..^(s.Length + 1)].Trim();
                }
            }
            if (namePart.Length >= 4 && seen.Add(namePart))
            {
                activities.Add(new LicenseActivity(namePart, status));
            }
        }
        return activities;
    }

    // ---- Members / partners --------------------------------------------------------------------

    /// <summary>Parses the owners/members section. Handles both the older "Partners" / "ملحق الشركاء"
    /// annex (serial + name + share%) and the newer inline "License Members" / "الأطراف" table
    /// (No., Name, Nationality, Role, Share — share often blank for a manager/agent).</summary>
    private static List<LicenseMember> ExtractMembers(string text)
    {
        var start = FirstIndexOf(text, "اصحاب", "الاطراف", "License Members", "Partners");
        var scope = start >= 0 ? text[start..] : text;
        var end = EarliestIndexOf(scope, "License Activities", "نشاط الرخصة", "الملاحظات", "Remarks", "Print Date");
        if (end > 0) { scope = scope[..end]; }

        var members = new List<LicenseMember>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string rawName, string? serial, string? share, string? role, string? nationality)
        {
            var name = CleanName(rawName);
            if (name.Length < 3
                || name.Contains('&') || name.Contains('®')
                || name.Contains("D-U-N-S", StringComparison.OrdinalIgnoreCase)
                || name.Contains("License", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Members", StringComparison.OrdinalIgnoreCase)
                || KnownRoles.Any(r => name.Equals(r, StringComparison.OrdinalIgnoreCase))
                || KnownNationalities.Any(n => name.Equals(n, StringComparison.OrdinalIgnoreCase))
                || !seen.Add(name))
            {
                return;
            }
            members.Add(new LicenseMember(
                name,
                string.IsNullOrWhiteSpace(serial) ? null : serial.Trim(),
                nationality,
                role,
                string.IsNullOrWhiteSpace(share) ? null : share.Trim()));
        }

        // Pass 1 — share-anchored rows (classic annex, and any inline row that prints a share).
        foreach (Match m in PartnerRow.Matches(scope))
        {
            var window = Slice(scope, m.Index - 40, m.Index + m.Length + 40);
            Add(m.Groups[2].Value, m.Groups[1].Value, m.Groups[3].Value,
                FirstContained(window, KnownRoles), FirstContained(window, KnownNationalities));
        }

        // Pass 2 — inline "License Members" rows anchored on the all-caps name, so share-less members
        // (a manager/agent) are still captured. Serial/share/role/nationality read from a side window.
        foreach (Match m in MemberName.Matches(scope))
        {
            var before = Slice(scope, m.Index - 30, m.Index);
            var after = Slice(scope, m.Index + m.Length, m.Index + m.Length + 90);
            var serials = SerialNumber.Matches(before);
            var shareMatch = SharePercent.Match(after);
            Add(m.Value,
                serials.Count > 0 ? serials[^1].Value : null,
                shareMatch.Success ? shareMatch.Groups[1].Value : null,
                FirstContained(after, KnownRoles),
                FirstContained(after, KnownNationalities));
        }

        return members;
    }

    private static string CleanName(string raw)
    {
        var name = Regex.Replace(raw, @"\s+", " ").Trim().Trim('.', ',', '-', '/').Trim();
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var token in KnownRoles.Concat(KnownNationalities).Concat(StatusWords))
            {
                if (name.Length > token.Length
                    && name.EndsWith(token, StringComparison.OrdinalIgnoreCase)
                    && char.IsWhiteSpace(name[name.Length - token.Length - 1]))
                {
                    name = name[..^token.Length].Trim().Trim('.', ',', '-', '/').Trim();
                    changed = true;
                    break;
                }
            }
        }
        return name;
    }

    // ---- Small text utilities ------------------------------------------------------------------

    private static string? FirstContained(string haystack, string[] options)
    {
        foreach (var option in options)
        {
            if (haystack.Contains(option, StringComparison.OrdinalIgnoreCase))
            {
                return option;
            }
        }
        return null;
    }

    private static string Slice(string s, int from, int to)
    {
        from = Math.Max(0, from);
        to = Math.Min(s.Length, to);
        return from < to ? s[from..to] : "";
    }

    private static int FirstIndexOf(string text, params string[] needles)
    {
        foreach (var needle in needles)
        {
            var i = text.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
            if (i >= 0) { return i; }
        }
        return -1;
    }

    private static int EarliestIndexOf(string text, params string[] needles)
    {
        var best = -1;
        foreach (var needle in needles)
        {
            var i = text.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
            if (i >= 0 && (best < 0 || i < best)) { best = i; }
        }
        return best;
    }
}
