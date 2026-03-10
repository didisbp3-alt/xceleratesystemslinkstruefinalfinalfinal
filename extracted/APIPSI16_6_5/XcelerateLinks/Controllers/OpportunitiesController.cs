using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using APIPSI16.Models;
using APIPSI16.Services;

namespace XcelerateLinks.Mvc.Controllers
{
    public class OpportunitiesController : ApiControllerBase
    {
        private readonly ILogger<OpportunitiesController> _logger;

        public OpportunitiesController(IHttpClientFactory httpFactory, ILogger<OpportunitiesController> logger, ISessionService sessionService)
            : base(httpFactory, sessionService)
        {
            _logger = logger;
        }

        // Role-dispatched: admin → Index (table), user/employer → UserIndex (job search)
        public async Task<IActionResult> Index(string? q = null, int? locationId = null, byte? employmentType = null, byte? remoteOption = null, bool recommended = false)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();

            if (IsAdmin())
            {
                var adminResp = await client.GetAsync("api/opportunities");
                if (!adminResp.IsSuccessStatusCode)
                {
                    ViewBag.Error = await SafeReadStringAsync(adminResp) ?? "Unable to load opportunities.";
                    return View(Array.Empty<Opportunity>());
                }
                var adminOpps = await adminResp.Content.ReadFromJsonAsync<IEnumerable<Opportunity>>() ?? Array.Empty<Opportunity>();
                return View(adminOpps);
            }

            // User / employer: load with match scores
            var resp = await client.GetAsync("api/opportunities/with-match");
            List<OpportunityWithMatch> opportunitiesWithMatch;

            if (resp.IsSuccessStatusCode)
            {
                opportunitiesWithMatch = await resp.Content.ReadFromJsonAsync<List<OpportunityWithMatch>>()
                                         ?? new List<OpportunityWithMatch>();
            }
            else
            {
                // Fallback to regular endpoint
                var fallbackResp = await client.GetAsync("api/opportunities");
                var plain = fallbackResp.IsSuccessStatusCode
                    ? await fallbackResp.Content.ReadFromJsonAsync<IEnumerable<OpportunityPlain>>() ?? Array.Empty<OpportunityPlain>()
                    : Array.Empty<OpportunityPlain>();
                opportunitiesWithMatch = plain.Select(o => new OpportunityWithMatch
                {
                    Id = o.Id, Title = o.Title, Location = o.Location,
                    LocationId = o.LocationId, LocationName = o.LocationName,
                    EmploymentType = o.EmploymentType, SeniorityLevel = o.SeniorityLevel,
                    RemoteOption = o.RemoteOption, CompanyId = o.CompanyId, CompanyName = o.CompanyName,
                    MatchScore = 0
                }).ToList();
            }

