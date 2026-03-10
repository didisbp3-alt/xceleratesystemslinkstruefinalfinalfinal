using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using APIPSI16.Models.DTOs;
using APIPSI16.Services;
using XcelerateLinks.Models.ViewModels;

namespace XcelerateLinks.Mvc.Controllers
{
    public class UsersController : ApiControllerBase
    {
        private readonly ILogger<UsersController> _logger;

        public UsersController(IHttpClientFactory httpFactory, ILogger<UsersController> logger, ISessionService sessionService)
            : base(httpFactory, sessionService)
        {
            _logger = logger;
        }

        // Role-dispatched: admin → Index (table with filters), user → UserIndex (network grid)
        public async Task<IActionResult> Index(int? jobPreference = null, int? nationality = null, string? search = null)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();

            if (IsAdmin())
            {
                var model = new UserFilterViewModel
                {
                    JobPreference = jobPreference,
                    Nationality = nationality
                };

                var query = new List<string>();
                if (jobPreference.HasValue) query.Add($"jobPreference={jobPreference.Value}");
                if (nationality.HasValue) query.Add($"nationality={nationality.Value}");
                var url = query.Count == 0 ? "api/users" : $"api/users?{string.Join("&", query)}";

                var resp = await client.GetAsync(url);
                if (!resp.IsSuccessStatusCode)
                {
                    model.ErrorMessage = await SafeReadStringAsync(resp) ?? "Unable to load users.";
                    model.Users = Array.Empty<UserDTO>();
                    return View(model);
                }

                model.Users = await resp.Content.ReadFromJsonAsync<IEnumerable<UserDTO>>() ?? Array.Empty<UserDTO>();

                // Load filter dropdowns
                var natResp = await client.GetAsync("api/users/lookups/nationalities");
                ViewBag.Nationalities = natResp.IsSuccessStatusCode
                    ? await natResp.Content.ReadFromJsonAsync<IEnumerable<LookupItem>>() ?? Array.Empty<LookupItem>()
                    : Array.Empty<LookupItem>();

                var jrResp = await client.GetAsync("api/users/lookups/jobroles");
                ViewBag.JobRoles = jrResp.IsSuccessStatusCode
                    ? await jrResp.Content.ReadFromJsonAsync<IEnumerable<LookupItem>>() ?? Array.Empty<LookupItem>()
                    : Array.Empty<LookupItem>();

                return View(model);
            }

            // Regular user → network/people discovery (uses /api/users/network – no admin required)
            var networkUrl = string.IsNullOrWhiteSpace(search)
                ? "api/users/network"
                : $"api/users/network?search={Uri.EscapeDataString(search)}";

            var usersResp = await client.GetAsync(networkUrl);
            IEnumerable<UserDTO> users = Array.Empty<UserDTO>();
            if (usersResp.IsSuccessStatusCode)
                users = await usersResp.Content.ReadFromJsonAsync<IEnumerable<UserDTO>>() ?? Array.Empty<UserDTO>();
            else
                ViewBag.Error = "Unable to load users.";

            ViewBag.Search = search;
            return View("UserIndex", users);
        }

        // Keep Network as alias (backwards compat for nav links)
        public async Task<IActionResult> Network(string? search = null)
            => await Index(search: search);

        // USER DETAILS / PUBLIC PROFILE
        public async Task<IActionResult> Details(int id, string? returnUrl = null)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();
            var resp = await client.GetAsync($"api/users/{id}/profile");
            if (!resp.IsSuccessStatusCode)
            {
                var basicResp = await client.GetAsync($"api/users/{id}");
                if (!basicResp.IsSuccessStatusCode)
                    return RedirectToAction(nameof(Index));

                var basicUser = await basicResp.Content.ReadFromJsonAsync<UserDTO>();
                if (basicUser == null) return RedirectToAction(nameof(Index));
                if (!string.IsNullOrEmpty(returnUrl)) ViewBag.ReturnUrl = returnUrl;
                return View(new UserProfileDTO
                {
                    UserId = basicUser.UserId,
                    Name = basicUser.Name,
                    Email = basicUser.Email,
                    PhoneNumber = basicUser.PhoneNumber,
                    ProfileBio = basicUser.ProfileBio,
                    ProfilePictureUrl = basicUser.ProfilePictureUrl,
                    BannerUrl = basicUser.BannerUrl
                });
            }

