using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using APIPSI16.Models.DTOs;
using APIPSI16.Services;

namespace XcelerateLinks.Mvc.Controllers
{
    public class SubscriptionsController : ApiControllerBase
    {
        public SubscriptionsController(IHttpClientFactory httpFactory, ISessionService sessionService)
            : base(httpFactory, sessionService)
        {
        }

        public async Task<IActionResult> Index()
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var uid = GetCurrentUserId();
            if (uid.HasValue)
            {
                var client = CreateAuthorizedClient();
                var resp = await client.GetAsync($"api/users/{uid.Value}");
                if (resp.IsSuccessStatusCode)
                {
                    var user = await resp.Content.ReadFromJsonAsync<UserDTO>();
                    ViewBag.SubscriptionPlan = user?.SubscriptionPlan ?? 0;
                    ViewBag.UserId = uid.Value;
                }
            }

            return View();
        }

        // POST: upgrade or downgrade plan (for demo – in production this would go through payment)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upgrade(int plan)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var uid = GetCurrentUserId();
            if (!uid.HasValue) return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();
            var resp = await client.PutAsJsonAsync($"api/users/{uid.Value}/subscription", new { Plan = plan });

            if (resp.IsSuccessStatusCode)
                TempData["SuccessMessage"] = plan == 0
                    ? "Voltaste ao plano Free."
                    : $"Plano atualizado com sucesso para {(plan == 1 ? "Pro" : "Enterprise")}!";
            else
                TempData["ErrorMessage"] = "Não foi possível atualizar o plano.";

            return RedirectToAction(nameof(Index));
        }
    }
}