            // Apply filters
            if (!string.IsNullOrWhiteSpace(q))
                opportunitiesWithMatch = opportunitiesWithMatch.Where(o =>
                    (o.Title ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    (o.LocationName ?? o.Location ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

            if (locationId.HasValue)
                opportunitiesWithMatch = opportunitiesWithMatch
                    .Where(o => o.LocationId == locationId.Value).ToList();

            if (employmentType.HasValue)
                opportunitiesWithMatch = opportunitiesWithMatch.Where(o => o.EmploymentType == employmentType.Value).ToList();

            if (remoteOption.HasValue)
                opportunitiesWithMatch = opportunitiesWithMatch.Where(o => o.RemoteOption == remoteOption.Value).ToList();

            // Load locations for combobox filter
            var locsResp = await client.GetAsync("api/users/lookups/locations");
            if (locsResp.IsSuccessStatusCode)
                ViewBag.Locations = await locsResp.Content.ReadFromJsonAsync<IEnumerable<XcelerateLinks.Mvc.Controllers.UsersController.LookupItem>>() ?? Array.Empty<XcelerateLinks.Mvc.Controllers.UsersController.LookupItem>();
            else
                ViewBag.Locations = Array.Empty<XcelerateLinks.Mvc.Controllers.UsersController.LookupItem>();

            ViewBag.Q = q;
            ViewBag.LocationId = locationId;
            ViewBag.EmploymentType = employmentType;
            ViewBag.RemoteOption = remoteOption;
            ViewBag.Recommended = recommended;
            ViewBag.OpportunitiesWithMatch = opportunitiesWithMatch;

            // Also map to Opportunity for backwards-compat with existing model binding in view
            var opportunities = opportunitiesWithMatch.Select(o => new Opportunity
            {
                Id = o.Id, Title = o.Title, Location = o.Location,
                LocationId = o.LocationId,
                EmploymentType = o.EmploymentType, SeniorityLevel = o.SeniorityLevel,
                RemoteOption = o.RemoteOption, CompanyId = o.CompanyId
            });

            return View("UserIndex", opportunities);
        }

        public async Task<IActionResult> Browse(string? q = null, int? locationId = null, byte? employmentType = null, byte? remoteOption = null)
            => await Index(q, locationId, employmentType, remoteOption);

        public async Task<IActionResult> Details(int id)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();
            var resp = await client.GetAsync($"api/opportunities/{id}");
            if (!resp.IsSuccessStatusCode)
                return RedirectToAction(nameof(Index));

            var opportunity = await resp.Content.ReadFromJsonAsync<Opportunity>();
            if (opportunity == null) return RedirectToAction(nameof(Index));

            // Load job roles for tag display
            var jrResp = await client.GetAsync("api/users/lookups/jobroles");
            if (jrResp.IsSuccessStatusCode)
                ViewBag.JobRoles = await jrResp.Content.ReadFromJsonAsync<IEnumerable<XcelerateLinks.Mvc.Controllers.UsersController.LookupItem>>() ?? Array.Empty<XcelerateLinks.Mvc.Controllers.UsersController.LookupItem>();
            else
                ViewBag.JobRoles = Array.Empty<XcelerateLinks.Mvc.Controllers.UsersController.LookupItem>();

            return View(opportunity);
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? companyId = null)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var model = new Opportunity();
            var userId = GetCurrentUserId();
            if (userId.HasValue)
                model.CreatorId = userId.Value;

            // Pre-fill company if navigating from company Manage page
            if (companyId.HasValue)
                model.CompanyId = companyId.Value;

            await LoadDropdownsAsync();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Opportunity model)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
            {
                await LoadDropdownsAsync();
                return View(model);
            }
            model.CreatorId ??= GetCurrentUserId();

            var client = CreateAuthorizedClient();
            var resp = await client.PostAsJsonAsync("api/opportunities", model);
            if (!resp.IsSuccessStatusCode)
            {
                ModelState.AddModelError("", await SafeReadStringAsync(resp) ?? "Unable to create opportunity.");
                await LoadDropdownsAsync();
                return View(model);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();
            var resp = await client.GetAsync($"api/opportunities/{id}");
            if (!resp.IsSuccessStatusCode)
                return RedirectToAction(nameof(Index));

            var opportunity = await resp.Content.ReadFromJsonAsync<Opportunity>();
            if (opportunity == null) return RedirectToAction(nameof(Index));

            await LoadDropdownsAsync();
            return View(opportunity);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Opportunity model)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            if (id != model.Id) return RedirectToAction(nameof(Index));
            if (!ModelState.IsValid)
            {
                await LoadDropdownsAsync();
                return View(model);
            }

            var client = CreateAuthorizedClient();
            var resp = await client.PutAsJsonAsync($"api/opportunities/{id}", model);
            if (!resp.IsSuccessStatusCode)
            {
                ModelState.AddModelError("", await SafeReadStringAsync(resp) ?? "Unable to update opportunity.");
                await LoadDropdownsAsync();
                return View(model);
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();
            var resp = await client.GetAsync($"api/opportunities/{id}");
            if (!resp.IsSuccessStatusCode)
                return RedirectToAction(nameof(Index));

            var opportunity = await resp.Content.ReadFromJsonAsync<Opportunity>();
            if (opportunity == null) return RedirectToAction(nameof(Index));
            return View(opportunity);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();
            var resp = await client.DeleteAsync($"api/opportunities/{id}");
            if (!resp.IsSuccessStatusCode)
                return RedirectToAction(nameof(Delete), new { id });

            return RedirectToAction(nameof(Index));
        }