            var profile = await resp.Content.ReadFromJsonAsync<UserProfileDTO>();
            if (profile == null) return RedirectToAction(nameof(Index));
            if (!string.IsNullOrEmpty(returnUrl)) ViewBag.ReturnUrl = returnUrl;
            return View(profile);
        }

        // CURRENT PROFILE REDIRECT
        public async Task<IActionResult> Profile()
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return RedirectToAction(nameof(Index));

            return RedirectToAction(nameof(Details), new { id = userId.Value });
        }

        // EDIT USER GET
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();
            var resp = await client.GetAsync($"api/users/{id}");
            if (!resp.IsSuccessStatusCode)
                return RedirectToAction(nameof(Index));

            var user = await resp.Content.ReadFromJsonAsync<UserDTO>();
            if (user == null) return RedirectToAction(nameof(Index));

            // Load lookup lists for dropdowns
            var natResp = await client.GetAsync("api/users/lookups/nationalities");
            if (natResp.IsSuccessStatusCode)
                ViewBag.Nationalities = await natResp.Content.ReadFromJsonAsync<IEnumerable<LookupItem>>() ?? Array.Empty<LookupItem>();
            else
                ViewBag.Nationalities = Array.Empty<LookupItem>();

            var jrResp = await client.GetAsync("api/users/lookups/jobroles");
            if (jrResp.IsSuccessStatusCode)
                ViewBag.JobRoles = await jrResp.Content.ReadFromJsonAsync<IEnumerable<LookupItem>>() ?? Array.Empty<LookupItem>();
            else
                ViewBag.JobRoles = Array.Empty<LookupItem>();

            var countriesResp = await client.GetAsync("api/users/lookups/countries");
            if (countriesResp.IsSuccessStatusCode)
                ViewBag.Countries = await countriesResp.Content.ReadFromJsonAsync<IEnumerable<LookupItem>>() ?? Array.Empty<LookupItem>();
            else
                ViewBag.Countries = Array.Empty<LookupItem>();

            var locationsResp = await client.GetAsync("api/users/lookups/locations");
            if (locationsResp.IsSuccessStatusCode)
                ViewBag.Locations = await locationsResp.Content.ReadFromJsonAsync<IEnumerable<LookupItem>>() ?? Array.Empty<LookupItem>();
            else
                ViewBag.Locations = Array.Empty<LookupItem>();

            var profileResp = await client.GetAsync($"api/users/{id}/profile");
            if (profileResp.IsSuccessStatusCode)
            {
                var profile = await profileResp.Content.ReadFromJsonAsync<APIPSI16.Models.DTOs.UserProfileDTO>();
                ViewBag.SelectedJobRoleIds = profile?.JobRolePreferences?.Select(p => p.JobRoleId).ToList() ?? new List<int>();
            }
            else
            {
                ViewBag.SelectedJobRoleIds = new List<int>();
            }

            var companiesResp = await client.GetAsync("api/companies");
            if (companiesResp.IsSuccessStatusCode)
                ViewBag.Companies = await companiesResp.Content.ReadFromJsonAsync<IEnumerable<CompanyInfo>>() ?? Array.Empty<CompanyInfo>();
            else
                ViewBag.Companies = Array.Empty<CompanyInfo>();

            return View(user);
        }

        public class LookupItem
        {
            public int? NationalityId { get; set; }
            public int? JobRoleId { get; set; }
            public int? LocationId { get; set; }
            public int? CountryId { get; set; }
            public string? Name { get; set; }
            public string? Region { get; set; }
            public string? Code { get; set; }
            public string? CountryName { get; set; }
            public string? CountryCode { get; set; }
            public int Id => NationalityId ?? JobRoleId ?? LocationId ?? CountryId ?? 0;
        }

        public class CompanyInfo
        {
            public int CompanyId { get; set; }
            public string? Name { get; set; }
        }

        // EDIT USER POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, UserDTO model, [FromForm] List<int>? SelectedJobRoleIds = null)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            if (id != model.UserId) return RedirectToAction(nameof(Index));
            if (!ModelState.IsValid) return View(model);

            var client = CreateAuthorizedClient();
            var resp = await client.PutAsJsonAsync($"api/users/{id}", model);
            if (!resp.IsSuccessStatusCode)
            {
                ModelState.AddModelError("", await SafeReadStringAsync(resp) ?? "Unable to update user.");
                return View(model);
            }

            var prefIds = SelectedJobRoleIds ?? new List<int>();
            await client.PutAsJsonAsync($"api/users/{id}/job-preferences", prefIds);

            return RedirectToAction(nameof(Details), new { id });
        }
        // GET: show "become employer" form
        [HttpGet]
        public async Task<IActionResult> RequestEmployer()
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();
            var compResp = await client.GetAsync("api/companies");
            if (compResp.IsSuccessStatusCode)
            {
                var companies = await compResp.Content.ReadFromJsonAsync<IEnumerable<CompanyInfo>>()
                    ?? Array.Empty<CompanyInfo>();
                ViewBag.Companies = companies;
            }
            else
            {
                ViewBag.Companies = Array.Empty<CompanyInfo>();
            }
            return View();
        }

        // POST: submit employer request with optional document
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestEmployer(IFormFile? document, int? companyId = null, string? note = null)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();
            HttpResponseMessage resp;

            using var form = new MultipartFormDataContent();
            if (document != null && document.Length > 0)
            {
                var stream = document.OpenReadStream();
                form.Add(new StreamContent(stream), "document", document.FileName);
            }
            if (companyId.HasValue)
                form.Add(new StringContent(companyId.Value.ToString()), "companyId");
            if (!string.IsNullOrWhiteSpace(note))
                form.Add(new StringContent(note), "note");
            resp = await client.PostAsync("api/users/me/request-employer", form);

            if (!resp.IsSuccessStatusCode)
            {
                ModelState.AddModelError("", await SafeReadStringAsync(resp) ?? "Unable to submit request.");
                return View();
            }

            TempData["SuccessMessage"] = "Pedido submetido! O administrador irá analisar o teu pedido em breve.";
            return RedirectToAction("Profile");
        }

        // POST: admin approves a user as employer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveEmployer(int id)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();
            var resp = await client.PostAsync($"api/users/{id}/approve-employer", null);

            TempData["SuccessMessage"] = resp.IsSuccessStatusCode
                ? "Utilizador aprovado como empregador."
                : "Não foi possível aprovar o utilizador.";

            // If came from the requests page, go back there
            var referer = Request.Headers["Referer"].ToString();
            if (referer.Contains("EmployerRequests"))
                return RedirectToAction(nameof(EmployerRequests));

            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: admin rejects employer request
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectEmployer(int id)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();
            await client.PostAsync($"api/users/{id}/reject-employer", null);

            TempData["SuccessMessage"] = "Pedido rejeitado.";

            // If came from the requests page, go back there
            var referer = Request.Headers["Referer"].ToString();
            if (referer.Contains("EmployerRequests"))
                return RedirectToAction(nameof(EmployerRequests));

            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: admin page listing all pending employer requests
        [HttpGet]
        public async Task<IActionResult> EmployerRequests()
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            if (!IsAdmin())
                return RedirectToAction("Index", "Home");

            var client = CreateAuthorizedClient();
            var resp = await client.GetAsync("api/users/pending-employers");
            IEnumerable<UserDTO> pending = Array.Empty<UserDTO>();
            if (resp.IsSuccessStatusCode)
                pending = await resp.Content.ReadFromJsonAsync<IEnumerable<UserDTO>>() ?? Array.Empty<UserDTO>();

            return View(pending);
        }

        // DELETE (MVC): show confirmation page (GET) + perform delete (POST)
        // Requires an API endpoint: DELETE api/users/{id} (admin only in your API)

        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            // Optional: only allow admins to delete (matches API [Authorize(Roles="0")])
            if (!IsAdmin())
                return RedirectToAction(nameof(Index));

            var client = CreateAuthorizedClient();

            // Load user to show confirmation info
            var resp = await client.GetAsync($"api/users/{id}");
            if (!resp.IsSuccessStatusCode)
                return RedirectToAction(nameof(Index));

            var user = await resp.Content.ReadFromJsonAsync<UserDTO>();
            if (user == null)
                return RedirectToAction(nameof(Index));

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            if (!IsAdmin())
                return RedirectToAction(nameof(Index));

            var client = CreateAuthorizedClient();

            var resp = await client.DeleteAsync($"api/users/{id}");
            if (!resp.IsSuccessStatusCode)
            {
                TempData["ErrorMessage"] = await SafeReadStringAsync(resp) ?? "Unable to delete user.";
                return RedirectToAction(nameof(Delete), new { id });
            }

            TempData["SuccessMessage"] = "User deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResignEmployer()
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();
            var resp = await client.PostAsync("api/users/me/resign-employer", null);

            TempData[resp.IsSuccessStatusCode ? "SuccessMessage" : "ErrorMessage"] = resp.IsSuccessStatusCode
                ? "Estatuto de empregador removido com sucesso."
                : await SafeReadStringAsync(resp) ?? "Não foi possível remover o estatuto.";

            var userId = GetCurrentUserId();
            return userId.HasValue
                ? RedirectToAction(nameof(Details), new { id = userId.Value })
                : RedirectToAction(nameof(Index));
        }
    }
}
