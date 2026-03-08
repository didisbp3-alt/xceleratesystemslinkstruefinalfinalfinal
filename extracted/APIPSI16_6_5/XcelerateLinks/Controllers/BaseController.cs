using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using APIPSI16.Services;

namespace XcelerateLinks.Mvc.Controllers
{
    public abstract class BaseController : Controller
    {
        protected readonly ISessionService _sessionService;
        protected const string CookieName = "ApiAccessToken";

        public BaseController(ISessionService sessionService = null)
        {
            _sessionService = sessionService;
        }

        protected async Task<bool> ValidateSessionAsync()
        {
            if (_sessionService == null)
                return true; // No session service injected

            if (User.Identity?.IsAuthenticated != true)
                return true;

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!Request.Cookies.TryGetValue(CookieName, out var token) ||
                string.IsNullOrWhiteSpace(userIdClaim) ||
                !int.TryParse(userIdClaim, out var userId))
            {
                return true;
            }

            bool isValidSession = await _sessionService.IsSessionValidAsync(userId, token);

            if (!isValidSession)
            {
                Response.Cookies.Delete(CookieName);
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                TempData["ErrorMessage"] = "Sua sessão foi encerrada. Por favor, faça login novamente.";
                return false;
            }

            return true;
        }
    }
}