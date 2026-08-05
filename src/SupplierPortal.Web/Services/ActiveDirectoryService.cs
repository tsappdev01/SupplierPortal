using System.DirectoryServices.Protocols;
using System.Net;
using Microsoft.Extensions.Options;

namespace SupplierPortal.Web.Services;

/// <summary>Bound from the "ActiveDirectory" configuration section (secrets belong in
/// appsettings.Local.json on the server, never in source control).</summary>
public class ActiveDirectoryOptions
{
    /// <summary>Master switch. When false, no Active Directory logins are attempted.</summary>
    public bool Enabled { get; set; }

    /// <summary>A domain controller host, or the domain name itself for DC locator (e.g. "dipark.local").</summary>
    public string Host { get; set; } = "";

    /// <summary>636 for LDAPS (recommended), 389 for plain LDAP.</summary>
    public int Port { get; set; } = 636;

    /// <summary>Use SSL/TLS. Strongly recommended so passwords are not sent in clear text.</summary>
    public bool UseLdaps { get; set; } = true;

    /// <summary>AD domain FQDN, used to build a UPN when the user types only their short name (e.g. "dipark.local").</summary>
    public string Domain { get; set; } = "";

    /// <summary>NetBIOS domain name, used to build DOMAIN\user when LoginFormat is sAMAccountName (e.g. "DIPARK").</summary>
    public string? NetbiosName { get; set; }

    /// <summary>"UPN" (user@domain) or "sAMAccountName" (DOMAIN\user) - how bare usernames are qualified.</summary>
    public string LoginFormat { get; set; } = "UPN";
}

/// <summary>Verifies a user's credentials against on-prem Active Directory by binding to LDAP as that
/// user. Authorization (role, business units) still comes from the local Users table - AD only proves
/// identity. Registered as a singleton; it holds no per-request state.</summary>
public class ActiveDirectoryService(IOptions<ActiveDirectoryOptions> options)
{
    private readonly ActiveDirectoryOptions _options = options.Value;

    public bool Enabled => _options.Enabled;

    /// <summary>Binds to AD as the given login to verify the password. Returns (true, null) on success.
    /// Never reveals whether it was the username or password that was wrong.</summary>
    public (bool Success, string? Error) ValidateCredentials(string login, string password)
    {
        if (!_options.Enabled)
        {
            return (false, "Active Directory authentication is not enabled.");
        }
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrEmpty(password))
        {
            return (false, "Invalid credentials.");
        }

        var bindName = QualifyLogin(login);

        try
        {
            using var connection = new LdapConnection(new LdapDirectoryIdentifier(_options.Host, _options.Port))
            {
                AuthType = AuthType.Basic
            };
            connection.SessionOptions.ProtocolVersion = 3;
            connection.SessionOptions.SecureSocketLayer = _options.UseLdaps;
            connection.Timeout = TimeSpan.FromSeconds(15);

            // A successful bind with the supplied password proves the credentials are valid.
            connection.Bind(new NetworkCredential(bindName, password));
            return (true, null);
        }
        catch (LdapException)
        {
            // Bad username or password, or the directory rejected the bind - do not distinguish.
            return (false, "Invalid username or password.");
        }
        catch (Exception ex)
        {
            // Connectivity / TLS / configuration problem - surface a generic message (details go to logs).
            return (false, $"Could not reach Active Directory: {ex.Message}");
        }
    }

    /// <summary>Turns a bare username into the configured bind form; leaves an already-qualified
    /// login (contains '@' or '\') untouched.</summary>
    private string QualifyLogin(string login)
    {
        login = login.Trim();
        if (login.Contains('@') || login.Contains('\\'))
        {
            return login;
        }

        if (string.Equals(_options.LoginFormat, "sAMAccountName", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(_options.NetbiosName))
        {
            return $"{_options.NetbiosName}\\{login}";
        }

        return string.IsNullOrWhiteSpace(_options.Domain) ? login : $"{login}@{_options.Domain}";
    }
}
