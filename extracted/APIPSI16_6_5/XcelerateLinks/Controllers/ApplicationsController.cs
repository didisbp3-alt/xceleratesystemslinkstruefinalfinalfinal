using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using APIPSI16.Models;
using APIPSI16.Services;
using XcelerateLinks.Models.ViewModels;

namespace XcelerateLinks.Mvc.Controllers
{
    public class ApplicationsController : ApiControllerBase
    {
        private readonly ILogger<ApplicationsController> _logger;

        public ApplicationsController(IHttpClientFactory httpFactory, ILogger<ApplicationsController> logger, ISessionService sessionService)
            : base(httpFactory, sessionService)
        {
            _logger = logger;
        }

        // Role-dispatched: admin → Index (all apps table), user → UserIndex (my jobs tracker)
        public async Task<IActionResult> Index()
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var uid = GetCurrentUserId();
            if (uid == null) return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();

            if (IsAdmin())
            {
                // Admin: load all applications
                var allResp = await client.GetAsync("api/jobapplications");
                if (!allResp.IsSuccessStatusCode)
                {
                    ViewBag.Error = await SafeReadStringAsync(allResp) ?? "Unable to load applications.";
                    return View(Array.Empty<JobApplication>());
                }
                var allApps = await allResp.Content.ReadFromJsonAsync<IEnumerable<JobApplication>>();
                return View(allApps ?? Array.Empty<JobApplication>());
            }

            // Regular user: load their own applications
            var resp = await client.GetAsync($"api/jobapplications/user/{uid}");
            if (!resp.IsSuccessStatusCode)
            {
                ViewBag.Error = await SafeReadStringAsync(resp) ?? "Unable to load applications.";
                return View("UserIndex", Array.Empty<JobApplication>());
            }

            var applications = await resp.Content.ReadFromJsonAsync<IEnumerable<JobApplication>>();
            return View("UserIndex", applications ?? Array.Empty<JobApplication>());
        }

        public async Task<IActionResult> Details(int id)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();
            var resp = await client.GetAsync($"api/jobapplications/{id}");
            if (!resp.IsSuccessStatusCode)
                return RedirectToAction(nameof(Index));

            var application = await resp.Content.ReadFromJsonAsync<JobApplication>();
            if (application == null) return RedirectToAction(nameof(Index));

            // Load job roles for tag display
            var jrResp = await client.GetAsync("api/users/lookups/jobroles");
            ViewBag.JobRoles = jrResp.IsSuccessStatusCode
                ? await jrResp.Content.ReadFromJsonAsync<IEnumerable<XcelerateLinks.Mvc.Controllers.UsersController.LookupItem>>() ?? Array.Empty<XcelerateLinks.Mvc.Controllers.UsersController.LookupItem>()
                : Array.Empty<XcelerateLinks.Mvc.Controllers.UsersController.LookupItem>();

