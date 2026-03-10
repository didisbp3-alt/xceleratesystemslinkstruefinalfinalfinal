using APIPSI16.Data;
using APIPSI16.Filters;
using APIPSI16.Models;
using APIPSI16.DTOs;
using APIPSI16.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace APIPSI16.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CompaniesController : ApiControllerBase
    {
        private readonly xcleratesystemslinks_SampleDBContext _context;
        private readonly IFileStorageService _fileStorage;

        public CompaniesController(
            xcleratesystemslinks_SampleDBContext context,
            IFileStorageService fileStorage,
            ISessionService sessionService)
            : base(sessionService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _fileStorage = fileStorage ?? throw new ArgumentNullException(nameof(fileStorage));
        }

        // GET: api/Companies
        [HttpGet]
        public async Task<IActionResult> GetCompanies()
        {
            var validationResult = await ValidateSessionAsync();
            if (validationResult != null) return validationResult;

            return Ok(await _context.Companies.ToListAsync());
        }

        // GET: api/Companies/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCompany(int id)
        {
            var validationResult = await ValidateSessionAsync();
            if (validationResult != null) return validationResult;

            var company = await _context.Companies.FindAsync(id);
            if (company == null) return NotFound();
            return Ok(company);
        }

        // GET: api/Companies/5/profile
        [HttpGet("{id}/profile")]
        public async Task<IActionResult> GetCompanyProfile(int id)
        {
            var validationResult = await ValidateSessionAsync();
            if (validationResult != null) return validationResult;

            var company = await _context.Companies.FindAsync(id);
            if (company == null) return NotFound();

            var members = await _context.CompanyMembers
                .Where(cm => cm.CompanyId == id)
                .Include(cm => cm.User)
                .Select(cm => new CompanyMemberDTO
                {
                    UserId = cm.UserId,
                    UserName = cm.User.Name,
                    Role = cm.Role.ToString(),
                    JoinedAt = cm.StartDate.HasValue ? new DateTime(cm.StartDate.Value.Year, cm.StartDate.Value.Month, cm.StartDate.Value.Day) : (DateTime?)null
                })
                .ToListAsync();

            var opportunities = await _context.Opportunities
                .Where(o => o.CompanyId == id)
                .Select(o => new OpportunityDTO
                {
                    Id = o.Id,
                    Title = o.Title,
                    Location = o.Location,
                    EmploymentType = o.EmploymentType,
                    SeniorityLevel = o.SeniorityLevel,
                    RemoteOption = o.RemoteOption,
                    CompanyId = o.CompanyId,
                    RequiredJobRoleIds = o.RequiredJobRoleIds
                })
                .ToListAsync();

            var profileDto = new CompanyProfileDTO
            {
                CompanyId = company.CompanyId,
                Name = company.Name,
                Industry = company.Industry,
                Location = company.Location,
                CompanyLogoUrl = company.CompanyLogoUrl,
                CreatedAt = company.CreatedAt,
                Opportunities = opportunities,
                Members = await _context.CompanyMembers
                    .Where(cm => cm.CompanyId == id)
                    .Include(cm => cm.User)
                    .Select(cm => new CompanyMemberSummaryDTO
                    {
                        UserId = cm.UserId,
                        UserName = cm.User.Name,
                        Role = cm.Role,
                        Title = cm.Title
                    })
                    .ToListAsync()
            };

            return Ok(profileDto);
        }

        // POST: api/Companies/5/upload-logo
        [HttpPost("{id}/upload-logo")]
        [Authorize(Roles = "0,2")]
        [SwaggerFileUpload]
        public async Task<IActionResult> UploadCompanyLogo(int id, IFormFile file)
        {
            var validationResult = await ValidateSessionAsync();
            if (validationResult != null) return validationResult;

            var currentUserId = GetCurrentUserId();
            var userRole = GetCurrentUserRole();

            var company = await _context.Companies.FindAsync(id);
            if (company == null) return NotFound();

            if (userRole == "2")
            {
                if (!currentUserId.HasValue) return Unauthorized();
                var actorMember = await _context.CompanyMembers
                    .FirstOrDefaultAsync(cm => cm.CompanyId == id && cm.UserId == currentUserId.Value);
                if (actorMember == null || actorMember.Role < 3)
                    return StatusCode(403, "Apenas admins de empresa podem alterar o logótipo da empresa.");
            }

            if (!_fileStorage.ValidateImageFile(file, out var errorMessage))
                return BadRequest(new { message = errorMessage });

            try
            {
                if (!string.IsNullOrEmpty(company.CompanyLogoUrl))
                {
                    await _fileStorage.DeleteFileAsync(company.CompanyLogoUrl);
                }

                var fileUrl = await _fileStorage.SaveFileAsync(file, "companies");
                company.CompanyLogoUrl = fileUrl;
                await _context.SaveChangesAsync();

                return Ok(new { success = true, fileUrl = fileUrl, message = "Company logo uploaded successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Error uploading file: {ex.Message}" });
            }
        }

        // POST: api/Companies/5/members
        [HttpPost("{id}/members")]
        [Authorize(Roles = "0,2")]
        public async Task<IActionResult> InviteMember(int id, [FromBody] CompanyMember member)
        {
            var validationResult = await ValidateSessionAsync();
            if (validationResult != null) return validationResult;

            var currentUserId = GetCurrentUserId();
            var userRole = GetCurrentUserRole();

            if (userRole == "2")
            {
                if (!currentUserId.HasValue) return Unauthorized();
                var actorMember = await _context.CompanyMembers
                    .FirstOrDefaultAsync(cm => cm.CompanyId == id && cm.UserId == currentUserId.Value);
                if (actorMember == null) return Forbid();
                if (actorMember.Role < 3)
                    return StatusCode(403, "Apenas admins de empresa podem convidar membros.");
            }

            member.CompanyId = id;
            _context.CompanyMembers.Add(member);
            await _context.SaveChangesAsync();

            return Ok(member);
        }

        // DELETE: api/Companies/5/members/userId
        [HttpDelete("{id}/members/{userId}")]
        [Authorize(Roles = "0,2")]
        public async Task<IActionResult> RemoveMember(int id, int userId)
        {
            var validationResult = await ValidateSessionAsync();
            if (validationResult != null) return validationResult;

            var currentUserId = GetCurrentUserId();
            var userRole = GetCurrentUserRole();

            if (userRole == "2")
            {
                if (!currentUserId.HasValue) return Unauthorized();
                // Must be CompanyAdmin (Role=3) to remove members
                var actorMember = await _context.CompanyMembers
                    .FirstOrDefaultAsync(cm => cm.CompanyId == id && cm.UserId == currentUserId.Value);
                if (actorMember == null) return Forbid();
                if (actorMember.Role < 3 && currentUserId.Value != userId) // Allow self-removal at any role
                    return StatusCode(403, "Apenas admins de empresa podem remover membros.");
            }

            var member = await _context.CompanyMembers
                .FirstOrDefaultAsync(cm => cm.CompanyId == id && cm.UserId == userId);

            if (member == null) return NotFound();

            _context.CompanyMembers.Remove(member);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // POST: api/Companies
        [HttpPost]
        [Authorize(Roles = "0,2")]
        public async Task<IActionResult> CreateCompany([FromBody] Company company)
        {
            var validationResult = await ValidateSessionAsync();
            if (validationResult != null) return validationResult;

            if (!ModelState.IsValid) return BadRequest(ModelState);

            _context.Companies.Add(company);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCompany), new { id = company.CompanyId }, company);
        }

        // PUT: api/Companies/5
        [HttpPut("{id}")]
        [Authorize(Roles = "0,2")]
        public async Task<IActionResult> UpdateCompany(int id, [FromBody] Company company)
        {
            var validationResult = await ValidateSessionAsync();
            if (validationResult != null) return validationResult;

            if (id != company.CompanyId) return BadRequest();

            var currentUserId = GetCurrentUserId();
            var userRole = GetCurrentUserRole();

            if (userRole == "2")
            {
                if (!currentUserId.HasValue) return Unauthorized();

                var actorMember = await _context.CompanyMembers
                    .FirstOrDefaultAsync(cm => cm.CompanyId == id && cm.UserId == currentUserId.Value);

                if (actorMember == null || actorMember.Role < 3)
                    return StatusCode(403, "Apenas admins de empresa podem atualizar os dados da empresa.");
            }

            _context.Entry(company).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CompanyExists(id)) return NotFound();
                throw;
            }

            return NoContent();
        }

        // DELETE: api/Companies/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "0")]
        public async Task<IActionResult> DeleteCompany(int id)
        {
            var validationResult = await ValidateSessionAsync();
            if (validationResult != null) return validationResult;

            var company = await _context.Companies.FindAsync(id);
            if (company == null) return NotFound();

            _context.Companies.Remove(company);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool CompanyExists(int id)
        {
            return _context.Companies.Any(c => c.CompanyId == id);
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