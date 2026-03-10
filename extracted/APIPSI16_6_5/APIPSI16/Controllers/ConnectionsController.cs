using APIPSI16.Data;
using APIPSI16.Models;
using APIPSI16.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace APIPSI16.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ConnectionsController : ControllerBase
    {
        private readonly xcleratesystemslinks_SampleDBContext _db;
        public ConnectionsController(xcleratesystemslinks_SampleDBContext db) => _db = db;

        // POST: api/connections/request
        [HttpPost("request")]
        public async Task<IActionResult> CreateRequest([FromBody] CreateConnectionRequestDto dto)
        {
            var requesterId = GetUserId();
            if (requesterId == null) return Unauthorized();

            if (requesterId == dto.AddresseeId) return BadRequest("Cannot connect to self.");

            var existing = await _db.Connections
                .FirstOrDefaultAsync(c => (c.RequesterUserId == requesterId && c.AddresseeUserId == dto.AddresseeId)
                                       || (c.RequesterUserId == dto.AddresseeId && c.AddresseeUserId == requesterId));

            if (existing != null) return Conflict(new { existing.ConnectionId, existing.Status });

            // Enforce Free plan connection limit (50 accepted connections)
            var user = await _db.Users.FindAsync(requesterId.Value);
            if (user != null && user.SubscriptionPlan == 0)
            {
                var connectionCount = await _db.Connections.CountAsync(c =>
                    (c.RequesterUserId == requesterId || c.AddresseeUserId == requesterId) && c.Status == 1);
                if (connectionCount >= 50)
                    return StatusCode(429, new { message = "Limite de ligações atingido para o plano Free (50). Faz upgrade para Pro para ligações ilimitadas.", limitReached = true, plan = "Free", limit = 50 });
            }

            var conn = new APIPSI16.Models.Connection
            {
                RequesterUserId = requesterId.Value,
                AddresseeUserId = dto.AddresseeId,
                Status = 0,
                CreatedAt = DateTime.UtcNow
            };

            await _db.Connections.AddAsync(conn);
            await _db.SaveChangesAsync();

            await _db.Notifications.AddAsync(new Notification
            {
                UserId = dto.AddresseeId,
                ActorUserId = requesterId.Value,
                Type = "ConnectionRequest",
                Payload = $"{{\"connectionId\":{conn.ConnectionId}}}",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { id = conn.ConnectionId }, conn);
        }

        // POST: api/connections/{id}/accept
        [HttpPost("{id}/accept")]
        public async Task<IActionResult> Accept(int id)
        {
            var actorId = GetUserId();
            if (actorId == null) return Unauthorized();

            var conn = await _db.Connections.FindAsync(id);
            if (conn == null) return NotFound();

            if (conn.AddresseeUserId != actorId && conn.RequesterUserId != actorId)
                return Forbid();

            conn.Status = 1;
            conn.AcceptedAt = DateTime.UtcNow;
            _db.Connections.Update(conn);
            await _db.SaveChangesAsync();

            var recipient = conn.RequesterUserId == actorId ? conn.AddresseeUserId : conn.RequesterUserId;
            await _db.Notifications.AddAsync(new Notification
            {
                UserId = recipient,
                ActorUserId = actorId.Value,
                Type = "ConnectionAccepted",
                Payload = $"{{\"connectionId\":{conn.ConnectionId}}}",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            return Ok(conn);
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> Get(int id)
        {
            var conn = await _db.Connections.FindAsync(id);
            if (conn == null) return NotFound();
            return Ok(conn);
        }

        [HttpGet("my")]
        public async Task<IActionResult> MyConnections()
        {
            var uid = GetUserId();
            if (uid == null) return Unauthorized();

            var list = await _db.Connections
                .Where(c => (c.RequesterUserId == uid || c.AddresseeUserId == uid) && c.Status == 1)
                .ToListAsync();

            return Ok(list);
        }

        // GET: api/connections/my-with-users – accepted connections with user details (single JOIN query)
        [HttpGet("my-with-users")]
        public async Task<IActionResult> MyConnectionsWithUsers()
        {
            var uid = GetUserId();
            if (uid == null) return Unauthorized();

            // Single query with joins to avoid N+1
            var connections = await _db.Connections
                .Where(c => (c.RequesterUserId == uid || c.AddresseeUserId == uid) && c.Status == 1)
                .Join(_db.Users, c => c.RequesterUserId == uid ? c.AddresseeUserId : c.RequesterUserId,
                    u => u.UserId, (c, u) => new
                    {
                        c.ConnectionId,
                        c.RequesterUserId,
                        c.AddresseeUserId,
                        c.Status,
                        c.CreatedAt,
                        c.AcceptedAt,
                        OtherUser = new { u.UserId, u.Name, u.ProfileBio, u.ProfilePictureUrl, u.Role }
                    })
                .ToListAsync();

            return Ok(connections);
        }

        // GET: api/connections/pending – pending incoming requests for me (single JOIN query)
        [HttpGet("pending")]
        public async Task<IActionResult> PendingRequests()
        {
            var uid = GetUserId();
            if (uid == null) return Unauthorized();

            var pending = await _db.Connections
                .Where(c => c.AddresseeUserId == uid && c.Status == 0)
                .Join(_db.Users, c => c.RequesterUserId, u => u.UserId, (c, u) => new
                {
                    c.ConnectionId,
                    c.RequesterUserId,
                    c.AddresseeUserId,
                    c.Status,
                    c.CreatedAt,
                    RequesterUser = new { u.UserId, u.Name, u.ProfileBio, u.ProfilePictureUrl, u.Role }
                })
                .ToListAsync();

            return Ok(pending);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var uid = GetUserId();
            if (uid == null) return Unauthorized();

            var conn = await _db.Connections.FindAsync(id);
            if (conn == null) return NotFound();

            if (conn.RequesterUserId != uid && conn.AddresseeUserId != uid)
                return Forbid();

            _db.Connections.Remove(conn);
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