            return View(application);
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? opportunityId = null)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            // Application must always be tied to a specific opportunity
            if (!opportunityId.HasValue)
                return RedirectToAction("Index", "Opportunities");

            var model = new JobApplicationCreateViewModel
            {
                Application = new JobApplicationData { OpportunityId = opportunityId.Value }
            };

            var client = CreateAuthorizedClient();
            var oppResp = await client.GetAsync($"api/opportunities/{opportunityId}");
            if (oppResp.IsSuccessStatusCode)
            {
                var opp = await oppResp.Content.ReadFromJsonAsync<Opportunity>();
                ViewBag.Opportunity = opp;
            }

            // Load job roles for tag display
            var jrResp = await client.GetAsync("api/users/lookups/jobroles");
            ViewBag.JobRoles = jrResp.IsSuccessStatusCode
                ? await jrResp.Content.ReadFromJsonAsync<IEnumerable<XcelerateLinks.Mvc.Controllers.UsersController.LookupItem>>() ?? Array.Empty<XcelerateLinks.Mvc.Controllers.UsersController.LookupItem>()
                : Array.Empty<XcelerateLinks.Mvc.Controllers.UsersController.LookupItem>();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(JobApplicationCreateViewModel model)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();

            if (!ModelState.IsValid)
            {
                if (model.Application.OpportunityId > 0)
                {
                    var oppR = await client.GetAsync($"api/opportunities/{model.Application.OpportunityId}");
                    if (oppR.IsSuccessStatusCode)
                        ViewBag.Opportunity = await oppR.Content.ReadFromJsonAsync<Opportunity>();
                }
                var jrR2 = await client.GetAsync("api/users/lookups/jobroles");
                ViewBag.JobRoles = jrR2.IsSuccessStatusCode
                    ? await jrR2.Content.ReadFromJsonAsync<IEnumerable<XcelerateLinks.Mvc.Controllers.UsersController.LookupItem>>() ?? Array.Empty<XcelerateLinks.Mvc.Controllers.UsersController.LookupItem>()
                    : Array.Empty<XcelerateLinks.Mvc.Controllers.UsersController.LookupItem>();
                return View(model);
            }

            var payload = new APIPSI16.Models.DTOs.ApplyDto {
                OpportunityId = model.Application.OpportunityId,
                Name = model.Application.Name,
                CoverLetter = model.Application.CoverLetter,
                PhoneNumber = model.Application.PhoneNumber,
                LinkedInUrl = model.Application.LinkedInUrl,
                PortfolioUrl = model.Application.PortfolioUrl,
                YearsOfExperience = model.Application.YearsOfExperience,
                OpenToRemote = model.Application.OpenToRemote,
                SelectedJobRoleIds = model.Application.SelectedJobRoleIds
            };

            var resp = await client.PostAsJsonAsync("api/jobapplications/apply", payload);
            if (!resp.IsSuccessStatusCode)
            {
                ModelState.AddModelError("", await SafeReadStringAsync(resp) ?? "Unable to submit application.");
                if (model.Application.OpportunityId > 0)
                {
                    var oppR = await client.GetAsync($"api/opportunities/{model.Application.OpportunityId}");
                    if (oppR.IsSuccessStatusCode)
                        ViewBag.Opportunity = await oppR.Content.ReadFromJsonAsync<Opportunity>();
                }
                var jrR = await client.GetAsync("api/users/lookups/jobroles");
                ViewBag.JobRoles = jrR.IsSuccessStatusCode
                    ? await jrR.Content.ReadFromJsonAsync<IEnumerable<XcelerateLinks.Mvc.Controllers.UsersController.LookupItem>>() ?? Array.Empty<XcelerateLinks.Mvc.Controllers.UsersController.LookupItem>()
                    : Array.Empty<XcelerateLinks.Mvc.Controllers.UsersController.LookupItem>();
                return View(model);
            }

            TempData["SuccessMessage"] = "A sua candidatura foi submetida com sucesso!";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();
            var resp = await client.GetAsync($"api/jobapplications/{id}");
            if (!resp.IsSuccessStatusCode)
                return RedirectToAction(nameof(Index));

            var application = await resp.Content.ReadFromJsonAsync<JobApplication>();
            if (application == null) return RedirectToAction(nameof(Index));
            return View(application);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();
            var resp = await client.DeleteAsync($"api/jobapplications/{id}");
            if (!resp.IsSuccessStatusCode)
                return RedirectToAction(nameof(Delete), new { id });

            return RedirectToAction(nameof(Index));
        }

        // Pipeline view for employer/admin
        public async Task<IActionResult> Pipeline(int companyId, int? opportunityId = null)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            if (companyId <= 0)
                return RedirectToAction(nameof(Index));

            var client = CreateAuthorizedClient();
            ViewBag.CompanyId = companyId;
            ViewBag.OpportunityId = opportunityId;

            // Build the company-specific endpoint URL (both admin and employer can access this)
            var url = $"api/jobapplications/for-company/{companyId}";
            if (opportunityId.HasValue && opportunityId.Value > 0)
                url += $"?opportunityId={opportunityId.Value}";

            IEnumerable<JobApplication> apps = Array.Empty<JobApplication>();
            var resp = await client.GetAsync(url);
            if (resp.IsSuccessStatusCode)
            {
                apps = await resp.Content.ReadFromJsonAsync<IEnumerable<JobApplication>>()
                       ?? Array.Empty<JobApplication>();
            }
            else
            {
                _logger.LogWarning("Pipeline fetch failed: {Status} — {Body}",
                    resp.StatusCode, await SafeReadStringAsync(resp));
                ViewBag.Error = "Não foi possível carregar as candidaturas.";
            }

            ViewBag.Applications = apps.ToList();
            return View("Pipeline");
        }

        // POST: update application status (employer only)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, byte newStatus, int? returnCompanyId = null)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();
            var payload = new { NewStatus = newStatus };
            var resp = await client.PostAsJsonAsync($"api/jobapplications/{id}/status", payload);

            if (returnCompanyId.HasValue)
                return RedirectToAction("Pipeline", "Applications", new { companyId = returnCompanyId.Value });

            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task<IEnumerable<Opportunity>> LoadOpportunitiesAsync()
        {
            var client = CreateAuthorizedClient();
            var resp = await client.GetAsync("api/opportunities");
            if (!resp.IsSuccessStatusCode)
                return Array.Empty<Opportunity>();
            return await resp.Content.ReadFromJsonAsync<IEnumerable<Opportunity>>() ?? Array.Empty<Opportunity>();
        }

        // POST: applicant responds to interview or offer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplicantRespond(int id, byte response)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();
            var payload = new { Response = response };
            var resp = await client.PostAsJsonAsync($"api/jobapplications/{id}/applicant-respond", payload);

            if (!resp.IsSuccessStatusCode)
            {
                TempData["ErrorMessage"] = await SafeReadStringAsync(resp) ?? "Não foi possível registar a resposta.";
            }
            else
            {
                TempData["SuccessMessage"] = response == 1 ? "Resposta aceite registada com sucesso." : "Resposta recusada registada com sucesso.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: employer takes action on application
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EmployerAction(int id, string action, string? notes = null, int? returnCompanyId = null)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();
            var payload = new { Action = action, Notes = notes };
            var resp = await client.PostAsJsonAsync($"api/jobapplications/{id}/employer-action", payload);

            if (!resp.IsSuccessStatusCode)
            {
                TempData["ErrorMessage"] = await SafeReadStringAsync(resp) ?? "Não foi possível executar a ação.";
            }

            if (returnCompanyId.HasValue)
                return RedirectToAction("Pipeline", "Applications", new { companyId = returnCompanyId.Value });

            return RedirectToAction(nameof(Details), new { id });
        }

        public class JobApplicationCreateViewModel
        {
            public JobApplicationData Application { get; set; } = new JobApplicationData();
            public IEnumerable<Opportunity> Opportunities { get; set; } = Array.Empty<Opportunity>();
        }

        public class JobApplicationData
        {
            public int OpportunityId { get; set; }
            public string? Name { get; set; }
            public string? CoverLetter { get; set; }
            public string? PhoneNumber { get; set; }
            public string? LinkedInUrl { get; set; }
            public string? PortfolioUrl { get; set; }
            public int? YearsOfExperience { get; set; }
            public bool? OpenToRemote { get; set; }
            // Comma-separated job role IDs chosen by the applicant
            public string? SelectedJobRoleIds { get; set; }
        }
    }
}
