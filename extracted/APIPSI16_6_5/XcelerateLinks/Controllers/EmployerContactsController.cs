using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using APIPSI16.Services;

namespace XcelerateLinks.Mvc.Controllers
{
    /// <summary>
    /// Employer previous-contact management: list, prioritise, and discard contacts.
    /// Accessible to employers (role=2) and admins (role=0).
    /// </summary>
    public class EmployerContactsController : ApiControllerBase
    {
        public EmployerContactsController(IHttpClientFactory httpFactory, ISessionService sessionService)
            : base(httpFactory, sessionService) { }

        // GET: /EmployerContacts?companyId=X[&opportunityId=Y]
        // Lists active (non-discarded) contacts for the given company, optionally showing matches for an opportunity.
        public async Task<IActionResult> Index(int companyId, int? opportunityId = null)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var role = GetCurrentUserRole();
            if (role != "0" && role != "2")
                return Forbid();

            var client = CreateAuthorizedClient();

            // Load contacts
            var contactsResp = await client.GetAsync($"api/opportunities/employer-contacts?companyId={companyId}");
            List<ContactRow> contacts;
            if (contactsResp.IsSuccessStatusCode)
                contacts = await contactsResp.Content.ReadFromJsonAsync<List<ContactRow>>() ?? new();
            else
                contacts = new();

            // Optionally load employer matches for the given opportunity
            List<MatchRow> matches = new();
            if (opportunityId.HasValue)
            {
                var matchResp = await client.GetAsync($"api/opportunities/{opportunityId}/employer-matches");
                if (matchResp.IsSuccessStatusCode)
                    matches = await matchResp.Content.ReadFromJsonAsync<List<MatchRow>>() ?? new();
            }

            ViewBag.CompanyId = companyId;
            ViewBag.OpportunityId = opportunityId;
            ViewBag.Matches = matches;
            ViewBag.PageTitle = opportunityId.HasValue ? "Matches Anteriores" : "Contactos Anteriores";

            return View(contacts);
        }

        // GET: /EmployerContacts/Trash?companyId=X
        public async Task<IActionResult> Trash(int companyId)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var role = GetCurrentUserRole();
            if (role != "0" && role != "2")
                return Forbid();

            var client = CreateAuthorizedClient();
            var resp = await client.GetAsync($"api/opportunities/employer-contacts?companyId={companyId}&discarded=true");
            List<ContactRow> contacts;
            if (resp.IsSuccessStatusCode)
                contacts = await resp.Content.ReadFromJsonAsync<List<ContactRow>>() ?? new();
            else
                contacts = new();

            ViewBag.CompanyId = companyId;
            return View(contacts);
        }

        // POST: /EmployerContacts/SetPriority
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetPriority(int historyId, int? priorityId, int companyId, int? opportunityId)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();
            await client.PutAsJsonAsync($"api/opportunities/employer-contacts/{historyId}/priority",
                new { PriorityId = priorityId });

            return RedirectToAction(nameof(Index), new { companyId, opportunityId });
        }

        // POST: /EmployerContacts/Discard
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Discard(int historyId, int companyId, int? opportunityId)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();
            await client.PutAsJsonAsync($"api/opportunities/employer-contacts/{historyId}/discard",
                new { Discard = true });

            return RedirectToAction(nameof(Index), new { companyId, opportunityId });
        }

        // POST: /EmployerContacts/Restore
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int historyId, int companyId)
        {
            if (!await ValidateSessionAsync())
                return RedirectToAction("Login", "Account");

            var client = CreateAuthorizedClient();
            await client.PutAsJsonAsync($"api/opportunities/employer-contacts/{historyId}/discard",
                new { Discard = false });

            return RedirectToAction(nameof(Trash), new { companyId });
        }

        private string? GetCurrentUserRole()
        {
            return User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        }

        public class ContactRow
        {
            public int EmployerCandidateHistoryId { get; set; }
            public int UserId { get; set; }
            public string? UserName { get; set; }
            public string? UserEmail { get; set; }
            public string? UserPicture { get; set; }
            public string? Outcome { get; set; }
            public string? StageReached { get; set; }
            public string? Notes { get; set; }
            public int? PriorityId { get; set; }
            public bool IsDiscarded { get; set; }
            public DateTime LastContactAt { get; set; }
            public int? OpportunityId { get; set; }
            public string? OpportunityTitle { get; set; }
        }

        public class MatchRow
        {
            public int UserId { get; set; }
            public string? Name { get; set; }
            public string? Email { get; set; }
            public string? ProfilePictureUrl { get; set; }
            public string? LocationName { get; set; }
            public string? CountryName { get; set; }
            public bool IsAvailable { get; set; }
            public string? PreviousOutcome { get; set; }
            public string? PreviousStage { get; set; }
            public string? PreviousOpportunityTitle { get; set; }
            public DateTime LastContactAt { get; set; }
            public int? PriorityId { get; set; }
            public int EmployerCandidateHistoryId { get; set; }
            public int MatchScore { get; set; }
        }
    }
}
