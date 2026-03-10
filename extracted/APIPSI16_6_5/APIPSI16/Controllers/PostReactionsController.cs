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
    public class PostReactionsController : ControllerBase
    {
        private readonly xcleratesystemslinks_SampleDBContext _db;
        public PostReactionsController(xcleratesystemslinks_SampleDBContext db) => _db = db;

        [HttpPost]
        public async Task<IActionResult> React([FromBody] PostReaction dto)
        {
            var uid = GetUserId();
            if (uid == null) return Unauthorized();
            dto.UserId = uid.Value;
            dto.CreatedAt = DateTime.UtcNow;

            if (await _db.PostReactions.AnyAsync(r => r.PostId == dto.PostId && r.UserId == dto.UserId && r.ReactionType == dto.ReactionType))
                return Conflict("Already reacted with this type.");

            await _db.PostReactions.AddAsync(dto);
            await _db.SaveChangesAsync();

            var post = await _db.Posts.FindAsync(dto.PostId);
            if (post != null && post.UserId != uid)
            {
                await _db.Notifications.AddAsync(new Notification
                {
                    UserId = post.UserId,
                    ActorUserId = uid.Value,
                    Type = "PostReaction",
                    Payload = $"{{\"postId\":{post.PostId},\"reactionId\":{dto.ReactionId}}}",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
            }

            return CreatedAtAction(nameof(Get), new { id = dto.ReactionId }, dto);
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> Get(int id)
        {
            var r = await _db.PostReactions.FindAsync(id);
            if (r == null) return NotFound();
            return Ok(r);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var uid = GetUserId();
            if (uid == null) return Unauthorized();

            var r = await _db.PostReactions.FindAsync(id);
            if (r == null) return NotFound();
            if (r.UserId != uid && !User.IsInRole("0")) return Forbid();
            _db.PostReactions.Remove(r);
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