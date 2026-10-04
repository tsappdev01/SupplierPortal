namespace SupplierPortal.SelfService.Services;

/// <summary>Static option lists ("LOVs") for the CST customer wizard's lookup fields, sourced from the
/// customer LOV workbook supplied by the business. Keyed by (Section, FieldKey) so the same key in
/// different sections resolves to the right list — e.g. "TypeOfCustomer" is Individual/Organization on
/// the Account but Normal/VIP on the Contact. A field with no list returns an empty list (the wizard then
/// falls back to a free-text box), so adding a new lookup is a one-line change here.</summary>
public static class CustomerLookups
{
    /// <summary>ISO 3166-1 alpha-2 code + display name, from the workbook's Country columns.</summary>
    public static readonly (string Code, string Name)[] Countries =
    {
        ("AD", "Andorra"),
        ("AE", "United Arab Emirates"),
        ("AF", "Afghanistan"),
        ("AG", "Antigua and Barbuda"),
        ("AI", "Anguilla"),
        ("AL", "Albania"),
        ("AM", "Armenia"),
        ("AO", "Angola"),
        ("AQ", "Antarctica"),
        ("AR", "Argentina"),
        ("AS", "American Samoa"),
        ("AT", "Austria"),
        ("AU", "Australia"),
        ("AW", "Aruba"),
        ("AX", "Åland Islands"),
        ("AZ", "Azerbaijan"),
        ("BA", "Bosnia and Herzegovina"),
        ("BB", "Barbados"),
        ("BD", "Bangladesh"),
        ("BE", "Belgium"),
        ("BF", "Burkina Faso"),
        ("BG", "Bulgaria"),
        ("BH", "Bahrain"),
        ("BI", "Burundi"),
        ("BJ", "Benin"),
        ("BL", "Saint Barthélemy"),
        ("BM", "Bermuda"),
        ("BN", "Brunei Darussalam"),
        ("BO", "Bolivia, Plurinational State of"),
        ("BQ", "Bonaire, Sint Eustatius and Saba"),
        ("BR", "Brazil"),
        ("BS", "Bahamas (The)"),
        ("BT", "Bhutan"),
        ("BV", "Bouvet Island"),
        ("BW", "Botswana"),
        ("BY", "Belarus"),
        ("BZ", "Belize"),
        ("CA", "Canada"),
        ("CC", "Cocos (Keeling) Islands"),
        ("CD", "Congo, the Democratic Republic of the"),
        ("CF", "Central African Republic"),
        ("CG", "Congo"),
        ("CH", "Switzerland"),
        ("CI", "Côte d'Ivoire"),
        ("CK", "Cook Islands"),
        ("CL", "Chile"),
        ("CM", "Cameroon"),
        ("CN", "China"),
        ("CO", "Colombia"),
        ("CR", "Costa Rica"),
        ("CU", "Cuba"),
        ("CV", "Cabo Verde"),
        ("CW", "Curaçao"),
        ("CX", "Christmas Island"),
        ("CY", "Cyprus"),
        ("CZ", "Czechia"),
        ("DE", "Germany"),
        ("DJ", "Djibouti"),
        ("DK", "Denmark"),
        ("DM", "Dominica"),
        ("DO", "Dominican Republic"),
        ("DZ", "Algeria"),
        ("EC", "Ecuador"),
        ("EE", "Estonia"),
        ("EG", "Egypt"),
        ("EH", "Western Sahara"),
        ("ER", "Eritrea"),
        ("ES", "Spain"),
        ("ET", "Ethiopia"),
        ("FI", "Finland"),
        ("FJ", "Fiji"),
        ("FK", "Falkland Islands (Malvinas)"),
        ("FM", "Micronesia, Federated States of"),
        ("FO", "Faroe Islands"),
        ("FR", "France"),
        ("GA", "Gabon"),
        ("GB", "United Kingdom of Great Britain and Northern Ireland"),
        ("GD", "Grenada"),
        ("GE", "Georgia"),
        ("GF", "French Guiana"),
        ("GG", "Guernsey"),
        ("GH", "Ghana"),
        ("GI", "Gibraltar"),
        ("GL", "Greenland"),
        ("GM", "Gambia"),
        ("GN", "Guinea"),
        ("GP", "Guadeloupe"),
        ("GQ", "Equatorial Guinea"),
        ("GR", "Greece"),
        ("GS", "South Georgia and the South Sandwich Islands"),
        ("GT", "Guatemala"),
        ("GU", "Guam"),
        ("GW", "Guinea-Bissau"),
        ("GY", "Guyana"),
        ("HK", "Hong Kong"),
        ("HM", "Heard Island and McDonald Islands"),
        ("HN", "Honduras"),
        ("HR", "Croatia"),
        ("HT", "Haiti"),
        ("HU", "Hungary"),
        ("ID", "Indonesia"),
        ("IE", "Ireland"),
        ("IL", "Israel"),
        ("IM", "Isle of Man"),
        ("IN", "India"),
        ("IO", "British Indian Ocean Territory"),
        ("IQ", "Iraq"),
        ("IR", "Iran, Islamic Republic of"),
        ("IS", "Iceland"),
        ("IT", "Italy"),
        ("JE", "Jersey"),
        ("JM", "Jamaica"),
        ("JO", "Jordan"),
        ("JP", "Japan"),
        ("KE", "Kenya"),
        ("KG", "Kyrgyzstan"),
        ("KH", "Cambodia"),
        ("KI", "Kiribati"),
        ("KM", "Comoros"),
        ("KN", "Saint Kitts and Nevis"),
        ("KP", "Korea, Democratic People's Republic of"),
        ("KR", "Korea, Republic of"),
        ("KW", "Kuwait"),
        ("KY", "Cayman Islands"),
        ("KZ", "Kazakhstan"),
        ("LA", "Lao People's Democratic Republic"),
        ("LB", "Lebanon"),
        ("LC", "Saint Lucia"),
        ("LI", "Liechtenstein"),
        ("LK", "Sri Lanka"),
        ("LR", "Liberia"),
        ("LS", "Lesotho"),
        ("LT", "Lithuania"),
        ("LU", "Luxembourg"),
        ("LV", "Latvia"),
        ("LY", "Libya"),
        ("MA", "Morocco"),
        ("MC", "Monaco"),
        ("MD", "Moldova, Republic of"),
        ("ME", "Montenegro"),
        ("MF", "Saint Martin (French part)"),
        ("MG", "Madagascar"),
        ("MH", "Marshall Islands"),
        ("MK", "Macedonia, the former Yugoslav Republic of"),
        ("ML", "Mali"),
        ("MM", "Myanmar"),
        ("MN", "Mongolia"),
        ("MO", "Macao"),
        ("MP", "Northern Mariana Islands"),
        ("MQ", "Martinique"),
        ("MR", "Mauritania"),
        ("MS", "Montserrat"),
        ("MT", "Malta"),
        ("MU", "Mauritius"),
        ("MV", "Maldives"),
        ("MW", "Malawi"),
        ("MX", "Mexico"),
        ("MY", "Malaysia"),
        ("MZ", "Mozambique"),
        ("NA", "Namibia"),
        ("NC", "New Caledonia"),
        ("NE", "Niger"),
        ("NF", "Norfolk Island"),
        ("NG", "Nigeria"),
        ("NI", "Nicaragua"),
        ("NL", "Netherlands"),
        ("NO", "Norway"),
        ("NP", "Nepal"),
        ("NR", "Nauru"),
        ("NU", "Niue"),
        ("NZ", "New Zealand"),
        ("OM", "Oman"),
        ("PA", "Panama"),
        ("PE", "Peru"),
        ("PF", "French Polynesia"),
        ("PG", "Papua New Guinea"),
        ("PH", "Philippines"),
        ("PK", "Pakistan"),
        ("PL", "Poland"),
        ("PM", "Saint Pierre and Miquelon"),
        ("PN", "Pitcairn"),
        ("PR", "Puerto Rico"),
        ("PS", "Palestine, State of"),
        ("PT", "Portugal"),
        ("PW", "Palau"),
        ("PY", "Paraguay"),
        ("QA", "Qatar"),
        ("RE", "Réunion"),
        ("RO", "Romania"),
        ("RS", "Serbia"),
        ("RU", "Russian Federation"),
        ("RW", "Rwanda"),
        ("SA", "Saudi Arabia"),
        ("SB", "Solomon Islands"),
        ("SC", "Seychelles"),
        ("SD", "Sudan"),
        ("SE", "Sweden"),
        ("SG", "Singapore"),
        ("SH", "Saint Helena, Ascension and Tristan da Cunha"),
        ("SI", "Slovenia"),
        ("SJ", "Svalbard and Jan Mayen"),
        ("SK", "Slovakia"),
        ("SL", "Sierra Leone"),
        ("SM", "San Marino"),
        ("SN", "Senegal"),
        ("SO", "Somalia"),
        ("SR", "Suriname"),
        ("SS", "South Sudan"),
        ("ST", "Sao Tome and Principe"),
        ("SV", "El Salvador"),
        ("SX", "Sint Maarten (Dutch part)"),
        ("SY", "Syrian Arab Republic"),
        ("SZ", "Swaziland"),
        ("TC", "Turks and Caicos Islands"),
        ("TD", "Chad"),
        ("TF", "French Southern Territories"),
        ("TG", "Togo"),
        ("TH", "Thailand"),
        ("TJ", "Tajikistan"),
        ("TK", "Tokelau"),
        ("TL", "Timor-Leste"),
        ("TM", "Turkmenistan"),
        ("TN", "Tunisia"),
        ("TO", "Tonga"),
        ("TR", "Turkey"),
        ("TT", "Trinidad and Tobago"),
        ("TV", "Tuvalu"),
        ("TW", "Taiwan, Province of China"),
        ("TZ", "Tanzania, United Republic of"),
        ("UA", "Ukraine"),
        ("UG", "Uganda"),
        ("UM", "United States Minor Outlying Islands"),
        ("US", "United States of America"),
        ("UY", "Uruguay"),
        ("UZ", "Uzbekistan"),
        ("VA", "Holy See"),
        ("VC", "Saint Vincent and the Grenadines"),
        ("VE", "Venezuela, Bolivarian Republic of"),
        ("VG", "Virgin Islands, British"),
        ("VI", "Virgin Islands, U.S."),
        ("VN", "Viet Nam"),
        ("VU", "Vanuatu"),
        ("WF", "Wallis and Futuna"),
        ("WS", "Samoa"),
        ("YE", "Yemen"),
        ("YT", "Mayotte"),
        ("ZA", "South Africa"),
        ("ZM", "Zambia"),
        ("ZW", "Zimbabwe"),
        ("1A", "Kosovo"),
        ("XI", "United Kingdom (Northern Ireland)"),
    };

