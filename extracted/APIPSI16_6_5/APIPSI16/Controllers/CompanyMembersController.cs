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
    public class CompanyMembersController : ControllerBase
    {
        private readonly xcleratesystemslinks_SampleDBContext _context;

        public CompanyMembersController(xcleratesystemslinks_SampleDBContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        // GET: api/CompanyMembers
        // All authenticated users can view company members
        [HttpGet]
        public async Task<IActionResult> GetMembers()
        {
            var members = await _context.CompanyMembers
                .Include(cm => cm.Company)
                .Include(cm => cm.User)
                .ToListAsync();
            return Ok(members);
        }

        // GET: api/CompanyMembers/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetMember(int id)
        {
            var member = await _context.CompanyMembers
                .Include(cm => cm.Company)
                .Include(cm => cm.User)
                .FirstOrDefaultAsync(m => m.CompanyMemberId == id);

            if (member == null) return NotFound();

            return Ok(member);
        }

        // POST: api/CompanyMembers
        // Admins and employers can add members
        [HttpPost]
        [Authorize(Roles = "0,2")] // Admin or Employer
        public async Task<IActionResult> AddMember([FromBody] CompanyMember member)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var currentUserId = GetCurrentUserId();
            var userRole = GetCurrentUserRole();

            // Employers can only add members to their own company
            if (userRole == "2")
            {
                if (!currentUserId.HasValue) return Unauthorized();

                var isMemberOfCompany = await _context.CompanyMembers
                    .AnyAsync(cm => cm.CompanyId == member.CompanyId && cm.UserId == currentUserId.Value);

                if (!isMemberOfCompany)
                    return StatusCode(403, "You can only add members to companies you're a member of.");
            }

            _context.CompanyMembers.Add(member);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetMember), new { id = member.CompanyMemberId }, member);
        }

        // DELETE: api/CompanyMembers/5
        // Admins and employers can remove members
        [HttpDelete("{id}")]
        [Authorize(Roles = "0,2")] // Admin or Employer
        public async Task<IActionResult> RemoveMember(int id)
        {
            var member = await _context.CompanyMembers.FindAsync(id);
            if (member == null) return NotFound();

            var currentUserId = GetCurrentUserId();
            var userRole = GetCurrentUserRole();

            // Employers can only remove members from their own company
            if (userRole == "2")
            {
                if (!currentUserId.HasValue) return Unauthorized();

                var isMemberOfCompany = await _context.CompanyMembers
                    .AnyAsync(cm => cm.CompanyId == member.CompanyId && cm.UserId == currentUserId.Value);

                if (!isMemberOfCompany)
                    return StatusCode(403, "You can only remove members from companies you're a member of.");
            }

            _context.CompanyMembers.Remove(member);
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