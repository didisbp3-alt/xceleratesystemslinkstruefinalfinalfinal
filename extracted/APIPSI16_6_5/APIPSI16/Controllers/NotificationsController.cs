using System.Linq;
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
    public class NotificationsController : ControllerBase
    {
        private readonly xcleratesystemslinks_SampleDBContext _db;
        public NotificationsController(xcleratesystemslinks_SampleDBContext db) => _db = db;

        [HttpGet("my")]
        public async Task<IActionResult> MyNotifications()
        {
            var uid = GetUserId();
            if (uid == null) return Unauthorized();
            var list = await _db.Notifications.Where(n => n.UserId == uid).OrderByDescending(n => n.CreatedAt).Take(100).ToListAsync();
            return Ok(list);
        }

        [HttpPost("{id}/markread")]
        public async Task<IActionResult> MarkRead(int id)
        {
            var uid = GetUserId();
            if (uid == null) return Unauthorized();

            var n = await _db.Notifications.FindAsync(id);
            if (n == null) return NotFound();
            if (n.UserId != uid) return Forbid();

            n.IsRead = true;
            _db.Notifications.Update(n);
            await _db.SaveChangesAsync();
            return Ok();
        }

        private int? GetUserId()
        {
            var sid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(sid, out var id) ? id : (int?)null;
        }
    }
}