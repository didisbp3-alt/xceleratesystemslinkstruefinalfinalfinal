using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using APIPSI16.Services;

namespace APIPSI16.Controllers
{
    public class ApiControllerBase : ControllerBase
    {
        protected readonly ISessionService _sessionService;

        public ApiControllerBase(ISessionService sessionService)
        {
            _sessionService = sessionService;
        }

        protected async Task<IActionResult> ValidateSessionAsync()
        {
            if (_sessionService == null)
                return Unauthorized(new { error = "Session validation unavailable." });

            if (User.Identity?.IsAuthenticated != true)
                return Unauthorized(new { error = "Not authenticated." });

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                return Unauthorized(new { error = "Invalid user claim." });

            // Get the token from the Authorization header
            var authHeader = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
                return Unauthorized(new { error = "No token provided." });

            var token = authHeader.Substring("Bearer ".Length).Trim();

            bool isValidSession = await _sessionService.IsSessionValidAsync(userId, token);

            if (!isValidSession)
            {
                return Unauthorized(new { error = "Session has been invalidated. Please log in again." });
            }

            return null; // Session is valid
        }
    }
}