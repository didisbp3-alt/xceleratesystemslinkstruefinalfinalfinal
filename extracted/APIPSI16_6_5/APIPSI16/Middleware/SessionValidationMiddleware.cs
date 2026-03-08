using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using APIPSI16.Data;
using APIPSI16.Services;

namespace APIPSI16.Middleware
{
    public class SessionValidationMiddleware
    {
        private readonly RequestDelegate _next;

        public SessionValidationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ISessionService sessionService, xcleratesystemslinks_SampleDBContext db)
        {
            var path = context.Request.Path.Value ?? "";
            if (path.Contains("/api/auth/login") || path.Contains("/api/auth/register") || path.Contains("/api/auth/login-debug"))
            {
                await _next(context);
                return;
            }

            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var token = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

            if (!string.IsNullOrWhiteSpace(userIdClaim) && !string.IsNullOrWhiteSpace(token) && int.TryParse(userIdClaim, out var userId))
            {
                var session = await db.Sessions
                    .FirstOrDefaultAsync(s => s.Token == token && s.UserId == userId);

                if (session == null)
                {
                    // Just return 401 - don't try to sign out
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsJsonAsync(new { error = "Session not found" });
                    return;
                }

                if (!session.IsActive)
                {
                    // Just return 401 - don't try to sign out
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsJsonAsync(new { error = "Session has been invalidated. Please log in again." });
                    return;
                }

                if (session.ExpiresAt < DateTime.UtcNow)
                {
                    session.IsActive = false;
                    session.InvalidatedAt = DateTime.UtcNow;
                    db.Sessions.Update(session);
                    await db.SaveChangesAsync();

                    // Just return 401 - don't try to sign out
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsJsonAsync(new { error = "Session expired" });
                    return;
                }
            }

            await _next(context);
        }
    }
}