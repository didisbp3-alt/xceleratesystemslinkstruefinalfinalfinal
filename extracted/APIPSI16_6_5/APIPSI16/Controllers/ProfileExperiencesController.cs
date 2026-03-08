using System;
using System.Threading.Tasks;
using System.Security.Claims;
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
    public class ProfileExperiencesController : ControllerBase
    {
        private readonly xcleratesystemslinks_SampleDBContext _db;
        public ProfileExperiencesController(xcleratesystemslinks_SampleDBContext db) => _db = db;

        [HttpGet("user/{userId}")]
        [AllowAnonymous]
        public async Task<IActionResult> ForUser(int userId) => Ok(await _db.ProfileExperiences.Where(p => p.UserId == userId).OrderByDescending(p => p.StartDate).ToListAsync());

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] ProfileExperience exp)
        {
            var uid = GetUserId();
            if (uid == null) return Unauthorized();
            if (exp.UserId != uid && !User.IsInRole("0")) return Forbid();
            await _db.ProfileExperiences.AddAsync(exp);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(ForUser), new { userId = exp.UserId }, exp);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ProfileExperience updated)
        {
            var uid = GetUserId();
            if (uid == null) return Unauthorized();
            var existing = await _db.ProfileExperiences.FindAsync(id);
            if (existing == null) return NotFound();
            if (existing.UserId != uid && !User.IsInRole("0")) return Forbid();
            existing.Title = updated.Title;
            existing.CompanyName = updated.CompanyName;
            existing.Location = updated.Location;
            existing.StartDate = updated.StartDate;
            existing.EndDate = updated.EndDate;
            existing.IsCurrent = updated.IsCurrent;
            existing.Description = updated.Description;
            await _db.SaveChangesAsync();
            return Ok(existing);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var uid = GetUserId();
            if (uid == null) return Unauthorized();
            var e = await _db.ProfileExperiences.FindAsync(id);
            if (e == null) return NotFound();
            if (e.UserId != uid && !User.IsInRole("0")) return Forbid();
            _db.ProfileExperiences.Remove(e);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        private int? GetUserId()
        {
            var sid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(sid, out var id) ? id : (int?)null;
        }
    }
}