    private static readonly string[] CountryNames = Countries.Select(c => c.Name).ToArray();
    private static readonly string[] CountryCodes = Countries.Select(c => c.Code).ToArray();

    /// <summary>ISO-2 → international dialing code, for the phone controls. Comprehensive common set;
    /// countries without an entry simply don't appear in the dial-code dropdown.</summary>
    private static readonly Dictionary<string, string> DialByIso = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AE"]="971",["SA"]="966",["QA"]="974",["KW"]="965",["BH"]="973",["OM"]="968",["YE"]="967",
        ["EG"]="20",["JO"]="962",["LB"]="961",["SY"]="963",["IQ"]="964",["IR"]="98",["IL"]="972",["PS"]="970",
        ["TR"]="90",["PK"]="92",["IN"]="91",["BD"]="880",["LK"]="94",["NP"]="977",["AF"]="93",["CN"]="86",
        ["HK"]="852",["TW"]="886",["JP"]="81",["KR"]="82",["SG"]="65",["MY"]="60",["ID"]="62",["TH"]="66",
        ["PH"]="63",["VN"]="84",["KH"]="855",["MM"]="95",["KZ"]="7",["UZ"]="998",["AZ"]="994",["GE"]="995",
        ["US"]="1",["CA"]="1",["MX"]="52",["BR"]="55",["AR"]="54",["CL"]="56",["CO"]="57",["PE"]="51",["VE"]="58",
        ["GB"]="44",["IE"]="353",["FR"]="33",["DE"]="49",["ES"]="34",["PT"]="351",["IT"]="39",["NL"]="31",
        ["BE"]="32",["LU"]="352",["CH"]="41",["AT"]="43",["SE"]="46",["NO"]="47",["DK"]="45",["FI"]="358",
        ["IS"]="354",["PL"]="48",["CZ"]="420",["SK"]="421",["HU"]="36",["RO"]="40",["BG"]="359",["GR"]="30",
        ["RU"]="7",["UA"]="380",["BY"]="375",["HR"]="385",["RS"]="381",["SI"]="386",["CY"]="357",["MT"]="356",
        ["ZA"]="27",["NG"]="234",["KE"]="254",["GH"]="233",["ET"]="251",["TZ"]="255",["UG"]="256",["MA"]="212",
        ["DZ"]="213",["TN"]="216",["LY"]="218",["SD"]="249",["SO"]="252",["SN"]="221",["CI"]="225",["CM"]="237",
        ["AU"]="61",["NZ"]="64",["FJ"]="679",["AD"]="376",["MC"]="377",["LI"]="423",["SM"]="378",
    };

    /// <summary>The seven UAE emirates — the City option list shown when the country is the UAE.</summary>
    public static readonly string[] UaeEmirates =
    {
        "Abu Dhabi", "Dubai", "Sharjah", "Ajman", "Umm Al Quwain", "Ras Al Khaimah", "Fujairah"
    };

    /// <summary>The country name that switches the City field to the UAE-emirate dropdown.</summary>
    public const string UaeCountryName = "United Arab Emirates";

    /// <summary>E-Invoicing Wave applicability options for the VAT Group entities grid (Wave 1 / Wave 2).</summary>
    public static readonly string[] EInvoicingWaves = { "Wave 1", "Wave 2" };

    /// <summary>Business Location Type options for the VAT Group entities grid (Mainland / Free Zone).</summary>
    public static readonly string[] BusinessLocationTypes = { "Mainland", "Free Zone" };

    /// <summary>Countries that carry a dial code, as (Iso, Dial, Name), UAE first then alphabetical by name —
    /// the option list for the customer phone controls.</summary>
    public static readonly (string Iso, string Dial, string Name)[] PhoneCountries =
        Countries.Where(c => DialByIso.ContainsKey(c.Code))
                 .Select(c => (c.Code, DialByIso[c.Code], c.Name))
                 .OrderByDescending(c => c.Code == "AE")
                 .ThenBy(c => c.Name)
                 .ToArray();
    /// <summary>Combined "Code - Name" options, e.g. "AE - United Arab Emirates".</summary>
    private static readonly string[] CountryCodeNames = Countries.Select(c => $"{c.Code} - {c.Name}").ToArray();

    /// <summary>UAE emirates — display names (used elsewhere, e.g. Emirate/City).</summary>
    private static readonly string[] Emirates =
        { "Abu Dhabi", "Dubai", "Sharjah", "Umm Al Quwain", "Fujairah", "Ajman", "Ras Al Khaimah" };

    /// <summary>Country Subdivision as (code → name): dropdown shows the Name, stored / exported value is the Code.</summary>
    public static readonly (string Code, string Name)[] CountrySubdivisions =
    {
        ("AUH", "Abu Dhabi"), ("DXB", "Dubai"), ("SHJ", "Sharjah"), ("UAQ", "Umm Al Quwain"),
        ("FUJ", "Fujairah"), ("AJM", "Ajman"), ("RAK", "Ras Al Khaimah"),
    };
    private static readonly string[] CountrySubdivisionCodes = CountrySubdivisions.Select(c => c.Code).ToArray();

    public static IReadOnlyList<(string Value, string Text)>? CodedOptions(string section, string fieldKey) =>
        (section, fieldKey) switch
        {
            ("Account", "CountrySubdivision") => CountrySubdivisions.Select(c => (c.Code, c.Name)).ToList(),
            _ => null
        };

    public static string CodedDisplay(string section, string fieldKey, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var coded = CodedOptions(section, fieldKey);
        if (coded is null) return value;
        var hit = coded.FirstOrDefault(o => string.Equals(o.Value, value, StringComparison.OrdinalIgnoreCase));
        return hit.Value is null ? value : hit.Text;
    }

    /// <summary>UAE trade-license issuing authorities (economic departments + free zones), from the workbook.</summary>
    private static readonly string[] LicensingAuthorities =
    {
        "Dubai Department of Economy and Tourism (DET) (formerly DED)",
        "Abu Dhabi Department of Economic Development (ADDED)",
        "Sharjah Economic Development Department (SEDD)",
        "Ajman Department of Economic Development (Ajman DED)",
        "Ras Al Khaimah Department of Economic Development (RAK DED)",
        "Fujairah Municipality – Commercial Licensing Department",
        "Umm Al Quwain Department of Economic Development",
        "Jebel Ali Free Zone Authority (JAFZA)",
        "Dubai Multi Commodities Centre (DMCC)",
        "Dubai Airport Freezone Authority (DAFZA)",
        "Dubai Silicon Oasis Authority",
        "Dubai South",
        "International Free Zone Authority (IFZA)",
        "Sharjah Airport International Free Zone (SAIF Zone)",
        "Sharjah Media City (Shams)",
        "Ras Al Khaimah Economic Zone (RAKEZ)",
        "Ajman Free Zone",
        "Abu Dhabi Global Market (ADGM)",
        "Khalifa Economic Zones Abu Dhabi (KEZAD)",
        "Others",
    };

    private static readonly string[] Empty = Array.Empty<string>();

    /// <summary>The option list for a lookup field, or an empty list when the key isn't a known lookup.</summary>
    public static IReadOnlyList<string> For(string section, string fieldKey) => (section, fieldKey) switch
    {
        ("Account", "AccountType") => new[] { "Internal", "External" },
        ("Account", "VATStructure") => new[] { "VAT Group", "Individual VAT" },
        ("Account", "TypeOfCustomer") => new[] { "Individual", "Organization" },
        ("Account", "TypeOfOrganization") => new[] { "Business to Business", "Business to Government" },
        ("Account", "EINvoicingWaveApplicability") => new[] { "Wave 1", "Wave 2" },
        ("Account", "BuyerLegalRegistrationIdentifierType") => new[] { "Trade License", "Emirates ID", "Cabinet Decision", "Passport" },
        ("Account", "CustomerType") => new[] { "NON-PMS", "PMS" },
        ("Account", "CountryCode") => CountryCodes,
        ("Account", "CountryName") => CountryNames,
        ("Account", "CountrySubdivision") => CountrySubdivisionCodes,
        ("Account", "IncorporationCountry") => CountryCodeNames,
        ("Account", "LicensingAuthorityName") => LicensingAuthorities,
        ("Account", "TradeLicenseIssuingBody") => new[] { "Mainland", "Free Zone" },
        ("Account", "Owner1Nationality") => CountryNames,
        ("Account", "Owner2Nationality") => CountryNames,
        ("Account", "PaymentTerms") => new[] { "30 Net", "IMMEDIATE", "NET 45", "Net 3", "15 Net", "NET 120", "NET 90" },

        ("Contact", "Title") => new[] { "Mr", "Mrs", "Ms", "M/s", "Dr" },
        ("Contact", "Gender") => new[] { "Male", "Female" },
        ("Contact", "MaritalStatus") => new[] { "Single", "Married" },
        ("Contact", "PreferredLanguage") => new[] { "Arabic", "English", "French", "Hindi/Urdu" },
        ("Contact", "BuyingRole") => new[] { "Key decision maker", "Secondary decision maker", "Supporting role", "Does not matter" },
        ("Contact", "ResidentialStatus") => new[] { "Non UAE Resident", "UAE Resident" },
        ("Contact", "Nationality") => CountryNames,
        ("Contact", "Nationality2") => CountryNames,
        ("Contact", "Country") => CountryNames,
        ("Contact", "IssueCountry") => CountryNames,
        ("Contact", "PassportIssueCountryCode") => CountryCodeNames,
        ("Contact", "PassportIssuePlaceCountry") => CountryCodeNames,
        ("Contact", "TypeOfCustomer") => new[] { "Normal", "VIP" },

        _ => Empty
    };
}
