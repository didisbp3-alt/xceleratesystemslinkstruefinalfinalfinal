using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using APIPSI16.Data;
using APIPSI16.Models;

namespace APIPSI16.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SkillsController : ControllerBase
    {
        private readonly xcleratesystemslinks_SampleDBContext _db;
        public SkillsController(xcleratesystemslinks_SampleDBContext db) => _db = db;

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll() => Ok(await _db.Skills.OrderBy(s => s.Name).ToListAsync());

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> Get(int id)
        {
            var skill = await _db.Skills.FindAsync(id);
            if (skill == null) return NotFound();
            return Ok(skill);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Skill skill)
        {
            if (string.IsNullOrWhiteSpace(skill.Name)) return BadRequest("Name required");
            _db.Skills.Add(skill);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = skill.SkillId }, skill);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Skill updated)
        {
            if (id != updated.SkillId) return BadRequest();
            _db.Entry(updated).State = EntityState.Modified;
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var skill = await _db.Skills.FindAsync(id);
            if (skill == null) return NotFound();
            _db.Skills.Remove(skill);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}