        // Helper: load companies + users for dropdowns; for employers, restrict to their companies
        private async Task LoadDropdownsAsync()
        {
            var client = CreateAuthorizedClient();
            var isEmployer = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value == "2";

            // Companies
            if (isEmployer)
            {
                var myCompResp = await client.GetAsync("api/users/me/companies");
                if (myCompResp.IsSuccessStatusCode)
                {
                    var list = await myCompResp.Content.ReadFromJsonAsync<IEnumerable<CompanyDropItem>>();
                    ViewBag.Companies = list ?? Array.Empty<CompanyDropItem>();
                    ViewBag.IsEmployer = true;
                }
                else
                {
                    ViewBag.Companies = Array.Empty<CompanyDropItem>();
                    ViewBag.IsEmployer = true;
                }
            }
            else
            {
                var compResp = await client.GetAsync("api/companies");
                if (compResp.IsSuccessStatusCode)
                {
                    var companies = await compResp.Content.ReadFromJsonAsync<IEnumerable<Company>>();
                    ViewBag.Companies = companies?.Select(c => new CompanyDropItem { CompanyId = c.CompanyId, CompanyName = c.Name })
                                         ?? Array.Empty<CompanyDropItem>();
                }
                else
                {
                    ViewBag.Companies = Array.Empty<CompanyDropItem>();
                }
            }

            // Users (for CreatorId dropdown – admin only)
            if (IsAdmin())
            {
                var usersResp = await client.GetAsync("api/users");
                if (usersResp.IsSuccessStatusCode)
                {
                    var users = await usersResp.Content.ReadFromJsonAsync<IEnumerable<APIPSI16.Models.DTOs.UserDTO>>();
                    ViewBag.Users = users ?? Array.Empty<APIPSI16.Models.DTOs.UserDTO>();
                }
                else
                {
                    ViewBag.Users = Array.Empty<APIPSI16.Models.DTOs.UserDTO>();
                }
            }

            // Job roles
            var jobRolesResp = await client.GetAsync("api/users/lookups/jobroles");
            if (jobRolesResp.IsSuccessStatusCode)
            {
                var jr = await jobRolesResp.Content.ReadFromJsonAsync<IEnumerable<XcelerateLinks.Mvc.Controllers.UsersController.LookupItem>>();
                ViewBag.JobRoles = jr ?? Array.Empty<XcelerateLinks.Mvc.Controllers.UsersController.LookupItem>();
            }
            else
            {
                ViewBag.JobRoles = Array.Empty<XcelerateLinks.Mvc.Controllers.UsersController.LookupItem>();
            }

            // Locations for combobox
            var locsResp = await client.GetAsync("api/users/lookups/locations");
            if (locsResp.IsSuccessStatusCode)
            {
                var locs = await locsResp.Content.ReadFromJsonAsync<IEnumerable<XcelerateLinks.Mvc.Controllers.UsersController.LookupItem>>();
                ViewBag.Locations = locs ?? Array.Empty<XcelerateLinks.Mvc.Controllers.UsersController.LookupItem>();
            }
            else
            {
                ViewBag.Locations = Array.Empty<XcelerateLinks.Mvc.Controllers.UsersController.LookupItem>();
            }
        }

        public record CompanyDropItem(int CompanyId = 0, string? CompanyName = null);
    }

    public class OpportunityWithMatch
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Location { get; set; }
        public int? LocationId { get; set; }
        public string? LocationName { get; set; }
        public string? CountryName { get; set; }
        public byte? EmploymentType { get; set; }
        public byte? SeniorityLevel { get; set; }
        public byte? RemoteOption { get; set; }
        public byte? OpportunityType { get; set; }
        public byte? ApplicationScope { get; set; }
        public int? CompanyId { get; set; }
        public string? CompanyName { get; set; }
        public string? RequiredJobRoleIds { get; set; }
        public int MatchScore { get; set; }
    }

    public class OpportunityPlain
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Location { get; set; }
        public int? LocationId { get; set; }
        public string? LocationName { get; set; }
        public byte? EmploymentType { get; set; }
        public byte? SeniorityLevel { get; set; }
        public byte? RemoteOption { get; set; }
        public int? CompanyId { get; set; }
        public string? CompanyName { get; set; }
    }
}
