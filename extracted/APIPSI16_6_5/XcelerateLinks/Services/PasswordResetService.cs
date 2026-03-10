using Microsoft.AspNetCore.DataProtection;
using System;

public class PasswordResetService
{
    private readonly IDataProtector _protector;
    private readonly TimeSpan _tokenLifespan = TimeSpan.FromHours(2);

    public PasswordResetService(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("PasswordReset");
    }

    public string GeneratePasswordResetToken(string email)
    {
        var plainData = $"{email}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        return _protector.Protect(plainData);
    }

    public bool TryValidatePasswordResetToken(string token, out string email)
    {
        email = null!;
        try
        {
            var plainData = _protector.Unprotect(token);
            var parts = plainData.Split('|');
            if (parts.Length != 2)
                return false;

            email = parts[0];
            var ts = DateTimeOffset.FromUnixTimeSeconds(long.Parse(parts[1]));
            if (DateTimeOffset.UtcNow - ts > _tokenLifespan)
                return false;
            return true;
        }
        catch
        {
            return false;
        }
    }
}