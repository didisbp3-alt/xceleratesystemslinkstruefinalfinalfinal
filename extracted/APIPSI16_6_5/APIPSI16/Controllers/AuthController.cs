using APIPSI16.Data;
using APIPSI16.Models;
using APIPSI16.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace APIPSI16.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly xcleratesystemslinks_SampleDBContext _db;
        private readonly ITokenService _tokenService;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly ILogger<AuthController> _logger;
        private readonly IWebHostEnvironment _env;

        public AuthController(
            xcleratesystemslinks_SampleDBContext db,
            ITokenService tokenService,
            IPasswordHasher<User> passwordHasher,
            ILogger<AuthController> logger,
            IWebHostEnvironment env)
        {
            _db = db;
            _tokenService = tokenService;
            _passwordHasher = passwordHasher;
            _logger = logger;
            _env = env;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest("username and password required");

            var normalized = req.Username.Trim().ToLowerInvariant();

            var user = await _db.Users
                .FirstOrDefaultAsync(u =>
                    (u.Email != null && u.Email.ToLower() == normalized) ||
                    (u.Username != null && u.Username.ToLower() == normalized));

            if (user == null)
            {
                _logger.LogInformation("Login failed: user not found for '{Username}'", req.Username);
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                _logger.LogWarning("Login failed: user {UserId} has empty password hash", user.UserId);
                return Unauthorized();
            }

            var verify = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, req.Password);
            _logger.LogDebug("Password verification result for user {UserId}: {Result}", user.UserId, verify.ToString());

            if (verify == PasswordVerificationResult.Failed)
            {
                return Unauthorized();
            }

            if (verify == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordHash = _passwordHasher.HashPassword(user, req.Password);
                _db.Users.Update(user);
                await _db.SaveChangesAsync();
                _logger.LogInformation("Password rehashed for user {UserId}", user.UserId);
            }

            //Invalidate all previous active sessions for this user
            var previousSessions = await _db.Sessions
                .Where(s => s.UserId == user.UserId && s.IsActive)
                .ToListAsync();

            if (previousSessions.Count > 0)
            {
                foreach (var session in previousSessions)
                {
                    session.IsActive = false;
                    session.InvalidatedAt = DateTime.UtcNow;
                }
                _db.Sessions.UpdateRange(previousSessions);
                await _db.SaveChangesAsync();
                _logger.LogInformation("Invalidated {Count} previous sessions for user {UserId}", previousSessions.Count, user.UserId);
            }

            // Build claims (include role)
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.Name ?? user.Email ?? string.Empty),
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Role, user.Role?.ToString() ?? "1") // 0=Admin, 1=User, default to User
            };

            // Wrap token creation to surface detailed errors in Development
            string token;
            DateTime expires;
            try
            {
                token = _tokenService.CreateToken(user.UserId.ToString(), claims);
                expires = _tokenService.GetLastExpiry();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token creation failed for user {UserId}", user.UserId);

                // In development show full exception, otherwise return a generic problem
                if (_env.IsDevelopment())
                {
                    return Problem(detail: ex.ToString(), title: "Token creation failed", statusCode: 500);
                }

                return Problem(title: "Token creation failed", statusCode: 500);
            }

            var sessionService = HttpContext.RequestServices.GetRequiredService<ISessionService>();
            await sessionService.CreateSessionAsync(user.UserId, token, expires);
            _logger.LogInformation("Session created for user {UserId}", user.UserId);

            return Ok(new { token, expiresAt = expires });
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var token = HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

            if (!string.IsNullOrWhiteSpace(token))
            {
                var sessionService = HttpContext.RequestServices.GetRequiredService<ISessionService>();
                await sessionService.InvalidateSessionAsync(token);
            }

            return Ok(new { message = "Logged out successfully" });
        }

        // Register endpoint - allow public registration but force role to User (1) unless caller is admin
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest("Email and password are required.");

            if (await _db.Users.AnyAsync(u => u.Email == req.Email))
                return Conflict("Email already in use.");

            if (!string.IsNullOrWhiteSpace(req.Username) &&
                await _db.Users.AnyAsync(u => u.Username != null && u.Username.ToLower() == req.Username.Trim().ToLowerInvariant()))
                return Conflict("Username already in use.");

            var requestedRole = req.Role ?? 1;
            var callerRole = User.FindFirst(ClaimTypes.Role)?.Value;

            // If caller is not admin (0), force role to User (1)
            if (callerRole != "0" && requestedRole != 1)
            {
                _logger.LogWarning("Non-admin attempted to register with role {Role}. Forcing to User (1).", requestedRole);
                requestedRole = 1;
            }

            var user = new User
            {
                Name = req.Name,
                Email = req.Email,
                Username = string.IsNullOrWhiteSpace(req.Username) ? null : req.Username.Trim(),
                PhoneNumber = req.PhoneNumber,
                Nationality = req.Nationality,
                JobPreference = req.JobPreference,
                ProfileBio = req.ProfileBio,
                DoB = req.DoB,
                Role = requestedRole
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, req.Password);

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            _logger.LogInformation("User registered: {UserId}, {Email}, Role: {Role}", user.UserId, user.Email, user.Role);

            return CreatedAtAction(nameof(Register), new { id = user.UserId }, new { id = user.UserId, email = user.Email, role = user.Role });
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.NewPassword))
                return BadRequest("Email and new password are required.");

            if (req.NewPassword.Length < 8)
                return BadRequest("Password must be at least 8 characters.");

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
            if (user == null)
                return NotFound("User not found.");

            // Hash and update password
            user.PasswordHash = _passwordHasher.HashPassword(user, req.NewPassword);
            _db.Users.Update(user);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Password reset for user {UserId}", user.UserId);
            return Ok(new { message = "Password reset successfully" });
        }

        // Useful debug endpoint to check password verification and role on a user
        [HttpPost("login-debug-verify")]
        [AllowAnonymous]
        public async Task<IActionResult> LoginDebugVerify([FromBody] LoginRequest req)
        {
            if (req == null) return BadRequest("Request body required.");
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest("Username and password required.");

            try
            {
                var user = await _db.Users.SingleOrDefaultAsync(u =>
                    u.Username == req.Username || u.Email == req.Username);

                if (user == null)
                {
                    _logger.LogInformation("DebugVerify: user not found: {Username}", req.Username);
                    return Unauthorized(new { error = "Invalid credentials" });
                }

                if (string.IsNullOrEmpty(user.PasswordHash))
                {
                    _logger.LogWarning("DebugVerify: user {Id} has no PasswordHash.", user.UserId);
                    return Unauthorized(new { error = "Invalid credentials" });
                }

                var verify = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, req.Password);
                _logger.LogInformation("DebugVerify: Verify result for user {Id}: {Result}", user.UserId, verify);

                return Ok(new { verified = (verify != PasswordVerificationResult.Failed), result = verify.ToString(), role = user.Role });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DebugVerify: unhandled exception");
                throw;
            }
        }

        [HttpPost("login-debug-token")]
        [AllowAnonymous]
        public IActionResult LoginDebugToken([FromBody] object? _ = null)
        {
            try
            {
                var claims = new[]
                {
                    new Claim(ClaimTypes.Name, "debug"),
                    new Claim(ClaimTypes.NameIdentifier, "9999"),
                    new Claim(ClaimTypes.Role, "0") // Debug token has admin role
                };

                var token = _tokenService.CreateToken("9999", claims);
                var expires = _tokenService.GetLastExpiry();

                return Ok(new { token, expiresAt = expires });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DebugToken: token creation failed");
                if (_env.IsDevelopment())
                {
                    return Problem(detail: ex.ToString(), title: "Token generation failed", statusCode: 500);
                }
                return Problem(title: "Token generation failed", statusCode: 500);
            }
        }
    }

    // DTOs
    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class ResetPasswordRequest
    {
        public string Email { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class RegisterRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Username { get; set; }
        public string? PhoneNumber { get; set; }
        public int? Nationality { get; set; }
        public int? JobPreference { get; set; }
        public string? ProfileBio { get; set; }
        public DateOnly? DoB { get; set; }
        public int? Role { get; set; } = 1;
        public string Password { get; set; } = string.Empty;
    }
}