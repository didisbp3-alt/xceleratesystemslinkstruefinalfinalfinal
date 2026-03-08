using System;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using APIPSI16.Data;
using APIPSI16.Models;
using APIPSI16.Services;

namespace APIPSI16.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserSkillsController : ControllerBase
    {
        private readonly xcleratesystemslinks_SampleDBContext _db;
        private readonly IFileStorageService _fileStorage;

        public UserSkillsController(xcleratesystemslinks_SampleDBContext db, IFileStorageService fileStorage)
        {
            _db = db;
            _fileStorage = fileStorage;
        }

        [HttpGet("user/{userId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetForUser(int userId)
        {
            var skills = await _db.UserSkills
                .Where(us => us.UserId == userId)
                .Include(us => us.Skill)
                .Select(us => new UserSkillDto
                {
                    UserSkillId = us.UserSkillId,
                    UserId = us.UserId,
                    SkillId = us.SkillId,
                    Skill = new SkillDto { SkillId = us.Skill.SkillId, Name = us.Skill.Name },
                    EndorsementCount = us.EndorsementCount,
                    AddedAt = us.AddedAt
                })
                .ToListAsync();

            return Ok(skills);
        }

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] AddUserSkillDto dto)
        {
            var uid = GetUserId();
            if (uid == null) return Unauthorized();
            if (dto.UserId != uid && !User.IsInRole("0")) return Forbid();

            if (await _db.UserSkills.AnyAsync(x => x.UserId == dto.UserId && x.SkillId == dto.SkillId))
                return Conflict("Already added");

            var us = new UserSkill
            {
                UserId = dto.UserId,
                SkillId = dto.SkillId,
                EndorsementCount = 0,
                AddedAt = DateTime.UtcNow
            };

            await _db.UserSkills.AddAsync(us);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetForUser), new { userId = us.UserId }, new UserSkillDto
            {
                UserSkillId = us.UserSkillId,
                UserId = us.UserId,
                SkillId = us.SkillId,
                EndorsementCount = us.EndorsementCount,
                AddedAt = us.AddedAt
            });
        }

        [HttpPost("{userSkillId}/endorse")]
        public async Task<IActionResult> Endorse(int userSkillId)
        {
            var endorserId = GetUserId();
            if (endorserId == null) return Unauthorized();

            var us = await _db.UserSkills.FindAsync(userSkillId);
            if (us == null) return NotFound();
            if (us.UserId == endorserId) return BadRequest("Cannot endorse yourself.");

            var already = await _db.SkillEndorsements.AnyAsync(se => se.UserSkillId == userSkillId && se.EndorserUserId == endorserId);
            if (already) return Conflict("Already endorsed");

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                await _db.SkillEndorsements.AddAsync(new SkillEndorsement
                {
                    UserSkillId = userSkillId,
                    EndorserUserId = endorserId.Value,
                    CreatedAt = DateTime.UtcNow
                });
                us.EndorsementCount = us.EndorsementCount + 1;
                _db.UserSkills.Update(us);
                await _db.SaveChangesAsync();

                await _db.Notifications.AddAsync(new Notification
                {
                    UserId = us.UserId,
                    ActorUserId = endorserId.Value,
                    Type = "SkillEndorsed",
                    Payload = $"{{\"userSkillId\":{userSkillId}}}",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                return Ok(new { Status = "Endorsed" });
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Remove(int id)
        {
            var uid = GetUserId();
            if (uid == null) return Unauthorized();

            var us = await _db.UserSkills.FindAsync(id);
            if (us == null) return NotFound();
            if (us.UserId != uid && !User.IsInRole("0")) return Forbid();

            _db.UserSkills.Remove(us);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("validation-request")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> SubmitValidationRequest(
            [FromForm] int? skillId,
            [FromForm] string? requestedSkillName,
            [FromForm] string? notes,
            IFormFile? document)
        {
            var uid = GetUserId();
            if (uid == null) return Unauthorized();

            if (skillId == null && string.IsNullOrWhiteSpace(requestedSkillName))
                return BadRequest("Fornece um skillId existente ou um nome de nova skill.");

            string? docUrl = null;
            if (document != null && document.Length > 0)
            {
                if (!_fileStorage.ValidateDocumentOrImageFile(document, out var err))
                    return BadRequest(err);
                docUrl = await _fileStorage.SaveFileAsync(document, "skill-validations");
            }

            var req = new SkillValidationRequest
            {
                UserId = uid.Value,
                SkillId = skillId,
                RequestedSkillName = string.IsNullOrWhiteSpace(requestedSkillName) ? null : requestedSkillName.Trim(),
                DocumentUrl = docUrl,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                Status = 0,
                CreatedAt = DateTime.UtcNow
            };

            await _db.SkillValidationRequests.AddAsync(req);
            await _db.SaveChangesAsync();

            var adminIds = await _db.Users.Where(u => u.Role == 0).Select(u => u.UserId).ToListAsync();
            var now = DateTime.UtcNow;
            _db.Notifications.AddRange(adminIds.Select(aid => new Notification
            {
                UserId = aid,
                ActorUserId = uid.Value,
                Type = "SkillValidationRequest",
                Payload = $"{{\"requestId\":{req.RequestId}}}",
                IsRead = false,
                CreatedAt = now
            }));
            await _db.SaveChangesAsync();

            return Ok(new { requestId = req.RequestId, message = "Pedido de validação submetido." });
        }

        [HttpGet("validation-requests/my")]
        public async Task<IActionResult> GetMyValidationRequests()
        {
            var uid = GetUserId();
            if (uid == null) return Unauthorized();

            var reqs = await _db.SkillValidationRequests
                .Where(r => r.UserId == uid.Value)
                .Include(r => r.Skill)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    r.RequestId,
                    r.SkillId,
                    SkillName = r.Skill != null ? r.Skill.Name : r.RequestedSkillName,
                    r.RequestedSkillName,
                    r.DocumentUrl,
                    r.Notes,
                    r.Status,
                    r.CreatedAt,
                    r.ReviewedAt
                })
                .ToListAsync();

            return Ok(reqs);
        }

        [HttpGet("validation-requests")]
        [Authorize(Roles = "0")]
        public async Task<IActionResult> GetAllValidationRequests([FromQuery] int? status = null)
        {
            var query = _db.SkillValidationRequests
                .Include(r => r.User)
                .Include(r => r.Skill)
                .AsQueryable();

            if (status.HasValue)
                query = query.Where(r => r.Status == status.Value);

            var reqs = await query.OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    r.RequestId,
                    r.UserId,
                    UserName = r.User.Name,
                    r.SkillId,
                    SkillName = r.Skill != null ? r.Skill.Name : r.RequestedSkillName,
                    r.RequestedSkillName,
                    r.DocumentUrl,
                    r.Notes,
                    r.Status,
                    r.CreatedAt,
                    r.ReviewedAt
                })
                .ToListAsync();

            return Ok(reqs);
        }

        [HttpPost("validation-requests/{requestId}/review")]
        [Authorize(Roles = "0")]
        public async Task<IActionResult> ReviewValidationRequest(int requestId, [FromBody] ReviewSkillRequestDto dto)
        {
            var adminId = GetUserId();
            var req = await _db.SkillValidationRequests
                .Include(r => r.Skill)
                .FirstOrDefaultAsync(r => r.RequestId == requestId);
            if (req == null) return NotFound();

            req.Status = dto.Approve ? 1 : 2;
            req.ReviewedAt = DateTime.UtcNow;
            req.ReviewedByUserId = adminId;

            if (dto.Approve)
            {
                int targetSkillId;
                if (req.SkillId.HasValue)
                {
                    targetSkillId = req.SkillId.Value;
                }
                else if (!string.IsNullOrWhiteSpace(req.RequestedSkillName))
                {
                    var existing = await _db.Skills.FirstOrDefaultAsync(s => s.Name == req.RequestedSkillName);
                    if (existing != null)
                    {
                        targetSkillId = existing.SkillId;
                    }
                    else
                    {
                        var newSkill = new Skill { Name = req.RequestedSkillName };
                        _db.Skills.Add(newSkill);
                        await _db.SaveChangesAsync();
                        targetSkillId = newSkill.SkillId;
                        req.SkillId = targetSkillId;
                    }
                }
                else
                {
                    return BadRequest("Não há skill válida para associar.");
                }

                var alreadyHas = await _db.UserSkills.AnyAsync(us => us.UserId == req.UserId && us.SkillId == targetSkillId);
                if (!alreadyHas)
                {
                    _db.UserSkills.Add(new UserSkill
                    {
                        UserId = req.UserId,
                        SkillId = targetSkillId,
                        EndorsementCount = 0,
                        AddedAt = DateTime.UtcNow
                    });
                }
            }

            await _db.Notifications.AddAsync(new Notification
            {
                UserId = req.UserId,
                ActorUserId = adminId,
                Type = dto.Approve ? "SkillValidationApproved" : "SkillValidationRejected",
                Payload = $"{{\"requestId\":{requestId}}}",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            return Ok(new { status = req.Status, message = dto.Approve ? "Skill aprovada e adicionada ao perfil." : "Pedido rejeitado." });
        }

        private int? GetUserId()
        {
            var sid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(sid, out var id) ? id : (int?)null;
        }
    }

    public class ReviewSkillRequestDto
    {
        public bool Approve { get; set; }
        public string? Notes { get; set; }
    }

    public class AddUserSkillDto
    {
        public int UserId { get; set; }
        public int SkillId { get; set; }
    }

    public class UserSkillDto
    {
        public int UserSkillId { get; set; }
        public int UserId { get; set; }
        public int SkillId { get; set; }
        public SkillDto? Skill { get; set; }
        public int EndorsementCount { get; set; }
        public DateTime AddedAt { get; set; }
    }

    public class SkillDto
    {
        public int SkillId { get; set; }
        public string? Name { get; set; }
    }
}