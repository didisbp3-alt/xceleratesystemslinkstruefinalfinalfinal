using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using APIPSI16.Data;
using APIPSI16.Models;
using System.Security.Claims;

namespace APIPSI16.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SkillEndorsementsController : ControllerBase
    {
        private readonly xcleratesystemslinks_SampleDBContext _db;

        public SkillEndorsementsController(xcleratesystemslinks_SampleDBContext db) => _db = db;

        // Get all skills for current user
        [HttpGet("my-skills")]
        public async Task<IActionResult> GetMySkills()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var skills = await _db.UserSkills
                .Where(us => us.UserId == userId)
                .Include(us => us.Skill)
                .Select(us => us.Skill)
                .ToListAsync();
            return Ok(skills);
        }

        // Add skill to current user
        [HttpPost("add-skill")]
        public async Task<IActionResult> AddSkill([FromBody] AddSkillDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.SkillName))
                return BadRequest("Skill name required");

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            // Find or create skill
            var skill = await _db.Skills
                .FirstOrDefaultAsync(s => s.Name == dto.SkillName);

            if (skill == null)
            {
                skill = new Skill { Name = dto.SkillName };
                _db.Skills.Add(skill);
                await _db.SaveChangesAsync();
            }

            // Check if user skill already exists
            var existing = await _db.UserSkills
                .FirstOrDefaultAsync(us => us.UserId == userId && us.SkillId == skill.SkillId);

            if (existing != null)
                return BadRequest("Skill already added");

            // Add user skill
            var userSkill = new UserSkill
            {
                UserId = userId,
                SkillId = skill.SkillId,
                AddedAt = System.DateTime.UtcNow,
                EndorsementCount = 0
            };

            _db.UserSkills.Add(userSkill);
            await _db.SaveChangesAsync();

            return Ok(skill);
        }

        // Remove skill from current user
        [HttpDelete("remove-skill/{skillId}")]
        public async Task<IActionResult> RemoveSkill(int skillId)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var userSkill = await _db.UserSkills
                .FirstOrDefaultAsync(us => us.UserId == userId && us.SkillId == skillId);

            if (userSkill == null) return NotFound();

            _db.UserSkills.Remove(userSkill);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}