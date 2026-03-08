using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace APIPSI16.Services
{
    public interface ITokenService
    {
        string CreateToken(string subject, IEnumerable<Claim>? additionalClaims = null);
        DateTime GetLastExpiry();
    }

    public class TokenService : ITokenService
    {
        private readonly IConfiguration _config;
        private DateTime _lastExpiry = DateTime.UtcNow;

        public TokenService(IConfiguration config)
        {
            _config = config;
        }

        public string CreateToken(string subject, IEnumerable<Claim>? additionalClaims = null)
        {
            // Read key from env var first then config
            var keyBase64 = Environment.GetEnvironmentVariable("XCELERATE_JWT_KEY")
                            ?? _config["Jwt:Key"];

            if (string.IsNullOrWhiteSpace(keyBase64))
                throw new InvalidOperationException("JWT key not configured. Set XCELERATE_JWT_KEY or Jwt:Key.");

            byte[] keyBytes;
            try
            {
                keyBytes = Convert.FromBase64String(keyBase64);
            }
            catch (FormatException fx)
            {
                throw new InvalidOperationException("JWT key is not valid base64. Check XCELERATE_JWT_KEY or Jwt:Key value.", fx);
            }

            var issuer = _config["Jwt:Issuer"] ?? "xcelerate-links-api";
            var audience = _config["Jwt:Audience"] ?? "xcelerate-links-clients";

            
            var expireRaw = _config["Jwt:ExpireMinutes"];
            int expireMinutes;
            if (!int.TryParse(expireRaw, out expireMinutes) || expireMinutes <= 0)
            {
                expireMinutes = 60;
            }

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, subject),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            if (additionalClaims != null)
                claims.AddRange(additionalClaims);

            var creds = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);
            var expires = DateTime.UtcNow.AddMinutes(expireMinutes);
            _lastExpiry = expires;

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expires,
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public DateTime GetLastExpiry() => _lastExpiry;
    }
}