using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using APIPSI16.Models;
using APIPSI16.Services;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace XcelerateLinks.Mvc.Controllers
{
    public class CompaniesController : ApiControllerBase
    {
        private readonly ILogger<CompaniesController> _logger;
        private readonly IFileStorageService _fileStorage;

        public CompaniesController(IHttpClientFactory httpFactory, ILogger<CompaniesController> logger, ISessionService sessionService, IFileStorageService fileStorage)
            : base(httpFactory, sessionService)
        {
            _logger = logger;
            _fileStorage = fileStorage;
        }

        // Role-dispatched: admin → Index (table), user → UserIndex (company explorer)
        public async Task<IActionResult> Index(string? search = null)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();
            var resp = await client.GetAsync("api/companies");
            if (!resp.IsSuccessStatusCode)
            {
                ViewBag.Error = await SafeReadStringAsync(resp) ?? "Unable to load companies.";
                return View(IsAdmin() ? "Index" : "UserIndex", Array.Empty<Company>());
            }

            IEnumerable<Company> companies = await resp.Content.ReadFromJsonAsync<IEnumerable<Company>>()
                                              ?? Array.Empty<Company>();

            if (IsAdmin())
                return View(companies);

            // For employers: load their companies for the "Gerir" banner
            var isEmployer = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value == "2";
            if (isEmployer)
            {
                var myCompResp = await client.GetAsync("api/users/me/companies");
                if (myCompResp.IsSuccessStatusCode)
                {
                    var myComps = await myCompResp.Content
                        .ReadFromJsonAsync<IEnumerable<MyCompanyItem>>() ?? Array.Empty<MyCompanyItem>();
                    ViewBag.MyCompanies = myComps.ToList();
                }
            }

            // User: optionally filter by search
            if (!string.IsNullOrWhiteSpace(search))
                companies = companies.Where(c =>
                    (c.Name ?? "").Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (c.Industry ?? "").Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (c.Location ?? "").Contains(search, StringComparison.OrdinalIgnoreCase));

            ViewBag.Search = search;
            return View("UserIndex", companies);
        }

        public async Task<IActionResult> Details(int id)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();
            var resp = await client.GetAsync($"api/companies/{id}");
            if (!resp.IsSuccessStatusCode)
                return RedirectToAction(nameof(Index));

            var company = await resp.Content.ReadFromJsonAsync<Company>();
            if (company == null) return RedirectToAction(nameof(Index));

            // Load company's open opportunities to show on page
            var oppsResp = await client.GetAsync("api/opportunities");
            if (oppsResp.IsSuccessStatusCode)
            {
                var allOpps = await oppsResp.Content.ReadFromJsonAsync<IEnumerable<Opportunity>>();
                ViewBag.CompanyOpportunities = allOpps?.Where(o => o.CompanyId == id).ToList();
            }

            return View(company);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");
            return View(new Company());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Company model)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid) return View(model);

            var client = CreateAuthorizedClient();
            var resp = await client.PostAsJsonAsync("api/companies", model);
            if (!resp.IsSuccessStatusCode)
            {
                ModelState.AddModelError("", await SafeReadStringAsync(resp) ?? "Unable to create company.");
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
            var resp = await client.GetAsync($"api/companies/{id}");
            if (!resp.IsSuccessStatusCode)
                return RedirectToAction(nameof(Index));

            var company = await resp.Content.ReadFromJsonAsync<Company>();
            if (company == null) return RedirectToAction(nameof(Index));
            return View(company);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Company model)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            if (id != model.CompanyId) return RedirectToAction(nameof(Index));
            if (!ModelState.IsValid) return View(model);

            var client = CreateAuthorizedClient();
            var resp = await client.PutAsJsonAsync($"api/companies/{id}", model);
            if (!resp.IsSuccessStatusCode)
            {
                ModelState.AddModelError("", await SafeReadStringAsync(resp) ?? "Unable to update company.");
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
            var resp = await client.GetAsync($"api/companies/{id}");
            if (!resp.IsSuccessStatusCode)
                return RedirectToAction(nameof(Index));

            var company = await resp.Content.ReadFromJsonAsync<Company>();
            if (company == null) return RedirectToAction(nameof(Index));
            return View(company);
        }

        // Employer/Admin company management dashboard
        public async Task<IActionResult> Manage(int id)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();
            var compResp = await client.GetAsync($"api/companies/{id}");
            if (!compResp.IsSuccessStatusCode)
                return RedirectToAction(nameof(Index));

            var company = await compResp.Content.ReadFromJsonAsync<APIPSI16.Models.Company>();
            if (company == null) return RedirectToAction(nameof(Index));

            // Load opportunities for this company
            var oppsResp = await client.GetAsync("api/opportunities");
            IEnumerable<APIPSI16.Models.Opportunity> companyOpps = Array.Empty<APIPSI16.Models.Opportunity>();
            if (oppsResp.IsSuccessStatusCode)
            {
                var all = await oppsResp.Content.ReadFromJsonAsync<IEnumerable<APIPSI16.Models.Opportunity>>();
                companyOpps = all?.Where(o => o.CompanyId == id) ?? Array.Empty<APIPSI16.Models.Opportunity>();
            }

            ViewBag.Company = company;
            ViewBag.CompanyOpportunities = companyOpps.ToList();
            return View(company);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();
            var resp = await client.DeleteAsync($"api/companies/{id}");
            if (!resp.IsSuccessStatusCode)
                return RedirectToAction(nameof(Delete), new { id });

            return RedirectToAction(nameof(Index));
        }

        // POST: upload company logo via multipart form
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadLogo(int id, IFormFile? logoFile)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            if (logoFile != null && logoFile.Length > 0)
            {
                if (!_fileStorage.ValidateImageFile(logoFile, out var validationError))
                {
                    TempData["LogoError"] = validationError;
                    return RedirectToAction(nameof(Edit), new { id });
                }

                try
                {
                    // Save to MVC's wwwroot/uploads/companies so the image is served by the MVC app
                    var fileUrl = await _fileStorage.SaveFileAsync(logoFile, "companies");

                    // Tell the API to update the CompanyLogoUrl field
                    var client = CreateAuthorizedClient();
                    var company = await (await client.GetAsync($"api/companies/{id}")).Content.ReadFromJsonAsync<Company>();
                    if (company != null)
                    {
                        company.CompanyLogoUrl = fileUrl;
                        await client.PutAsJsonAsync($"api/companies/{id}", company);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to upload company logo for company {Id}", id);
                    TempData["LogoError"] = "Falha no upload. Tente novamente.";
                }
            }

            return RedirectToAction(nameof(Edit), new { id });
        }
    }

    // DTO matching the shape returned by GET api/users/me/companies
    public record MyCompanyItem(int CompanyId = 0, string? CompanyName = null, string? Title = null, int Role = 0);
}
