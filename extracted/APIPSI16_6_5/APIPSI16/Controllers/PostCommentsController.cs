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
    public class PostCommentsController : ControllerBase
    {
        private readonly xcleratesystemslinks_SampleDBContext _db;
        public PostCommentsController(xcleratesystemslinks_SampleDBContext db) => _db = db;

        [HttpPost]
        public async Task<IActionResult> AddComment([FromBody] PostComment dto)
        {
            var uid = GetUserId();
            if (uid == null) return Unauthorized();

            if (dto.ParentCommentId.HasValue)
            {
                var parent = await _db.PostComments.FindAsync(dto.ParentCommentId.Value);
                if (parent == null) return BadRequest("Parent comment not found.");
                if (parent.PostId != dto.PostId) return BadRequest("Parent comment belongs to different post.");
            }

            dto.UserId = uid.Value;
            dto.CreatedAt = DateTime.UtcNow;
            dto.IsModerated = false;
            dto.IsDeleted = false;
            await _db.PostComments.AddAsync(dto);
            await _db.SaveChangesAsync();

            var post = await _db.Posts.FindAsync(dto.PostId);
            if (post != null && post.UserId != uid)
            {
                await _db.Notifications.AddAsync(new Notification
                {
                    UserId = post.UserId,
                    ActorUserId = uid.Value,
                    Type = "PostComment",
                    Payload = $"{{\"postId\":{post.PostId},\"commentId\":{dto.CommentId}}}",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
            }

            return CreatedAtAction(nameof(Get), new { id = dto.CommentId }, dto);
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> Get(int id)
        {
            var c = await _db.PostComments.FindAsync(id);
            if (c == null) return NotFound();
            return Ok(c);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var uid = GetUserId();
            if (uid == null) return Unauthorized();

            var c = await _db.PostComments.FindAsync(id);
            if (c == null) return NotFound();
            if (c.UserId != uid && !User.IsInRole("0")) return Forbid();
            
            // Soft delete
            c.IsDeleted = true;
            _db.PostComments.Update(c);
            await _db.SaveChangesAsync();
            
            return NoContent();
        }

        // POST: api/PostComments/5/moderate
        // Moderate a comment (approve/disapprove)
        [HttpPost("{id}/moderate")]
        [Authorize(Roles = "0,2")]
        public async Task<IActionResult> ModerateComment(int id, [FromBody] ModerateCommentDTO dto)
        {
            var uid = GetUserId();
            if (uid == null) return Unauthorized();

            var comment = await _db.PostComments.FindAsync(id);
            if (comment == null) return NotFound();

            comment.IsModerated = true;
            comment.ModeratedBy = uid.Value;
            comment.ModeratedAt = DateTime.UtcNow;
            
            if (!dto.Approve)
            {
                comment.IsDeleted = true;
            }

            _db.PostComments.Update(comment);

            // Create audit log
            await _db.AuditLogs.AddAsync(new AuditLog
            {
                UserId = uid.Value,
                Action = dto.Approve ? "ApproveComment" : "DisapproveComment",
                TargetType = "PostComment",
                TargetId = id,
                Metadata = dto.Reason,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            return Ok(comment);
        }

        // GET: api/PostComments/moderation/pending
        // Get pending comments for moderation
        [HttpGet("moderation/pending")]
        [Authorize(Roles = "0,2")]
        public async Task<IActionResult> GetPendingModeration([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var comments = await _db.PostComments
                .Where(c => !c.IsModerated && !c.IsDeleted)
                .Include(c => c.User)
                .Include(c => c.Post)
                .OrderBy(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new
                {
                    c.CommentId,
                    c.PostId,
                    PostTitle = c.Post.Content != null ? c.Post.Content.Substring(0, Math.Min(50, c.Post.Content.Length)) : "",
                    c.UserId,
                    UserName = c.User.Name,
                    c.Content,
                    c.CreatedAt
                })
                .ToListAsync();

            return Ok(comments);
        }

        private int? GetUserId()
        {
            var sid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(sid, out var id) ? id : (int?)null;
        }

        public class ModerateCommentDTO
        {
            public bool Approve { get; set; }
            public string? Reason { get; set; }
        }
    }
}