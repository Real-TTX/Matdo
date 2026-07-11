using Microsoft.AspNetCore.DataProtection;

namespace Matdo.Web.Services.Calendar;

/// <summary>Ver-/Entschlüsselt OAuth-Tokens vor der Speicherung (nutzt die persistierten DataProtection-Keys).</summary>
public class TokenProtector
{
    private readonly IDataProtector _protector;

    public TokenProtector(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector("Matdo.Calendar.Tokens.v1");

    public string? Protect(string? plain) =>
        string.IsNullOrEmpty(plain) ? plain : _protector.Protect(plain);

    public string? Unprotect(string? cipher)
    {
        if (string.IsNullOrEmpty(cipher)) return cipher;
        try { return _protector.Unprotect(cipher); }
        catch { return null; }
    }
}
