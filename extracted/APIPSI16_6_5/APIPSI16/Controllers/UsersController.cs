using APIPSI16.Data;
using APIPSI16.Filters;
using APIPSI16.Models;
using APIPSI16.Models.DTOs;
using APIPSI16.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace APIPSI16.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Require JWT for all actions
    public class UsersController : ControllerBase
    {
        private readonly xcleratesystemslinks_SampleDBContext _context;
        private readonly IFileStorageService _fileStorage;

        public UsersController(xcleratesystemslinks_SampleDBContext context, IFileStorageService fileStorage)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _fileStorage = fileStorage ?? throw new ArgumentNullException(nameof(fileStorage));
        }

        // GET: api/Users
        // - If no filters supplied: Admin (0) only -> returns all users
        // - If filters supplied (jobPreference and/or nationality): Admin (0) and Employer (2) can query
        [HttpGet]
        public async Task<IActionResult> GetUsers([FromQuery] int? jobPreference, [FromQuery] int? nationality)
        {
            var userRole = GetCurrentUserRole();

            if (jobPreference.HasValue || nationality.HasValue)
            {
                if (userRole != "0" && userRole != "2")
                    return Forbid();

                IQueryable<User> q = _context.Users;

                if (jobPreference.HasValue)
                    q = q.Where(u => u.JobPreference == jobPreference.Value);

                if (nationality.HasValue)
                    q = q.Where(u => u.Nationality == nationality.Value);

                var filtered = await q
                    .Select(u => new UserDTO
                    {
                        UserId = u.UserId,
                        Name = u.Name,
                        Email = u.Email,
                        Nationality = u.Nationality,
                        JobPreference = u.JobPreference,
                        ProfileBio = u.ProfileBio,
                        DoB = u.DoB,
                        PhoneNumber = u.PhoneNumber,
                        ProfilePictureUrl = u.ProfilePictureUrl,
                        LocationId = u.LocationId,
                        CountryId = u.CountryId,
                        LocationName = u.LocationNav != null ? u.LocationNav.Name : u.Location,
                        CountryName = u.CountryNav != null ? u.CountryNav.Name : null
                    })
                    .ToListAsync();

                return Ok(filtered);
            }

            if (userRole != "0")
                return Forbid();

            var users = await _context.Users
                .Select(u => new UserDTO
                {
                    UserId = u.UserId,
                    Name = u.Name,
                    Email = u.Email,
                    Nationality = u.Nationality,
                    JobPreference = u.JobPreference,
                    ProfileBio = u.ProfileBio,
                    DoB = u.DoB,
                    PhoneNumber = u.PhoneNumber,
                    ProfilePictureUrl = u.ProfilePictureUrl,
                    Location = u.Location,
                    LocationId = u.LocationId,
                    CountryId = u.CountryId,
                    LocationName = u.LocationNav != null ? u.LocationNav.Name : u.Location,
                    CountryName = u.CountryNav != null ? u.CountryNav.Name : null
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            var user = await _context.Users
                .Where(u => u.UserId == id)
                .Select(u => new UserDTO
                {
                    UserId = u.UserId,
                    Name = u.Name,
                    Email = u.Email,
                    Username = u.Username,
                    Nationality = u.Nationality,
                    JobPreference = u.JobPreference,
                    ProfileBio = u.ProfileBio,
                    DoB = u.DoB,
                    PhoneNumber = u.PhoneNumber,
                    Role = u.Role,
                    ProfilePictureUrl = u.ProfilePictureUrl,
                    BannerUrl = u.BannerUrl,
                    SubscriptionPlan = u.SubscriptionPlan,
                    Location = u.Location,
                    LocationId = u.LocationId,
                    CountryId = u.CountryId,
                    LocationName = u.LocationNav != null ? u.LocationNav.Name : u.Location,
                    CountryName = u.CountryNav != null ? u.CountryNav.Name : null
                })
                .FirstOrDefaultAsync();

            if (user == null) return NotFound();

            var currentUserId = GetCurrentUserId();
            var userRole = GetCurrentUserRole();

            if (userRole != "0" && currentUserId != id)
                return Forbid();

            return Ok(user);
        }

        // GET: api/Users/5/profile
        // Get complete user profile with skills, experiences, and educations
        [HttpGet("{id}/profile")]
        public async Task<IActionResult> GetUserProfile(int id)
        {
            var user = await _context.Users
                .Include(u => u.LocationNav)
                    .ThenInclude(l => l != null ? l.Country : null)
                .Include(u => u.CountryNav)
                .FirstOrDefaultAsync(u => u.UserId == id);
            if (user == null) return NotFound();

            var skills = await _context.UserSkills
                .Where(us => us.UserId == id)
                .Include(us => us.Skill)
                .Select(us => new SkillDTO
                {
                    SkillId = us.SkillId,
                    Name = us.Skill.Name,
                    EndorsementCount = _context.SkillEndorsements.Count(se => se.UserSkillId == us.UserSkillId)
                })
                .ToListAsync();

            var experiences = await _context.ProfileExperiences
                .Where(pe => pe.UserId == id)
                .Select(pe => new ProfileExperienceDTO
                {
                    ExperienceId = pe.ExperienceId,
                    JobTitle = pe.Title,
                    CompanyName = pe.CompanyName,
                    StartDate = pe.StartDate,
                    EndDate = pe.EndDate,
                    Description = pe.Description
                })
                .ToListAsync();

            var educations = await _context.ProfileEducations
                .Where(pe => pe.UserId == id)
                .Select(pe => new ProfileEducationDTO
                {
                    EducationId = pe.EducationId,
                    Institution = pe.School,
                    Degree = pe.Degree,
                    FieldOfStudy = pe.FieldOfStudy,
                    StartDate = pe.StartYear.HasValue ? new DateOnly(pe.StartYear.Value, 1, 1) : (DateOnly?)null,
                    EndDate = pe.EndYear.HasValue ? new DateOnly(pe.EndYear.Value, 1, 1) : (DateOnly?)null
                })
                .ToListAsync();

            var jobRolePreferences = await _context.UserJobPreferences
                .Where(p => p.UserId == id)
                .Include(p => p.JobRole)
                .Select(p => new JobRolePreferenceDTO
                {
                    JobRoleId = p.JobRoleId,
                    Name = p.JobRole.Name
                })
                .ToListAsync();

            var profileDto = new UserProfileDTO
            {
                UserId = user.UserId,
                Name = user.Name,
                Email = user.Email,
                Username = user.Username,
                Location = user.Location,
                LocationId = user.LocationId,
                CountryId = user.CountryId,
                LocationName = user.LocationNav?.Name ?? user.Location,
                CountryName = user.CountryNav?.Name,
                PhoneNumber = user.PhoneNumber,
                Nationality = user.Nationality,
                JobPreference = user.JobPreference,
                ProfileBio = user.ProfileBio,
                DoB = user.DoB,
                ProfilePictureUrl = user.ProfilePictureUrl,
                BannerUrl = user.BannerUrl,
                Role = user.Role,
                IsOpenToWork = user.IsOpenToWork,
                Skills = skills,
                Experiences = experiences,
                Educations = educations,
                JobRolePreferences = jobRolePreferences
            };

            return Ok(profileDto);
        }

        // POST: api/Users/5/upload-picture
        // Upload profile picture for a user
        [HttpPost("{id}/upload-picture")]
        [SwaggerFileUpload]
        public async Task<IActionResult> UploadProfilePicture(int id, IFormFile file)
        {
            var currentUserId = GetCurrentUserId();
            var userRole = GetCurrentUserRole();

            if (userRole != "0" && currentUserId != id)
                return Forbid();

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            if (!_fileStorage.ValidateImageFile(file, out var errorMessage))
                return BadRequest(new { message = errorMessage });

            try
            {
                // Delete old profile picture if exists
                if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
                {
                    await _fileStorage.DeleteFileAsync(user.ProfilePictureUrl);
                }

                // Save new profile picture
                var fileUrl = await _fileStorage.SaveFileAsync(file, "profiles");
                user.ProfilePictureUrl = fileUrl;
                await _context.SaveChangesAsync();

                return Ok(new { success = true, fileUrl = fileUrl, message = "Profile picture uploaded successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Error uploading file: {ex.Message}" });
            }
        }

        // POST: api/Users/5/upload-banner
        // Upload banner image for a user
        [HttpPost("{id}/upload-banner")]
        [SwaggerFileUpload]
        public async Task<IActionResult> UploadBanner(int id, IFormFile file)
        {
            var currentUserId = GetCurrentUserId();
            var userRole = GetCurrentUserRole();

            if (userRole != "0" && currentUserId != id)
                return Forbid();

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            if (!_fileStorage.ValidateImageFile(file, out var errorMessage))
                return BadRequest(new { message = errorMessage });

            try
            {
                if (!string.IsNullOrEmpty(user.BannerUrl))
                {
                    await _fileStorage.DeleteFileAsync(user.BannerUrl);
                }

                var fileUrl = await _fileStorage.SaveFileAsync(file, "banners");
                user.BannerUrl = fileUrl;
                await _context.SaveChangesAsync();

                return Ok(new { success = true, fileUrl = fileUrl, message = "Banner uploaded successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Error uploading file: {ex.Message}" });
            }
        }

        // POST: api/Users/me/request-employer – user uploads documents to request employer role
        [HttpPost("me/request-employer")]
        [SwaggerFileUpload]
        public async Task<IActionResult> RequestEmployerRole(IFormFile? document, [FromForm] int? companyId = null, [FromForm] string? note = null)
        {
            var uid = GetCurrentUserId();
            if (uid == null) return Unauthorized();

            var user = await _context.Users.FindAsync(uid.Value);
            if (user == null) return NotFound();

            // Store doc if provided (accepts images and PDFs)
            string? docUrl = null;
            if (document != null && document.Length > 0 && document.Length <= 5 * 1024 * 1024)
            {
                docUrl = await _fileStorage.SaveFileAsync(document, "employer-requests");
            }

            // Set pending employer status: Role = 3 means "Pending Employer"
            user.Role = 3;
            user.EmployerRequestDocumentUrl = docUrl;
            user.EmployerRequestNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

            // Optionally join the company as pending member
            if (companyId.HasValue)
            {
                var alreadyMember = await _context.CompanyMembers
                    .AnyAsync(m => m.CompanyId == companyId.Value && m.UserId == uid.Value);
                if (!alreadyMember)
                {
                    _context.CompanyMembers.Add(new CompanyMember
                    {
                        CompanyId = companyId.Value,
                        UserId = uid.Value,
                        Role = 0, // 0 = pending, 1 = Recruiter, 2 = HRManager, 3 = CompanyAdmin
                        StartDate = DateOnly.FromDateTime(DateTime.UtcNow)
                    });
                }
            }

            await _context.SaveChangesAsync();

            await _context.AuditLogs.AddAsync(new AuditLog
            {
                UserId = uid.Value,
                Action = "RequestEmployerRole",
                TargetType = "User",
                TargetId = uid.Value,
                CreatedAt = DateTime.UtcNow
            });

            // Create notifications for all admins so they see the pending request
            var adminIds = await _context.Users
                .Where(u => u.Role == 0)
                .Select(u => u.UserId)
                .ToListAsync();

            var now = DateTime.UtcNow;
            _context.Notifications.AddRange(adminIds.Select(adminId => new Notification
            {
                UserId = adminId,
                ActorUserId = uid.Value,
                Type = "EmployerRequest",
                Payload = uid.Value.ToString(),
                IsRead = false,
                CreatedAt = now
            }));

            await _context.SaveChangesAsync();

            return Ok(new { message = "Pedido submetido. Aguarda aprovação do administrador.", documentUrl = docUrl });
        }

        // POST: api/Users/{id}/approve-employer – admin or CompanyAdmin member can approve employer role
        [HttpPost("{id}/approve-employer")]
        [Authorize(Roles = "0,2")] // Admin or Employer
        public async Task<IActionResult> ApproveEmployerRole(int id, [FromQuery] int? companyMemberRole = null)
        {
            var actorId = GetCurrentUserId();
            var actorRole = GetCurrentUserRole();

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            // Employers (role=2) can only approve if they are CompanyAdmin (CompanyMember.Role=3) in the same company
            if (actorRole == "2")
            {
                var isCompanyAdmin = await _context.CompanyMembers
                    .AnyAsync(cm => cm.UserId == actorId && cm.Role == 3);
                if (!isCompanyAdmin)
                    return StatusCode(403, "Apenas admins de empresa podem aprovar pedidos de empregador.");
            }

            user.Role = 2; // Employer
            user.IsOpenToWork = false;
            await _context.SaveChangesAsync();

            // If target user is a pending company member, activate them with chosen role (default=Recruiter=1)
            var pendingMembership = await _context.CompanyMembers
                .Where(cm => cm.UserId == id && cm.Role == 0)
                .FirstOrDefaultAsync();
            if (pendingMembership != null)
            {
                pendingMembership.Role = companyMemberRole.HasValue && companyMemberRole.Value is >= 1 and <= 3
                    ? companyMemberRole.Value
                    : 1; // Default to Recruiter
                _context.CompanyMembers.Update(pendingMembership);
            }

            await _context.AuditLogs.AddAsync(new AuditLog
            {
                UserId = actorId ?? 0,
                Action = "ApproveEmployerRole",
                TargetType = "User",
                TargetId = id,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return Ok(new { message = "Utilizador aprovado como empregador." });
        }

        // POST: api/Users/{id}/reject-employer – admin or CompanyAdmin can reject
        [HttpPost("{id}/reject-employer")]
        [Authorize(Roles = "0,2")]
        public async Task<IActionResult> RejectEmployerRole(int id)
        {
            var actorId = GetCurrentUserId();
            var actorRole = GetCurrentUserRole();

            if (actorRole == "2")
            {
                var isCompanyAdmin = await _context.CompanyMembers
                    .AnyAsync(cm => cm.UserId == actorId && cm.Role == 3);
                if (!isCompanyAdmin)
                    return Forbid("Apenas admins de empresa podem rejeitar pedidos de empregador.");
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.Role = 1; // Back to regular user
            user.EmployerRequestDocumentUrl = null;
            user.EmployerRequestNote = null;

            // Remove pending company memberships
            var pendingMemberships = _context.CompanyMembers
                .Where(cm => cm.UserId == id && cm.Role == 0);
            _context.CompanyMembers.RemoveRange(pendingMemberships);

            await _context.SaveChangesAsync();

            return Ok(new { message = "Pedido de empregador rejeitado." });
        }

        // GET: api/Users/pending-employers – list users with Role = 3 (pending) for admin
        [HttpGet("pending-employers")]
        [Authorize(Roles = "0,2")]
        public async Task<IActionResult> GetPendingEmployers()
        {
            var actorId = GetCurrentUserId();
            var actorRole = GetCurrentUserRole();

            // Employers can only see pending requests for companies they are CompanyAdmin of
            IQueryable<User> query = _context.Users.Where(u => u.Role == 3);

            if (actorRole == "2")
            {
                // Find companies where actor is CompanyAdmin (role=3)
                var adminCompanyIds = await _context.CompanyMembers
                    .Where(cm => cm.UserId == actorId && cm.Role == 3)
                    .Select(cm => cm.CompanyId)
                    .ToListAsync();

                // Only show pending users who requested to join those companies
                var pendingInMyCompanies = await _context.CompanyMembers
                    .Where(cm => adminCompanyIds.Contains(cm.CompanyId) && cm.Role == 0)
                    .Select(cm => cm.UserId)
                    .Distinct()
                    .ToListAsync();

                query = query.Where(u => pendingInMyCompanies.Contains(u.UserId));
            }

            var pending = await query
                .Select(u => new UserDTO
                {
                    UserId = u.UserId,
                    Name = u.Name,
                    Email = u.Email,
                    ProfilePictureUrl = u.ProfilePictureUrl,
                    Role = u.Role,
                    EmployerRequestDocumentUrl = u.EmployerRequestDocumentUrl,
                    EmployerRequestNote = u.EmployerRequestNote
                })
                .ToListAsync();

            return Ok(pending);
        }

        // GET: api/Users/network – public user listing for the network/discovery page
        // Available to all authenticated users; returns only non-sensitive fields
        [HttpGet("network")]
        public async Task<IActionResult> GetNetworkUsers([FromQuery] string? search = null)
        {
            IQueryable<User> q = _context.Users.Where(u => u.Role != 0); // exclude admins

            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(u => (u.Name ?? "").Contains(search) || (u.ProfileBio ?? "").Contains(search));

            var users = await q
                .Select(u => new UserDTO
                {
                    UserId = u.UserId,
                    Name = u.Name,
                    ProfileBio = u.ProfileBio,
                    ProfilePictureUrl = u.ProfilePictureUrl,
                    BannerUrl = u.BannerUrl,
                    Role = u.Role
                })
                .ToListAsync();

            return Ok(users);
        }

        // GET: api/Users/me/companies – employer's company memberships
        [HttpGet("me/companies")]
        public async Task<IActionResult> GetMyCompanies()
        {
            var uid = GetCurrentUserId();
            if (uid == null) return Unauthorized();

            var memberships = await _context.CompanyMembers
                .Where(cm => cm.UserId == uid.Value)
                .Include(cm => cm.Company)
                .Select(cm => new
                {
                    cm.CompanyId,
                    CompanyName = cm.Company.Name,
                    cm.Title,
                    cm.Role
                })
                .ToListAsync();

            return Ok(memberships);
        }

        // POST: api/Users
        // Only admins can create users via this endpoint.
        // (Self-registration should be done through /api/Auth/register)
        [HttpPost]
        [Authorize(Roles = "0")]
        public async Task<IActionResult> CreateUser([FromBody] User user)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetUser), new { id = user.UserId }, user);
        }

        // PUT: api/Users/5
        // Admins can update any user; users can update only their own record.
        // Non-admins cannot change Role or PasswordHash through this endpoint.
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] User updated)
        {
            if (id != updated.UserId) return BadRequest();

            var existing = await _context.Users.FindAsync(id);
            if (existing == null) return NotFound();

            var currentUserId = GetCurrentUserId();
            var userRole = GetCurrentUserRole();

            // Only admin or owner can update
            if (userRole != "0" && existing.UserId != currentUserId)
                return Forbid();

            // Non-admins: only allow a subset of fields to be changed
            if (userRole != "0")
            {
                existing.Name = updated.Name;
                existing.PhoneNumber = updated.PhoneNumber;
                existing.Nationality = updated.Nationality;
                existing.JobPreference = updated.JobPreference;
                existing.ProfileBio = updated.ProfileBio;
                existing.DoB = updated.DoB;
                existing.Location = updated.Location;
                existing.LocationId = updated.LocationId;
                existing.CountryId = updated.CountryId;
                existing.IsOpenToWork = updated.IsOpenToWork;
                // Do NOT allow non-admins to change Email, Role, PasswordHash
            }
            else
            {
                // Admin can update most fields; don't automatically accept a plaintext password here
                existing.Name = updated.Name;
                existing.Email = updated.Email;
                existing.Username = updated.Username;
                existing.PhoneNumber = updated.PhoneNumber;
                existing.Nationality = updated.Nationality;
                existing.JobPreference = updated.JobPreference;
                existing.ProfileBio = updated.ProfileBio;
                existing.DoB = updated.DoB;
                existing.Role = updated.Role;
                existing.Location = updated.Location;
                existing.LocationId = updated.LocationId;
                existing.CountryId = updated.CountryId;
                // If you want admins to reset passwords, provide a dedicated endpoint that accepts a hashed password
            }

            try
            {
                _context.Entry(existing).State = EntityState.Modified;
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UserExists(id)) return NotFound();
                throw;
            }

            return NoContent();
        }

        // PUT: api/users/{id}/job-preferences
        [HttpPut("{id}/job-preferences")]
        public async Task<IActionResult> UpdateJobPreferences(int id, [FromBody] List<int> jobRoleIds)
        {
            var currentUserId = GetCurrentUserId();
            var userRole = GetCurrentUserRole();

            if (userRole != "0" && currentUserId != id)
                return Forbid();

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            var existing = await _context.UserJobPreferences
                .Where(p => p.UserId == id)
                .ToListAsync();
            _context.UserJobPreferences.RemoveRange(existing);

            var now = DateTime.UtcNow;
            var newPrefs = (jobRoleIds ?? new List<int>())
                .Distinct()
                .Select(rid => new UserJobPreference
                {
                    UserId = id,
                    JobRoleId = rid,
                    CreatedAt = now
                })
                .ToList();

            await _context.UserJobPreferences.AddRangeAsync(newPrefs);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: api/Users/5
        // Admin only
        [HttpDelete("{id}")]
        [Authorize(Roles = "0")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // PUT: api/Users/{id}/subscription — update the user's subscription plan
        [HttpPut("{id}/subscription")]
        public async Task<IActionResult> UpdateSubscription(int id, [FromBody] UpdateSubscriptionDto dto)
        {
            var currentUserId = GetCurrentUserId();
            var userRole = GetCurrentUserRole();

            if (userRole != "0" && currentUserId != id) return Forbid();

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            if (dto.Plan < 0 || dto.Plan > 2) return BadRequest("Invalid subscription plan. Use 0=Free, 1=Pro, 2=Enterprise.");

            user.SubscriptionPlan = dto.Plan;
            await _context.SaveChangesAsync();

            return Ok(new { subscriptionPlan = user.SubscriptionPlan });
        }

        // PUT: api/Users/{id}/company-role — update a member's role within a company (CompanyAdmin only)
        [HttpPut("{id}/company-role")]
        [Authorize(Roles = "0,2")]
        public async Task<IActionResult> UpdateCompanyMemberRole(int id, [FromBody] UpdateCompanyMemberRoleDto dto)
        {
            var actorId = GetCurrentUserId();
            var actorRole = GetCurrentUserRole();

            if (dto.NewRole < 1 || dto.NewRole > 3)
                return BadRequest("Invalid role. Use 1=Recruiter, 2=HRManager, 3=CompanyAdmin.");

            // Verify actor is CompanyAdmin of the target company (or system admin)
            if (actorRole != "0")
            {
                var isCompanyAdmin = await _context.CompanyMembers
                    .AnyAsync(cm => cm.CompanyId == dto.CompanyId && cm.UserId == actorId && cm.Role == 3);
                if (!isCompanyAdmin) return StatusCode(403, "Apenas admins de empresa podem alterar funções de membros.");
            }

            var member = await _context.CompanyMembers
                .FirstOrDefaultAsync(cm => cm.CompanyId == dto.CompanyId && cm.UserId == id);
            if (member == null) return NotFound("Company member not found.");

            member.Role = dto.NewRole;
            await _context.SaveChangesAsync();

            return Ok(new { userId = id, companyId = dto.CompanyId, newRole = member.Role });
        }

        private bool UserExists(int id)
        {
            return _context.Users.Any(u => u.UserId == id);
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
        // GET: api/users/lookups/nationalities
        [HttpGet("lookups/nationalities")]
        [AllowAnonymous]
        public async Task<IActionResult> GetNationalities()
        {
            var list = await _context.Nationalities
                .OrderBy(n => n.Name)
                .Select(n => new { n.NationalityId, n.Name })
                .ToListAsync();
            return Ok(list);
        }

        // GET: api/users/lookups/jobroles
        [HttpGet("lookups/jobroles")]
        [AllowAnonymous]
        public async Task<IActionResult> GetJobRoles()
        {
            var list = await _context.JobRoles
                .OrderBy(j => j.Name)
                .Select(j => new { j.JobRoleId, j.Name })
                .ToListAsync();
            return Ok(list);
        }

        // GET: api/users/lookups/countries
        [HttpGet("lookups/countries")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCountries()
        {
            var list = await _context.Countries
                .OrderBy(c => c.Name)
                .Select(c => new { c.CountryId, c.Name, c.Code })
                .ToListAsync();
            return Ok(list);
        }

        // GET: api/users/lookups/locations?countryId=1
        [HttpGet("lookups/locations")]
        [AllowAnonymous]
        public async Task<IActionResult> GetLocations([FromQuery] int? countryId = null)
        {
            var q = _context.Locations.Include(l => l.Country).AsQueryable();
            if (countryId.HasValue)
                q = q.Where(l => l.CountryId == countryId.Value);

            var list = await q
                .OrderBy(l => l.Country.Name)
                .ThenBy(l => l.Name)
                .Select(l => new { l.LocationId, l.Name, l.Region, l.CountryId, CountryName = l.Country.Name, CountryCode = l.Country.Code })
                .ToListAsync();
            return Ok(list);
        }

        // GET: api/users/stats – real-time platform stats
        [HttpGet("stats")]
        [AllowAnonymous]
        public async Task<IActionResult> GetStats()
        {
            var userCount = await _context.Users.CountAsync(u => u.Role != 0);
            var companyCount = await _context.Companies.CountAsync();
            var oppCount = await _context.Opportunities.CountAsync();
            var activeConnections = await _context.Connections.CountAsync(c => c.Status == 1);
            return Ok(new { UserCount = userCount, CompanyCount = companyCount, OppCount = oppCount, ActiveConnections = activeConnections });
        }

        // GET: api/users/admin/revenue – admin-only revenue analytics
        [HttpGet("admin/revenue")]
        [Authorize(Roles = "0")]
        public async Task<IActionResult> GetRevenueStats()
        {
            const decimal proPrice = 9m;
            const decimal enterprisePrice = 29m;
            const decimal taxRate = 0.23m;

            var planCounts = await _context.Users
                .Where(u => u.Role != 0)
                .GroupBy(u => u.SubscriptionPlan)
                .Select(g => new { Plan = g.Key, Count = g.Count() })
                .ToListAsync();

            var freeCount = planCounts.FirstOrDefault(p => p.Plan == 0)?.Count ?? 0;
            var proCount = planCounts.FirstOrDefault(p => p.Plan == 1)?.Count ?? 0;
            var enterpriseCount = planCounts.FirstOrDefault(p => p.Plan == 2)?.Count ?? 0;
            var totalUsers = freeCount + proCount + enterpriseCount;

            var monthlyRevenue = (proCount * proPrice) + (enterpriseCount * enterprisePrice);
            var yearlyRevenue = monthlyRevenue * 12;
            var monthlyTax = monthlyRevenue * taxRate;
            var yearlyTax = yearlyRevenue * taxRate;
            var monthlyProfit = monthlyRevenue - monthlyTax;
            var yearlyProfit = yearlyRevenue - yearlyTax;

            // Monthly new-user count for the last 12 months (approximate growth via job applications)
            // Since User doesn't have a CreatedAt, we estimate from audit logs or return a static breakdown
            var now = DateTime.UtcNow;
            var last12Months = Enumerable.Range(0, 12)
                .Select(i => now.AddMonths(-i))
                .OrderBy(d => d)
                .Select(d => new { Year = d.Year, Month = d.Month, Label = d.ToString("MMM yyyy") })
                .ToList();

            // Build application-count-per-month as a proxy for platform growth
            var appsByMonth = await _context.JobApplications
                .Where(a => a.AppliedAt >= now.AddMonths(-12))
                .GroupBy(a => new { a.AppliedAt.Year, a.AppliedAt.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToListAsync();

            var monthlyActivity = last12Months.Select(m => new
            {
                m.Label,
                ApplicationCount = appsByMonth.FirstOrDefault(a => a.Year == m.Year && a.Month == m.Month)?.Count ?? 0,
                EstimatedRevenue = monthlyRevenue // current steady-state estimate
            }).ToList();

            return Ok(new
            {
                FreeCount = freeCount,
                ProCount = proCount,
                EnterpriseCount = enterpriseCount,
                TotalUsers = totalUsers,
                ProPrice = proPrice,
                EnterprisePrice = enterprisePrice,
                MonthlyRevenue = monthlyRevenue,
                YearlyRevenue = yearlyRevenue,
                TaxRate = taxRate,
                MonthlyTax = monthlyTax,
                YearlyTax = yearlyTax,
                MonthlyProfit = monthlyProfit,
                YearlyProfit = yearlyProfit,
                MonthlyActivity = monthlyActivity
            });
        }

        // POST: api/users/me/resign-employer – user removes their own employer status
        [HttpPost("me/resign-employer")]
        public async Task<IActionResult> ResignEmployerRole()
        {
            var uid = GetCurrentUserId();
            if (uid == null) return Unauthorized();

            var user = await _context.Users.FindAsync(uid.Value);
            if (user == null) return NotFound();

            if (user.Role != 2)
                return BadRequest("Apenas empregadores podem remover o estatuto de empregador.");

            user.Role = 1;
            user.IsOpenToWork = true;

            var memberships = _context.CompanyMembers.Where(cm => cm.UserId == uid.Value);
            _context.CompanyMembers.RemoveRange(memberships);

            await _context.AuditLogs.AddAsync(new AuditLog
            {
                UserId = uid.Value,
                Action = "ResignEmployerRole",
                TargetType = "User",
                TargetId = uid.Value,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return Ok(new { message = "Estatuto de empregador removido com sucesso." });
        }

        // PUT: api/users/{id}/open-to-work – toggle IsOpenToWork
        [HttpPut("{id}/open-to-work")]
        public async Task<IActionResult> SetOpenToWork(int id, [FromBody] SetOpenToWorkDto dto)
        {
            var currentUserId = GetCurrentUserId();
            var userRole = GetCurrentUserRole();

            if (userRole != "0" && currentUserId != id) return Forbid();

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.IsOpenToWork = dto.IsOpenToWork;
            await _context.SaveChangesAsync();

            return Ok(new { isOpenToWork = user.IsOpenToWork });
        }

        // GET: api/users/company/{companyId}/employer-requests – company admin sees pending employer requests for their company
        [HttpGet("company/{companyId}/employer-requests")]
        [Authorize(Roles = "0,2")]
        public async Task<IActionResult> GetCompanyEmployerRequests(int companyId)
        {
            var actorId = GetCurrentUserId();
            var actorRole = GetCurrentUserRole();

            if (actorRole == "2")
            {
                var isAdmin = await _context.CompanyMembers
                    .AnyAsync(cm => cm.CompanyId == companyId && cm.UserId == actorId && cm.Role == 3);
                if (!isAdmin)
                    return StatusCode(403, "Apenas admins de empresa podem ver os pedidos de empregador.");
            }

            var pendingUserIds = await _context.CompanyMembers
                .Where(cm => cm.CompanyId == companyId && cm.Role == 0)
                .Select(cm => cm.UserId)
                .ToListAsync();

            var pending = await _context.Users
                .Where(u => pendingUserIds.Contains(u.UserId))
                .Select(u => new UserDTO
                {
                    UserId = u.UserId,
                    Name = u.Name,
                    Email = u.Email,
                    ProfilePictureUrl = u.ProfilePictureUrl,
                    Role = u.Role,
                    EmployerRequestDocumentUrl = u.EmployerRequestDocumentUrl,
                    EmployerRequestNote = u.EmployerRequestNote
                })
                .ToListAsync();

            return Ok(pending);
        }

        // GET: api/users/company/{companyId}/employers – get all active employers in a company
        [HttpGet("company/{companyId}/employers")]
        [Authorize(Roles = "0,2")]
        public async Task<IActionResult> GetCompanyEmployers(int companyId)
        {
            var actorId = GetCurrentUserId();
            var actorRole = GetCurrentUserRole();

            if (actorRole == "2")
            {
                var isMember = await _context.CompanyMembers
                    .AnyAsync(cm => cm.CompanyId == companyId && cm.UserId == actorId && cm.Role >= 1);
                if (!isMember)
                    return StatusCode(403, "Não és membro desta empresa.");
            }

            var members = await _context.CompanyMembers
                .Where(cm => cm.CompanyId == companyId && cm.Role >= 1)
                .Include(cm => cm.User)
                .Select(cm => new
                {
                    cm.CompanyMemberId,
                    cm.UserId,
                    cm.Role,
                    cm.Title,
                    cm.StartDate,
                    Name = cm.User.Name,
                    Email = cm.User.Email,
                    ProfilePictureUrl = cm.User.ProfilePictureUrl
                })
                .ToListAsync();

            return Ok(members);
        }

        // DELETE: api/users/company/{companyId}/employers/{memberId} – company admin removes a member
        [HttpDelete("company/{companyId}/employers/{memberId}")]
        [Authorize(Roles = "0,2")]
        public async Task<IActionResult> RemoveCompanyEmployer(int companyId, int memberId)
        {
            var actorId = GetCurrentUserId();
            var actorRole = GetCurrentUserRole();

            if (actorRole == "2")
            {
                var isAdmin = await _context.CompanyMembers
                    .AnyAsync(cm => cm.CompanyId == companyId && cm.UserId == actorId && cm.Role == 3);
                if (!isAdmin)
                    return StatusCode(403, "Apenas admins de empresa podem remover membros.");
            }

            var member = await _context.CompanyMembers
                .FirstOrDefaultAsync(cm => cm.CompanyMemberId == memberId && cm.CompanyId == companyId);
            if (member == null) return NotFound();

            var targetUser = await _context.Users.FindAsync(member.UserId);
            if (targetUser != null && targetUser.Role == 2)
            {
                targetUser.Role = 1;
                targetUser.IsOpenToWork = true;
            }

            _context.CompanyMembers.Remove(member);

            await _context.AuditLogs.AddAsync(new AuditLog
            {
                UserId = actorId ?? 0,
                Action = "RemoveCompanyEmployer",
                TargetType = "CompanyMember",
                TargetId = memberId,
                Metadata = $"CompanyId:{companyId}",
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return Ok(new { message = "Membro removido da empresa." });
        }

    }

    public class UpdateSubscriptionDto
    {
        public int Plan { get; set; }
    }

    public class UpdateCompanyMemberRoleDto
    {
        public int CompanyId { get; set; }
        public int NewRole { get; set; }
    }

    public class SetOpenToWorkDto
    {
        public bool IsOpenToWork { get; set; }
    }
}