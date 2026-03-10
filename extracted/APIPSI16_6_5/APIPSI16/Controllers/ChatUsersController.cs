using APIPSI16.Data;
using APIPSI16.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace APIPSI16.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Require JWT for all actions
    public class ChatUsersController : ControllerBase
    {
        private readonly xcleratesystemslinks_SampleDBContext _context;

        public ChatUsersController(xcleratesystemslinks_SampleDBContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        // GET: api/ChatUsers
        // Admins see all; users see only their own chat memberships
        [HttpGet]
        public async Task<IActionResult> GetChatUsers()
        {
            var currentUserId = GetCurrentUserId();
            var userRole = GetCurrentUserRole();

            IQueryable<ChatUser> query = _context.ChatUsers
                .Include(cu => cu.Chat)
                .Include(cu => cu.User);

            // Non-admins see only their own memberships
            if (userRole != "0")
            {
                if (!currentUserId.HasValue) return Unauthorized();
                query = query.Where(cu => cu.UserId == currentUserId.Value);
            }

            var chatUsers = await query.ToListAsync();
            return Ok(chatUsers);
        }

        // GET: api/ChatUsers/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetChatUser(int id)
        {
            var chatUser = await _context.ChatUsers
                .Include(cu => cu.Chat)
                .Include(cu => cu.User)
                .FirstOrDefaultAsync(cu => cu.ChatUserId == id);

            if (chatUser == null) return NotFound();

            var currentUserId = GetCurrentUserId();
            var userRole = GetCurrentUserRole();

            // Enforce ownership
            if (userRole != "0" && chatUser.UserId != currentUserId)
                return Forbid();

            return Ok(chatUser);
        }

        // POST: api/ChatUsers
        // Users can add themselves to chats; admins can add anyone
        [HttpPost]
        public async Task<IActionResult> AddUserToChat([FromBody] ChatUser chatUser)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var currentUserId = GetCurrentUserId();
            var userRole = GetCurrentUserRole();

            // Regular users can only add themselves
            if (userRole != "0")
            {
                if (!currentUserId.HasValue) return Unauthorized();
                if (chatUser.UserId != currentUserId.Value)
                    return StatusCode(403, "You can only add yourself to chats.");
            }

            _context.Add(chatUser);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetChatUser), new { id = chatUser.ChatUserId }, chatUser);
        }

        // DELETE: api/ChatUsers/5
        // Users can remove themselves; admins can remove anyone
        [HttpDelete("{id}")]
        public async Task<IActionResult> RemoveUserFromChat(int id)
        {
            var chatUser = await _context.ChatUsers.FindAsync(id);
            if (chatUser == null) return NotFound();

            var currentUserId = GetCurrentUserId();
            var userRole = GetCurrentUserRole();

            // Regular users can only remove themselves
            if (userRole != "0" && chatUser.UserId != currentUserId)
                return StatusCode(403, "You can only remove yourself from chats.");

            _context.ChatUsers.Remove(chatUser);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private int? GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
        }

        private string? GetCurrentUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value;
        }
    }
}