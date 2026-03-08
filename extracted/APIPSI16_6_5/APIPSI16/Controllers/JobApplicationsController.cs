using System;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using APIPSI16.Data;
using APIPSI16.Models;
using APIPSI16.Models.DTOs;
using APIPSI16.Services;

namespace APIPSI16.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class JobApplicationsController : ControllerBase
    {
        private readonly xcleratesystemslinks_SampleDBContext _db;
        private readonly ILogger<JobApplicationsController> _logger;

        public JobApplicationsController(xcleratesystemslinks_SampleDBContext db, ILogger<JobApplicationsController> logger)
        {
            _db = db;
            _logger = logger;
        }

        // POST api/jobapplications/apply
        [HttpPost("apply")]
        public async Task<IActionResult> Apply([FromBody] ApplyDto dto)
        {
            var uid = GetUserId();
            if (uid == null) return Unauthorized();

            if (dto.OpportunityId <= 0) return BadRequest("OpportunityId is required.");

            if (!string.IsNullOrWhiteSpace(dto.Name) && dto.Name.Length > 50)
                return BadRequest("Name must be 50 characters or fewer.");

            if (await _db.JobApplications.AnyAsync(a => a.OpportunityId == dto.OpportunityId && a.UserId == uid))
                return Conflict("Already applied");

            // Enforce subscription limits for Free plan (plan=0)
            var user = await _db.Users.FindAsync(uid.Value);
            if (user != null && user.SubscriptionPlan == 0)
            {
                var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var appThisMonth = await _db.JobApplications
                    .CountAsync(a => a.UserId == uid.Value && a.AppliedAt >= monthStart);
                if (appThisMonth >= 5)
                    return StatusCode(429, new { message = "Limite de candidaturas mensais atingido para o plano Free (5/mês). Faz upgrade para Pro para candidaturas ilimitadas.", limitReached = true, plan = "Free", limit = 5 });
            }

            var app = new JobApplication
            {
                OpportunityId = dto.OpportunityId,
                UserId = uid.Value,
                Status = 0,
                AppliedAt = DateTime.UtcNow,
                Name = string.IsNullOrWhiteSpace(dto.Name) ? null : dto.Name,
                CoverLetter = string.IsNullOrWhiteSpace(dto.CoverLetter) ? null : dto.CoverLetter,
                PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber,
                ProfessionalUrl = string.IsNullOrWhiteSpace(dto.ProfessionalUrl) ? null : dto.ProfessionalUrl,
                PortfolioUrl = string.IsNullOrWhiteSpace(dto.PortfolioUrl) ? null : dto.PortfolioUrl,
                YearsOfExperience = dto.YearsOfExperience,
                OpenToRemote = dto.OpenToRemote,
                SelectedJobRoleIds = string.IsNullOrWhiteSpace(dto.SelectedJobRoleIds) ? null : dto.SelectedJobRoleIds
            };

            await _db.JobApplications.AddAsync(app);
            await _db.SaveChangesAsync();

            // Best-effort notification — failure here must not fail the application submission
            try
            {
                var opportunity = await _db.Opportunities.FindAsync(dto.OpportunityId);
                if (opportunity != null)
                {
                    var notifyUserId = opportunity.CreatorId ?? 0;
                    if (notifyUserId > 0)
                    {
                        await _db.Notifications.AddAsync(new Notification
                        {
                            UserId = notifyUserId,
                            ActorUserId = uid.Value,
                            Type = "JobApplied",
                            Payload = $"{{\"applicationId\":{app.JobApplicationId}}}",
                            IsRead = false,
                            CreatedAt = DateTime.UtcNow
                        });
                        await _db.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Best-effort notification failed for application {AppId} — application was still saved.", app.JobApplicationId);
            }

            return CreatedAtAction(nameof(Get), new { id = app.JobApplicationId }, app);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var uid = GetUserId();
            if (uid == null) return Unauthorized();

            var app = await _db.JobApplications
                .Include(a => a.Opportunity).ThenInclude(o => o.Company)
                .Include(a => a.User)
                .Include(a => a.InterviewRounds).ThenInclude(r => r.InterviewerUser)
                .FirstOrDefaultAsync(a => a.JobApplicationId == id);
            if (app == null) return NotFound();

            // Allow the applicant themselves, admins, or company members to view
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole != "0" && app.UserId != uid.Value)
            {
                // Check if requester is an employer-member of the opportunity's company
                var companyId = app.Opportunity?.CompanyId;
                if (companyId != null)
                {
                    var isMember = await _db.CompanyMembers
                        .AnyAsync(cm => cm.CompanyId == companyId.Value && cm.UserId == uid.Value);
                    if (!isMember) return Forbid();
                }
                else
                {
                    return Forbid();
                }
            }

            return Ok(app);
        }

        [HttpPost("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusDto dto)
        {
            var actorId = GetUserId();
            if (actorId == null) return Unauthorized();

            var app = await _db.JobApplications.FindAsync(id);
            if (app == null) return NotFound();

            app.Status = dto.NewStatus;
            app.UpdatedAt = DateTime.UtcNow;
            _db.JobApplications.Update(app);

            await _db.AuditLogs.AddAsync(new AuditLog { UserId = actorId.Value, Action = "UpdateApplicationStatus", TargetType = "JobApplication", TargetId = id, CreatedAt = DateTime.UtcNow });
            await _db.Notifications.AddAsync(new Notification
            {
                UserId = app.UserId,
                ActorUserId = actorId.Value,
                Type = "ApplicationStatusChanged",
                Payload = $"{{\"applicationId\":{id},\"newStatus\":{dto.NewStatus}}}",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            return Ok(app);
        }

        // POST: api/JobApplications/5/stage
        // Update application stage with audit trail
        [HttpPost("{id}/stage")]
        public async Task<IActionResult> UpdateStage(int id, [FromBody] UpdateStageDTO dto)
        {
            var actorId = GetUserId();
            if (actorId == null) return Unauthorized();

            var app = await _db.JobApplications
                .Include(a => a.Opportunity)
                .FirstOrDefaultAsync(a => a.JobApplicationId == id);
            
            if (app == null) return NotFound();

            // Verify user is authorized (active company member or admin)
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole != "0" && app.Opportunity?.CompanyId.HasValue == true)
            {
                var member = await _db.CompanyMembers
                    .FirstOrDefaultAsync(cm => cm.CompanyId == app.Opportunity.CompanyId.Value && cm.UserId == actorId.Value);
                if (member == null || member.Role < 1) return Forbid(); // Active member required
            }

            var oldStage = app.Status;
            app.Status = dto.NewStage;
            app.UpdatedAt = DateTime.UtcNow;
            _db.JobApplications.Update(app);

            // Create audit log
            await _db.AuditLogs.AddAsync(new AuditLog
            {
                UserId = actorId.Value,
                Action = "UpdateApplicationStage",
                TargetType = "JobApplication",
                TargetId = id,
                Metadata = $"Stage changed from {oldStage} to {dto.NewStage}",
                CreatedAt = DateTime.UtcNow
            });

            // Notify applicant
            await _db.Notifications.AddAsync(new Notification
            {
                UserId = app.UserId,
                ActorUserId = actorId.Value,
                Type = "ApplicationStageChanged",
                Payload = $"{{\"applicationId\":{id},\"newStage\":{dto.NewStage}}}",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            return Ok(app);
        }

        // GET: api/JobApplications/company/5/pipeline
        // Get all applications for a company grouped by stage
        [HttpGet("company/{companyId}/pipeline")]
        [Authorize(Roles = "0,2")]
        public async Task<IActionResult> GetCompanyPipeline(int companyId)
        {
            var actorId = GetUserId();
            if (actorId == null) return Unauthorized();

            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole != "0")
            {
                var member = await _db.CompanyMembers
                    .FirstOrDefaultAsync(cm => cm.CompanyId == companyId && cm.UserId == actorId.Value);
                if (member == null || member.Role < 1) return Forbid(); // Must be active member (role >= 1)
            }

            var applications = await _db.JobApplications
                .Include(a => a.Opportunity)
                .Include(a => a.User)
                .Where(a => a.Opportunity.CompanyId == companyId)
                .Select(a => new
                {
                    a.JobApplicationId,
                    a.OpportunityId,
                    OpportunityTitle = a.Opportunity.Title,
                    a.UserId,
                    UserName = a.User.Name,
                    a.Status,
                    a.AppliedAt,
                    a.UpdatedAt
                })
                .ToListAsync();

            var pipeline = applications.GroupBy(a => a.Status)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    Stage = g.Key,
                    StageName = GetStageName(g.Key),
                    Applications = g.ToList()
                });

            return Ok(pipeline);
        }

        // GET: api/jobapplications/for-company/{companyId}
        // Returns all applications for a company's opportunities — accessible by admin (role=0)
        // and by employers (role=2) who are verified company members.
        [HttpGet("for-company/{companyId}")]
        [Authorize(Roles = "0,2")]
        public async Task<IActionResult> ForCompany(int companyId, int? opportunityId = null)
        {
            var actorId = GetUserId();
            if (actorId == null) return Unauthorized();

            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole != "0")
            {
                var memberRole = await _db.CompanyMembers
                    .Where(cm => cm.CompanyId == companyId && cm.UserId == actorId.Value)
                    .Select(cm => (int?)cm.Role)
                    .FirstOrDefaultAsync();
                if (memberRole == null || memberRole < 1) return Forbid(); // Active member (role >= 1) required
            }

            var query = _db.JobApplications
                .Include(a => a.Opportunity).ThenInclude(o => o.Company)
                .Include(a => a.User).ThenInclude(u => u.UserSkills).ThenInclude(us => us.Skill)
                .Where(a => a.Opportunity != null && a.Opportunity.CompanyId == companyId);

            if (opportunityId.HasValue)
                query = query.Where(a => a.OpportunityId == opportunityId.Value);

            var list = await query
                .OrderByDescending(a => a.AppliedAt)
                .ToListAsync();

            return Ok(list);
        }

        // GET: api/jobapplications (admin only — full list)
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var uid = GetUserId();
            if (uid == null) return Unauthorized();

            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole != "0") return Forbid();

            var list = await _db.JobApplications
                .Include(a => a.Opportunity).ThenInclude(o => o.Company)
                .Include(a => a.User)
                .OrderByDescending(a => a.AppliedAt)
                .ToListAsync();

            return Ok(list);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var uid = GetUserId();
            if (uid == null) return Unauthorized();

            var app = await _db.JobApplications.FindAsync(id);
            if (app == null) return NotFound();

            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole != "0" && app.UserId != uid.Value) return Forbid();

            _db.JobApplications.Remove(app);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("user/{userId}")]
        public async Task<IActionResult> ForUser(int userId)
        {
            var uid = GetUserId();
            if (uid == null) return Unauthorized();

            if (uid != userId && !User.IsInRole("0")) return Forbid();

            var list = await _db.JobApplications
                .Include(a => a.Opportunity).ThenInclude(o => o.Company)
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.AppliedAt)
                .ToListAsync();

            return Ok(list);
        }

        // POST: api/jobapplications/{id}/applicant-respond
        // Applicant can respond to an interview invitation (stage=2) or offer (stage=3)
        // Response: 1=Accept, 2=Decline
        [HttpPost("{id}/applicant-respond")]
        public async Task<IActionResult> ApplicantRespond(int id, [FromBody] ApplicantResponseDto dto)
        {
            var uid = GetUserId();
            if (uid == null) return Unauthorized();

            var app = await _db.JobApplications
                .Include(a => a.Opportunity)
                .FirstOrDefaultAsync(a => a.JobApplicationId == id);
            if (app == null) return NotFound();

            if (app.UserId != uid.Value) return Forbid();

            if (app.Status != 2 && app.Status != 3)
                return BadRequest("A resposta só é possível quando a candidatura está em fase de Entrevista (2) ou Oferta (3).");

            if (dto.Response != 1 && dto.Response != 2)
                return BadRequest("Resposta inválida. Use 1=Aceitar, 2=Recusar.");

            app.ApplicantResponse = dto.Response;
            app.UpdatedAt = DateTime.UtcNow;

            // If applicant accepts the offer → move to Hired (4)
            if (app.Status == 3 && dto.Response == 1)
            {
                app.Status = 4;
                // Mark user as no longer open to work
                var applicant = await _db.Users.FindAsync(uid.Value);
                if (applicant != null)
                    applicant.IsOpenToWork = false;
            }
            // If applicant declines the offer or interview → move to Rejected (5)
            else if (dto.Response == 2)
                app.Status = 5;

            _db.JobApplications.Update(app);

            // Notify the opportunity creator / company
            if (app.Opportunity?.CreatorId > 0)
            {
                await _db.Notifications.AddAsync(new Notification
                {
                    UserId = app.Opportunity.CreatorId!.Value,
                    ActorUserId = uid.Value,
                    Type = dto.Response == 1 ? "ApplicantAccepted" : "ApplicantDeclined",
                    Payload = $"{{\"applicationId\":{id},\"response\":{dto.Response}}}",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _db.AuditLogs.AddAsync(new AuditLog
            {
                UserId = uid.Value,
                Action = dto.Response == 1 ? "ApplicantAccepted" : "ApplicantDeclined",
                TargetType = "JobApplication",
                TargetId = id,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            return Ok(app);
        }

        // POST: api/jobapplications/{id}/employer-action
        // Employer performs a stage-appropriate action on an application
        // Actions: "screen", "interview", "offer", "hire", "reject"
        [HttpPost("{id}/employer-action")]
        [Authorize(Roles = "0,2")]
        public async Task<IActionResult> EmployerAction(int id, [FromBody] EmployerActionDto dto)
        {
            var actorId = GetUserId();
            if (actorId == null) return Unauthorized();

            var app = await _db.JobApplications
                .Include(a => a.Opportunity)
                .FirstOrDefaultAsync(a => a.JobApplicationId == id);
            if (app == null) return NotFound();

            // Verify employer is a member of this company
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole != "0" && app.Opportunity?.CompanyId.HasValue == true)
            {
                var member = await _db.CompanyMembers
                    .FirstOrDefaultAsync(cm => cm.CompanyId == app.Opportunity.CompanyId.Value && cm.UserId == actorId.Value);
                if (member == null) return Forbid();

                // Check permission based on company role
                // Role 1 (Recruiter): can screen/interview/reject
                // Role 2 (HRManager): can screen/interview/offer/reject
                // Role 3 (CompanyAdmin): all actions
                var action = dto.Action.ToLowerInvariant();
                if (member.Role == 1 && (action == "offer" || action == "hire"))
                    return StatusCode(403, "Apenas Gestores RH ou admins de empresa podem fazer ofertas.");
            }

            var oldStatus = app.Status;
            byte newStatus = app.Status;
            string actionType = dto.Action.ToLowerInvariant();

            switch (actionType)
            {
                case "screen":
                    if (app.Status != 0) return BadRequest("Só pode mover para Em Análise a partir do estado Enviada.");
                    newStatus = 1;
                    break;
                case "interview":
                    if (app.Status != 1) return BadRequest("Só pode mover para Entrevista a partir do estado Em Análise.");
                    newStatus = 2;
                    break;
                case "offer":
                    if (app.Status != 2) return BadRequest("Só pode fazer oferta a partir do estado Entrevista.");
                    newStatus = 3;
                    break;
                case "hire":
                    if (app.Status != 3) return BadRequest("Só pode contratar a partir do estado Oferta.");
                    newStatus = 4;
                    break;
                case "reject":
                    newStatus = 5;
                    break;
                default:
                    return BadRequest("Ação inválida. Use: screen, interview, offer, hire, reject.");
            }

            app.Status = newStatus;
            app.ApplicantResponse = null; // reset applicant response on new stage
            app.UpdatedAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(dto.Notes))
                app.LatestEmployerMessage = dto.Notes.Trim();
            _db.JobApplications.Update(app);

            await _db.AuditLogs.AddAsync(new AuditLog
            {
                UserId = actorId.Value,
                Action = $"EmployerAction:{dto.Action}",
                TargetType = "JobApplication",
                TargetId = id,
                Metadata = $"Stage changed from {oldStatus} to {newStatus}. Notes: {dto.Notes}",
                CreatedAt = DateTime.UtcNow
            });

            await _db.Notifications.AddAsync(new Notification
            {
                UserId = app.UserId,
                ActorUserId = actorId.Value,
                Type = "ApplicationStageChanged",
                Payload = $"{{\"applicationId\":{id},\"action\":\"{dto.Action}\",\"newStage\":{newStatus}}}",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            return Ok(app);
        }

        // GET: api/jobapplications/{id}/match-score
        // Returns match percentage between the applicant and the opportunity
        [HttpGet("{id}/match-score")]
        public async Task<IActionResult> GetMatchScore(int id)
        {
            var app = await _db.JobApplications
                .Include(a => a.Opportunity)
                .FirstOrDefaultAsync(a => a.JobApplicationId == id);
            if (app == null) return NotFound();

            var score = await CalculateMatchScoreAsync(app.UserId, app.Opportunity);
            return Ok(new { applicationId = id, matchScore = score });
        }

        private async Task<int> CalculateMatchScoreAsync(int userId, Opportunity? opp)
        {
            if (opp == null) return 0;

            var user = await _db.Users.FindAsync(userId);

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
                var userPrefIds = await _db.UserJobPreferences
                    .Where(p => p.UserId == userId)
                    .Select(p => p.JobRoleId)
                    .ToListAsync();
                var matches = userPrefIds.Count(id => requiredRoleIds.Contains(id));
                roleScore = (int)Math.Round((double)matches / requiredRoleIds.Count * 100);
            }

            bool locationMatched = MatchScoreHelper.LocationsMatch(user?.Location, opp.Location);
            bool hasLocations = !string.IsNullOrWhiteSpace(user?.Location) && !string.IsNullOrWhiteSpace(opp.Location);

            return MatchScoreHelper.ComputeWeightedScore(roleScore, locationMatched, requiredRoleIds.Count > 0, hasLocations);
        }

        private int? GetUserId()
        {
            var sid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(sid, out var id) ? id : (int?)null;
        }

        private string GetStageName(byte stage)
        {
            return stage switch
            {
                0 => "Applied",
                1 => "Screening",
                2 => "Interview",
                3 => "Offer",
                4 => "Hired",
                5 => "Rejected",
                _ => "Unknown"
            };
        }
    }

    public class ApplicantResponseDto
    {
        /// <summary>1=Accept, 2=Decline</summary>
        public byte Response { get; set; }
    }

    public class EmployerActionDto
    {
        /// <summary>screen | interview | offer | hire | reject</summary>
        public string Action { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }
}