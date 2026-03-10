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
    public class ProfileEducationsController : ControllerBase
    {
        private readonly xcleratesystemslinks_SampleDBContext _db;
        public ProfileEducationsController(xcleratesystemslinks_SampleDBContext db) => _db = db;

        [HttpGet("user/{userId}")]
        [AllowAnonymous]
        public async Task<IActionResult> ForUser(int userId) => Ok(await _db.ProfileEducations.Where(p => p.UserId == userId).OrderByDescending(p => p.StartYear).ToListAsync());

        [HttpPost]
        public async Task<IActionResult> Add(ProfileEducation ed)
        {
            var uid = GetUserId();
            if (uid == null) return Unauthorized();
            if (ed.UserId != uid && !User.IsInRole("0")) return Forbid();
            await _db.ProfileEducations.AddAsync(ed);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(ForUser), new { userId = ed.UserId }, ed);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var uid = GetUserId();
            if (uid == null) return Unauthorized();
            var e = await _db.ProfileEducations.FindAsync(id);
            if (e == null) return NotFound();
            if (e.UserId != uid && !User.IsInRole("0")) return Forbid();
            _db.ProfileEducations.Remove(e);
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