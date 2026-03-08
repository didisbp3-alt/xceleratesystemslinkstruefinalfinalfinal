    using APIPSI16.Data;
using APIPSI16.Models;
using APIPSI16.Models.DTOs;
using APIPSI16.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Security.Claims;
using System.Threading.Tasks;

namespace APIPSI16.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Require JWT for all actions
    public class OpportunitiesController : ControllerBase
    {
        private readonly xcleratesystemslinks_SampleDBContext _context;
        private readonly ILogger<OpportunitiesController> _logger;

        public OpportunitiesController(xcleratesystemslinks_SampleDBContext context, ILogger<OpportunitiesController> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger;
        }

        // GET: api/Opportunities/recommended  – personalised for current user
        [HttpGet("recommended")]
        public async Task<IActionResult> GetRecommended()
        {
            var uid = GetCurrentUserId();
            if (uid == null) return Unauthorized();

            var user = await _context.Users.FindAsync(uid.Value);
            if (user == null) return NotFound();

            var q = _context.Opportunities.Include(o => o.Company).AsQueryable();

            // Match on EmploymentType or SeniorityLevel based on user's job preference
            if (user.JobPreference.HasValue)
                q = q.Where(o => o.EmploymentType == (byte?)user.JobPreference.Value);

            var results = await q.Select(o => new
            {
                o.Id, o.Title, o.Location,
                LocationId = o.LocationId,
                LocationName = o.LocationNav != null ? o.LocationNav.Name : o.Location,
                CountryName = o.CountryNav != null ? o.CountryNav.Name : null,
                o.EmploymentType, o.SeniorityLevel, o.RemoteOption,
                o.CompanyId, CompanyName = o.Company != null ? o.Company.Name : null
            }).ToListAsync();

            return Ok(results);
        }

        // GET: api/Opportunities
        // All authenticated users can view opportunities
        [HttpGet]
        public async Task<IActionResult> GetOpportunities()
        {
            var opportunities = await _context.Opportunities
                .Select(o => new
                {
                    o.Id,
                    o.Title,
                    o.Location,
                    LocationId = o.LocationId,
                    LocationName = o.LocationNav != null ? o.LocationNav.Name : o.Location,
                    CountryName = o.CountryNav != null ? o.CountryNav.Name : null,
                    o.EmploymentType,
                    o.SeniorityLevel,
                    o.RemoteOption,
                    o.CompanyId,
                    CompanyName = o.Company.Name
                })
                .ToListAsync();

            return Ok(opportunities);
        }

        // GET: api/Opportunities/5
        // All authenticated users can view opportunity details
        [HttpGet("{id}")]
        public async Task<IActionResult> GetOpportunity(int id)
        {
            var opportunity = await _context.Opportunities
                .Include(o => o.Company)
                .Include(o => o.LocationNav)
                    .ThenInclude(l => l != null ? l.Country : null)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (opportunity == null) return NotFound();

            return Ok(opportunity);
        }

        // POST: api/Opportunities
        // Only admins and employers can create opportunities
        [HttpPost]
        [Authorize(Roles = "0,2")] // Admin or Employer
        public async Task<IActionResult> CreateOpportunity([FromBody] Opportunity opportunity)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var currentUserId = GetCurrentUserId();
            var userRole = GetCurrentUserRole();

            // Employers can only create opportunities for companies they're members of (Recruiter, HRManager, or CompanyAdmin)
            if (userRole == "2" && opportunity.CompanyId.HasValue)
            {
                if (!currentUserId.HasValue) return Unauthorized();

                var member = await _context.CompanyMembers
                    .FirstOrDefaultAsync(cm => cm.CompanyId == opportunity.CompanyId.Value && cm.UserId == currentUserId.Value);

                if (member == null)
                    return StatusCode(403, "Só podes criar vagas para empresas onde és membro.");
                // Role 1=Recruiter (can create), 2=HRManager (can create), 3=CompanyAdmin (can create)
                if (member.Role < 1)
                    return StatusCode(403, "Membro pendente não pode criar vagas. Aguarda a aprovação do admin da empresa.");
            }

            _context.Add(opportunity);
            await _context.SaveChangesAsync();

            //notify all job-seeker users who have a perfect match with this opportunity
            try
            {
                await NotifyPerfectMatchUsersAsync(opportunity);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Best-effort perfect-match notifications failed for opportunity {OpportunityId} — opportunity was still saved.", opportunity.Id);
            }

            return CreatedAtAction(nameof(GetOpportunity), new { id = opportunity.Id }, opportunity);
        }

        // PUT: api/Opportunities/5
        // Admins can update any; employers can update their company's opportunities
        [HttpPut("{id}")]
        [Authorize(Roles = "0,2")] // Admin or Employer
        public async Task<IActionResult> UpdateOpportunity(int id, [FromBody] Opportunity opportunity)
        {
            if (id != opportunity.Id) return BadRequest();

            var existingOpp = await _context.Opportunities.FindAsync(id);
            if (existingOpp == null) return NotFound();

            var currentUserId = GetCurrentUserId();
            var userRole = GetCurrentUserRole();

            // Employers can only update opportunities for companies they're active members of (role >= 1)
            if (userRole == "2" && existingOpp.CompanyId.HasValue)
            {
                if (!currentUserId.HasValue) return Unauthorized();

                var member = await _context.CompanyMembers
                    .FirstOrDefaultAsync(cm => cm.CompanyId == existingOpp.CompanyId.Value && cm.UserId == currentUserId.Value);

                if (member == null || member.Role < 1)
                    return StatusCode(403, "Não tens permissão para editar vagas desta empresa.");
            }

            _context.Entry(existingOpp).State = EntityState.Detached;
            _context.Entry(opportunity).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!OpportunityExists(id)) return NotFound();
                throw;
            }

            return NoContent();
        }

        // DELETE: api/Opportunities/5
        // Only admins can delete opportunities
        [HttpDelete("{id}")]
        [Authorize(Roles = "0")] // Admin only
        public async Task<IActionResult> DeleteOpportunity(int id)
        {
            var opportunity = await _context.Opportunities.FindAsync(id);
            if (opportunity == null) return NotFound();

            _context.Opportunities.Remove(opportunity);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/Opportunities/{id}/match
        // Returns match percentage for the current user vs this opportunity
        [HttpGet("{id}/match")]
        public async Task<IActionResult> GetMatchScore(int id)
        {
            var uid = GetCurrentUserId();
            if (uid == null) return Unauthorized();

            var opp = await _context.Opportunities.FindAsync(id);
            if (opp == null) return NotFound();

            var score = await CalculateMatchScoreAsync(uid.Value, opp);
            return Ok(new { opportunityId = id, matchScore = score });
        }

        // GET: api/Opportunities/with-match
        // Returns all opportunities with match percentage for the current user
        [HttpGet("with-match")]
        public async Task<IActionResult> GetOpportunitiesWithMatch()
        {
            var uid = GetCurrentUserId();
            if (uid == null) return Unauthorized();

            var user = await _context.Users
                .Include(u => u.LocationNav)
                    .ThenInclude(l => l != null ? l.Country : null)
                .FirstOrDefaultAsync(u => u.UserId == uid.Value);

            var opportunities = await _context.Opportunities
                .Include(o => o.Company)
                .Include(o => o.LocationNav)
                    .ThenInclude(l => l != null ? l.Country : null)
                .ToListAsync();

            var userPrefIds = await _context.UserJobPreferences
                .Where(p => p.UserId == uid.Value)
                .Select(p => p.JobRoleId)
                .ToListAsync();

            var userLocationId = user?.LocationId;
            var userRegion = user?.LocationNav?.Region;
            var userCountryCode = user?.LocationNav?.Country?.Code ?? user?.CountryNav?.Code;

            var results = opportunities.Select(o =>
            {
                var requiredRoleIds = string.IsNullOrWhiteSpace(o.RequiredJobRoleIds)
                    ? new HashSet<int>()
                    : o.RequiredJobRoleIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => int.TryParse(s.Trim(), out var v) ? v : 0)
                        .Where(v => v > 0)
                        .ToHashSet();

                // Job role score (70% weight) – F1-score balances user precision and job recall
                int roleScore = 0;
                if (requiredRoleIds.Count > 0)
                {
                    var matches = userPrefIds.Count(id => requiredRoleIds.Contains(id));
                    roleScore = MatchScoreHelper.ComputeRoleScore(matches, userPrefIds.Count, requiredRoleIds.Count);
                }

                // Location score (30% weight) – use structured IDs when available
                int locationScore;
                if (userLocationId.HasValue && o.LocationId.HasValue)
                {
                    var oppRegion = o.LocationNav?.Region;
                    var oppCountryCode = o.LocationNav?.Country?.Code;
                    locationScore = MatchScoreHelper.ComputeLocationScore(
                        userLocationId, o.LocationId,
                        userRegion, oppRegion,
                        userCountryCode, oppCountryCode);
                }
                else
                {
                    // Fall back to legacy string matching
                    bool legacyMatch = MatchScoreHelper.LocationsMatch(user?.Location, o.Location);
                    bool hasLegacyLocations = !string.IsNullOrWhiteSpace(user?.Location) && !string.IsNullOrWhiteSpace(o.Location);
                    locationScore = hasLegacyLocations ? (legacyMatch ? 100 : 0) : -1;
                }

                int matchScore = MatchScoreHelper.ComputeWeightedScore(roleScore, locationScore, requiredRoleIds.Count > 0);

                var locationName = o.LocationNav?.Name ?? o.Location;
                var countryName = o.LocationNav?.Country?.Name;

                return new
                {
                    o.Id,
                    o.Title,
                    o.Location,
                    LocationId = o.LocationId,
                    LocationName = locationName,
                    CountryName = countryName,
                    o.EmploymentType,
                    o.SeniorityLevel,
                    o.RemoteOption,
                    o.CompanyId,
                    CompanyName = o.Company?.Name,
                    o.RequiredJobRoleIds,
                    o.OpportunityType,
                    o.ApplicationScope,
                    MatchScore = matchScore
                };
            }).OrderByDescending(o => o.MatchScore).ToList();

            return Ok(results);
        }

        /// <summary>
        /// Finds all job-seeker users (Role == 1) who score 100% against <paramref name="opportunity"/>
        /// and creates a <see cref="Notification"/> of type "PerfectOpportunityMatch" for each one.
        /// Uses a single bulk query for candidates and their preferences to stay efficient.
        /// </summary>
        private async Task NotifyPerfectMatchUsersAsync(Opportunity opportunity)
        {
            // Ensure the opportunity's location navigation is loaded
            if (opportunity.LocationNav == null && opportunity.LocationId.HasValue)
            {
                opportunity.LocationNav = await _context.Locations
                    .Include(l => l.Country)
                    .FirstOrDefaultAsync(l => l.LocationId == opportunity.LocationId.Value);
            }
            var requiredRoleIds = string.IsNullOrWhiteSpace(opportunity.RequiredJobRoleIds)
                ? new HashSet<int>()
                : opportunity.RequiredJobRoleIds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s.Trim(), out var v) ? v : 0)
                    .Where(v => v > 0)
                    .ToHashSet();
            // Matching is only meaningful when the opportunity specifies at least roles or a location
            bool hasRoles = requiredRoleIds.Count > 0;
            bool hasLocation = opportunity.LocationId.HasValue
                || !string.IsNullOrWhiteSpace(opportunity.Location);
            if (!hasRoles && !hasLocation)
                return;
            var oppRegion = opportunity.LocationNav?.Region;
            var oppCountryCode = opportunity.LocationNav?.Country?.Code;
            // Load all job-seeker candidates (Role == 1) with their location data
            var candidates = await _context.Users
                .Include(u => u.LocationNav).ThenInclude(l => l != null ? l.Country : null)
                .Where(u => u.Role == 1)
                .ToListAsync();
            if (candidates.Count == 0)
                return;
            // Bulk-load job-role preferences for all candidates in one query
            var candidateIds = candidates.Select(c => c.UserId).ToList();
            var allPrefs = await _context.UserJobPreferences
                .Where(p => candidateIds.Contains(p.UserId))
                .ToListAsync();
            var prefsByUser = allPrefs
                .GroupBy(p => p.UserId)
                .ToDictionary(g => g.Key, g => g.Select(p => p.JobRoleId).ToHashSet());
            var notifications = new List<Notification>();
            var now = DateTime.UtcNow;
            foreach (var user in candidates)
            {
                var userPrefIds = prefsByUser.TryGetValue(user.UserId, out var prefs)
                    ? prefs
                    : new HashSet<int>();
                int roleScore = 0;
                if (hasRoles && requiredRoleIds.Count > 0)
                {
                    var matchCount = userPrefIds.Count(id => requiredRoleIds.Contains(id));
                    roleScore = MatchScoreHelper.ComputeRoleScore(matchCount, userPrefIds.Count, requiredRoleIds.Count);
                }
                int locationScore;
                if (user.LocationId.HasValue && opportunity.LocationId.HasValue)
                {
                    locationScore = MatchScoreHelper.ComputeLocationScore(
                        user.LocationId, opportunity.LocationId,
                        user.LocationNav?.Region, oppRegion,
                        user.LocationNav?.Country?.Code, oppCountryCode);
                }
                else
                {
                    bool legacyMatch = MatchScoreHelper.LocationsMatch(user.Location, opportunity.Location);
                    bool hasLegacy = !string.IsNullOrWhiteSpace(user.Location)
                        && !string.IsNullOrWhiteSpace(opportunity.Location);
                    locationScore = hasLegacy ? (legacyMatch ? 100 : 0) : -1;
                }
                int score = MatchScoreHelper.ComputeWeightedScore(roleScore, locationScore, hasRoles);
                if (score >= 90)
                {
                    var payload = JsonSerializer.Serialize(new
                    {
                        opportunityId = opportunity.Id,
                        opportunityTitle = opportunity.Title ?? string.Empty,
                        matchScore = score
                    });
                    notifications.Add(new Notification
                    {
                        UserId = user.UserId,
                        ActorUserId = null,
                        Type = "HighMatchOpportunity",
                        Payload = payload,
                        IsRead = false,
                        CreatedAt = now
                    });
                }
            }
            if (notifications.Count > 0)
            {
                await _context.Notifications.AddRangeAsync(notifications);
                await _context.SaveChangesAsync();
            }
        }

        private async Task<int> CalculateMatchScoreAsync(int userId, Opportunity opp)
        {
            var user = await _context.Users
                .Include(u => u.LocationNav)
                    .ThenInclude(l => l != null ? l.Country : null)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            // Load opportunity's location if not already loaded
            if (opp.LocationNav == null && opp.LocationId.HasValue)
            {
                opp.LocationNav = await _context.Locations
                    .Include(l => l.Country)
                    .FirstOrDefaultAsync(l => l.LocationId == opp.LocationId.Value);
            }

            var requiredRoleIds = string.IsNullOrWhiteSpace(opp.RequiredJobRoleIds)
                ? new HashSet<int>()
                : opp.RequiredJobRoleIds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s.Trim(), out var v) ? v : 0)
                    .Where(v => v > 0)
                    .ToHashSet();

            int roleScore = 0;
            if (requiredRoleIds.Count > 0)
            {
                var userPrefIds = await _context.UserJobPreferences
                    .Where(p => p.UserId == userId)
                    .Select(p => p.JobRoleId)
                    .ToListAsync();
                var matches = userPrefIds.Count(id => requiredRoleIds.Contains(id));
                roleScore = MatchScoreHelper.ComputeRoleScore(matches, userPrefIds.Count, requiredRoleIds.Count);
            }

            int locationScore;
            if (user?.LocationId.HasValue == true && opp.LocationId.HasValue)
            {
                locationScore = MatchScoreHelper.ComputeLocationScore(
                    user.LocationId, opp.LocationId,
                    user.LocationNav?.Region, opp.LocationNav?.Region,
                    user.LocationNav?.Country?.Code, opp.LocationNav?.Country?.Code);
            }
            else
            {
                bool legacyMatch = MatchScoreHelper.LocationsMatch(user?.Location, opp.Location);
                bool hasLegacy = !string.IsNullOrWhiteSpace(user?.Location) && !string.IsNullOrWhiteSpace(opp.Location);
                locationScore = hasLegacy ? (legacyMatch ? 100 : 0) : -1;
            }

            return MatchScoreHelper.ComputeWeightedScore(roleScore, locationScore, requiredRoleIds.Count > 0);
        }

        // GET: api/Opportunities/{id}/employer-matches
        // Returns previous contacts (from EmployerCandidateHistory) scored against this opportunity.
        // Available to employers and admins.
        [HttpGet("{id}/employer-matches")]
        [Authorize(Roles = "0,2")]
        public async Task<IActionResult> GetEmployerMatches(int id)
        {
            var actorId = GetCurrentUserId();
            var actorRole = GetCurrentUserRole();

            var opp = await _context.Opportunities
                .Include(o => o.LocationNav).ThenInclude(l => l != null ? l.Country : null)
                .FirstOrDefaultAsync(o => o.Id == id);
            if (opp == null) return NotFound();

            // Employers can only see matches for their own company's opportunity
            if (actorRole == "2" && opp.CompanyId.HasValue && actorId.HasValue)
            {
                var isMember = await _context.CompanyMembers
                    .AnyAsync(cm => cm.CompanyId == opp.CompanyId.Value && cm.UserId == actorId.Value && cm.Role >= 1);
                if (!isMember) return StatusCode(403, "Só podes ver matches para empresas onde és membro.");
            }

            var requiredRoleIds = string.IsNullOrWhiteSpace(opp.RequiredJobRoleIds)
                ? new HashSet<int>()
                : opp.RequiredJobRoleIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s.Trim(), out var v) ? v : 0)
                    .Where(v => v > 0).ToHashSet();

            // Load all EmployerCandidateHistory entries for this company, not discarded
            var histories = await _context.EmployerCandidateHistories
                .Where(h => h.CompanyId == opp.CompanyId && !h.IsDiscarded)
                .Include(h => h.User)
                    .ThenInclude(u => u.LocationNav).ThenInclude(l => l != null ? l.Country : null)
                .Include(h => h.Opportunity)
                .ToListAsync();

            // Deduplicate by UserId (keep the most recent contact per user)
            var byUser = histories
                .GroupBy(h => h.UserId)
                .Select(g => g.OrderByDescending(h => h.LastContactAt).First())
                .ToList();

            // Score each candidate
            var oppRegion = opp.LocationNav?.Region;
            var oppCountryCode = opp.LocationNav?.Country?.Code;

            var matches = new List<EmployerMatchDto>();
            foreach (var h in byUser)
            {
                var user = h.User;
                // Job preferences
                var userPrefIds = await _context.UserJobPreferences
                    .Where(p => p.UserId == user.UserId)
                    .Select(p => p.JobRoleId).ToListAsync();

                int roleScore = 0;
                if (requiredRoleIds.Count > 0)
                {
                    var cnt = userPrefIds.Count(rid => requiredRoleIds.Contains(rid));
                    roleScore = (int)Math.Round((double)cnt / requiredRoleIds.Count * 100);
                }

                int locationScore = -1;
                if (user.LocationId.HasValue && opp.LocationId.HasValue)
                {
                    locationScore = MatchScoreHelper.ComputeLocationScore(
                        user.LocationId, opp.LocationId,
                        user.LocationNav?.Region, oppRegion,
                        user.LocationNav?.Country?.Code, oppCountryCode);
                }

                int score = MatchScoreHelper.ComputeWeightedScore(roleScore, locationScore, requiredRoleIds.Count > 0);

                matches.Add(new EmployerMatchDto
                {
                    UserId = user.UserId,
                    Name = user.Name,
                    Email = user.Email,
                    ProfilePictureUrl = user.ProfilePictureUrl,
                    LocationName = user.LocationNav?.Name ?? user.Location,
                    CountryName = user.LocationNav?.Country?.Name,
                    JobPreference = user.JobPreference,
                    IsAvailable = user.JobPreference != null && user.JobPreference > 0,
                    PreviousOutcome = h.Outcome,
                    PreviousStage = h.StageReached,
                    PreviousOpportunityTitle = h.Opportunity?.Title,
                    LastContactAt = h.LastContactAt,
                    PriorityId = h.PriorityId,
                    EmployerCandidateHistoryId = h.EmployerCandidateHistoryId,
                    MatchScore = score
                });
            }

            return Ok(matches.OrderByDescending(m => m.MatchScore).ToList());
        }

        // GET: api/Opportunities/employer-contacts?companyId=X
        // Full contact history for a company (for the contacts list page).
        [HttpGet("employer-contacts")]
        [Authorize(Roles = "0,2")]
        public async Task<IActionResult> GetEmployerContacts([FromQuery] int companyId, [FromQuery] bool discarded = false)
        {
            var actorId = GetCurrentUserId();
            var actorRole = GetCurrentUserRole();

            if (actorRole == "2" && actorId.HasValue)
            {
                var isMember = await _context.CompanyMembers
                    .AnyAsync(cm => cm.CompanyId == companyId && cm.UserId == actorId.Value && cm.Role >= 1);
                if (!isMember) return Forbid();
            }

            var contacts = await _context.EmployerCandidateHistories
                .Where(h => h.CompanyId == companyId && h.IsDiscarded == discarded)
                .Include(h => h.User)
                .Include(h => h.Opportunity)
                .OrderBy(h => h.IsDiscarded ? 1 : 0)
                .ThenBy(h => h.PriorityId ?? int.MaxValue)
                .ThenByDescending(h => h.LastContactAt)
                .Select(h => new
                {
                    h.EmployerCandidateHistoryId,
                    h.UserId,
                    UserName = h.User.Name,
                    UserEmail = h.User.Email,
                    UserPicture = h.User.ProfilePictureUrl,
                    h.Outcome,
                    h.StageReached,
                    h.Notes,
                    h.PriorityId,
                    h.IsDiscarded,
                    h.LastContactAt,
                    OpportunityId = h.OpportunityId,
                    OpportunityTitle = h.Opportunity != null ? h.Opportunity.Title : null
                })
                .ToListAsync();

            return Ok(contacts);
        }

        // PUT: api/Opportunities/employer-contacts/{id}/priority
        [HttpPut("employer-contacts/{historyId}/priority")]
        [Authorize(Roles = "0,2")]
        public async Task<IActionResult> SetContactPriority(int historyId, [FromBody] SetPriorityDto dto)
        {
            var entry = await _context.EmployerCandidateHistories.FindAsync(historyId);
            if (entry == null) return NotFound();

            var actorId = GetCurrentUserId();
            var actorRole = GetCurrentUserRole();
            if (actorRole == "2" && actorId.HasValue)
            {
                var isMember = await _context.CompanyMembers
                    .AnyAsync(cm => cm.CompanyId == entry.CompanyId && cm.UserId == actorId.Value && cm.Role >= 1);
                if (!isMember) return Forbid();
            }

            entry.PriorityId = dto.PriorityId;
            await _context.SaveChangesAsync();
            return Ok(new { entry.EmployerCandidateHistoryId, entry.PriorityId });
        }

        // PUT: api/Opportunities/employer-contacts/{id}/discard
        [HttpPut("employer-contacts/{historyId}/discard")]
        [Authorize(Roles = "0,2")]
        public async Task<IActionResult> DiscardContact(int historyId, [FromBody] DiscardDto dto)
        {
            var entry = await _context.EmployerCandidateHistories.FindAsync(historyId);
            if (entry == null) return NotFound();

            var actorId = GetCurrentUserId();
            var actorRole = GetCurrentUserRole();
            if (actorRole == "2" && actorId.HasValue)
            {
                var isMember = await _context.CompanyMembers
                    .AnyAsync(cm => cm.CompanyId == entry.CompanyId && cm.UserId == actorId.Value && cm.Role >= 1);
                if (!isMember) return Forbid();
            }

            entry.IsDiscarded = dto.Discard;
            await _context.SaveChangesAsync();
            return Ok(new { entry.EmployerCandidateHistoryId, entry.IsDiscarded });
        }

        private bool OpportunityExists(int id)
        {
            return _context.Opportunities.Any(o => o.Id == id);
        }

        private int? GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
        }

        private string? GetCurrentUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value;
        }
    }

    public class SetPriorityDto { public int? PriorityId { get; set; } }
    public class DiscardDto { public bool Discard { get; set; } }
}
