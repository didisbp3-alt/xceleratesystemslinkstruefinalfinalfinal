using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using XcelerateLinks.Mvc.Models.ViewModels;
using APIPSI16.Services;

namespace XcelerateLinks.Mvc.Controllers
{
    public class HomeController : BaseController
    {
        private readonly IHttpClientFactory _httpFactory;

        public HomeController(IHttpClientFactory httpFactory, ISessionService sessionService)
            : base(sessionService)
        {
            _httpFactory = httpFactory;
        }

        public async Task<IActionResult> Index()
        {
            // Initialize with default
            ViewBag.Stats = new PlatformStats
            {
                UserCount = 0,
                CompanyCount = 0,
                OppCount = 0,
                ActiveConnections = 0
            };

            try
            {
                var client = _httpFactory.CreateClient("Api");
                var resp = await client.GetAsync("api/users/stats");

                if (resp.IsSuccessStatusCode)
                {
                    var jsonString = await resp.Content.ReadAsStringAsync();
                    Console.WriteLine($"Raw JSON from API: {jsonString}");

                    var options = new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var stats = System.Text.Json.JsonSerializer.Deserialize<PlatformStats>(jsonString, options);

                    if (stats != null)
                    {
                        Console.WriteLine($"Successfully deserialized: UserCount={stats.UserCount}, OppCount={stats.OppCount}, CompanyCount={stats.CompanyCount}, ActiveConnections={stats.ActiveConnections}");
                        ViewBag.Stats = stats;
                    }
                    else
                    {
                        Console.WriteLine("Deserialization returned null!");
                    }
                }
                else
                {
                    Console.WriteLine($"API error: {resp.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                Console.WriteLine($"Stack: {ex.StackTrace}");
            }

            return View();
        }

        private class PlatformStats
        {
            public int UserCount { get; set; }
            public int CompanyCount { get; set; }
            public int OppCount { get; set; }
            public int ActiveConnections { get; set; }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [Authorize]
        public async Task<IActionResult> AdminIndex()
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
            if (!string.IsNullOrWhiteSpace(roleClaim) && int.TryParse(roleClaim, out var roleFromClaim))
            {
                if (roleFromClaim == 0)
                    return View("AdminIndex");

                return Forbid();
            }

            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(idClaim))
            {
                return Challenge();
            }

            try
            {
                var client = _httpFactory.CreateClient("Api");

                var token = Request.Cookies["ApiAccessToken"];
                if (!string.IsNullOrWhiteSpace(token))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                var resp = await client.GetAsync($"api/users/{idClaim}");
                if (!resp.IsSuccessStatusCode)
                {
                    return Forbid();
                }

                var userDto = await resp.Content.ReadFromJsonAsync<UserDto?>();
                if (userDto == null)
                    return Forbid();

                if (userDto.Role == 0)
                    return View("AdminIndex");

                return Forbid();
            }
            catch
            {
                return Forbid();
            }
        }

        [Authorize]
        public async Task<IActionResult> Profit()
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
            if (roleClaim != "0")
                return Forbid();

            try
            {
                var client = _httpFactory.CreateClient("Api");
                var token = Request.Cookies["ApiAccessToken"];
                if (!string.IsNullOrWhiteSpace(token))
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var resp = await client.GetAsync("api/users/admin/revenue");
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync();
                    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var stats = System.Text.Json.JsonSerializer.Deserialize<RevenueStats>(json, options);
                    if (stats != null)
                        ViewBag.Revenue = stats;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Profit page error: {ex.Message}");
            }

            if (ViewBag.Revenue == null)
                ViewBag.Revenue = new RevenueStats();

            return View("Profit");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var vm = new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            };
            return View(vm);
        }

        private class UserDto
        {
            public int Role { get; set; }
        }

        public class RevenueStats
        {
            public int FreeCount { get; set; }
            public int ProCount { get; set; }
            public int EnterpriseCount { get; set; }
            public int TotalUsers { get; set; }
            public decimal ProPrice { get; set; } = 9m;
            public decimal EnterprisePrice { get; set; } = 29m;
            public decimal MonthlyRevenue { get; set; }
            public decimal YearlyRevenue { get; set; }
            public decimal TaxRate { get; set; } = 0.23m;
            public decimal MonthlyTax { get; set; }
            public decimal YearlyTax { get; set; }
            public decimal MonthlyProfit { get; set; }
            public decimal YearlyProfit { get; set; }
            public List<MonthlyActivityItem> MonthlyActivity { get; set; } = new();
        }

        public class MonthlyActivityItem
        {
            public string Label { get; set; } = "";
            public int ApplicationCount { get; set; }
            public decimal EstimatedRevenue { get; set; }
        }
    }
}