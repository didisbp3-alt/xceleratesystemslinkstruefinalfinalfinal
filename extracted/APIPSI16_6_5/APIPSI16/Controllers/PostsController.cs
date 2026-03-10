using APIPSI16.Data;
using APIPSI16.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace APIPSI16.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PostsController : ControllerBase
    {
        private readonly xcleratesystemslinks_SampleDBContext _db;
        public PostsController(xcleratesystemslinks_SampleDBContext db) => _db = db;

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPost(int id)
        {
            var p = await _db.Posts.Include(x => x.PostComments).Include(x => x.PostReactions).FirstOrDefaultAsync(x => x.PostId == id);
            if (p == null) return NotFound();
            return Ok(p);
        }

        [HttpGet("feed")]
        public async Task<IActionResult> GetFeed([FromQuery] int pageSize = 20)
        {
            var uid = GetUserId();
            if (uid == null) return Unauthorized();

            var friends = await _db.Connections
                .Where(c => (c.RequesterUserId == uid || c.AddresseeUserId == uid) && c.Status == 1)
                .Select(c => c.RequesterUserId == uid ? c.AddresseeUserId : c.RequesterUserId)
                .ToListAsync();

            var q = _db.Posts
                .Where(p => p.Visibility == 0 || p.UserId == uid || (p.Visibility == 1 && friends.Contains(p.UserId)))
                .OrderByDescending(p => p.CreatedAt)
                .Take(pageSize);

            return Ok(await q.ToListAsync());
        }

        [HttpPost]
        public async Task<IActionResult> CreatePost([FromBody] Post post)
        {
            var uid = GetUserId();
            if (uid == null) return Unauthorized();

            post.UserId = uid.Value;
            post.CreatedAt = DateTime.UtcNow;
            await _db.Posts.AddAsync(post);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetPost), new { id = post.PostId }, post);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePost(int id, [FromBody] Post update)
        {
            var uid = GetUserId();
            if (uid == null) return Unauthorized();

            var post = await _db.Posts.FindAsync(id);
            if (post == null) return NotFound();
            if (post.UserId != uid) return Forbid();

            post.Content = update.Content;
            post.UpdatedAt = DateTime.UtcNow;
            _db.Posts.Update(post);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePost(int id)
        {
            var uid = GetUserId();
            if (uid == null) return Unauthorized();

            var post = await _db.Posts.FindAsync(id);
            if (post == null) return NotFound();
            if (post.UserId != uid && !User.IsInRole("0")) return Forbid();
            _db.Posts.Remove(post);
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