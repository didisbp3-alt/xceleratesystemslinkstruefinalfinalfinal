using APIPSI16.Data;
using APIPSI16.Models;
using APIPSI16.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace APIPSI16.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RatingsController : ControllerBase
    {
        private readonly xcleratesystemslinks_SampleDBContext _context;

        public RatingsController(xcleratesystemslinks_SampleDBContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        // POST: api/Ratings
        // Create a new rating – if one already exists for this user+entity, update it instead (upsert).
        [HttpPost]
        public async Task<IActionResult> CreateRating([FromBody] CreateRatingDTO dto)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue) return Unauthorized();

            if (dto.Score < 1 || dto.Score > 5)
                return BadRequest("Score must be between 1 and 5");

            // Upsert: if user already rated this entity, update instead of creating duplicate
            var existing = await _context.Ratings
                .FirstOrDefaultAsync(r => r.RatedByUserId == currentUserId.Value
                    && r.RatedEntityId == dto.RatedEntityId
                    && r.EntityType == dto.EntityType);

            if (existing != null)
            {
                existing.Score = dto.Score;
                existing.Review = dto.Review;
                existing.UpdatedAt = DateTime.UtcNow; // preserve original CreatedAt
                await _context.SaveChangesAsync();
                return Ok(existing);
            }

            var rating = new Rating
            {
                RatedByUserId = currentUserId.Value,
                RatedEntityId = dto.RatedEntityId,
                EntityType = dto.EntityType,
                Score = dto.Score,
                Review = dto.Review,
                CreatedAt = DateTime.UtcNow
            };

            _context.Ratings.Add(rating);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetRating), new { id = rating.RatingId }, rating);
        }

        // PUT: api/Ratings/5
        // Update an existing rating (only by the original rater or admin)
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateRating(int id, [FromBody] CreateRatingDTO dto)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue) return Unauthorized();

            if (dto.Score < 1 || dto.Score > 5)
                return BadRequest("Score must be between 1 and 5");

            var rating = await _context.Ratings.FindAsync(id);
            if (rating == null) return NotFound();

            var userRole = GetCurrentUserRole();
            if (userRole != "0" && rating.RatedByUserId != currentUserId.Value)
                return Forbid();

            rating.Score = dto.Score;
            rating.Review = dto.Review;
            rating.UpdatedAt = DateTime.UtcNow; // preserve original CreatedAt
            await _context.SaveChangesAsync();

            return Ok(rating);
        }

        // GET: api/Ratings/my/{entityType}/{entityId}
        // Check if the current user has already rated this entity
        [HttpGet("my/{entityType}/{entityId}")]
        public async Task<IActionResult> GetMyRating(string entityType, int entityId)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue) return Unauthorized();

            var rating = await _context.Ratings
                .FirstOrDefaultAsync(r => r.RatedByUserId == currentUserId.Value
                    && r.RatedEntityId == entityId
                    && r.EntityType == entityType);

            if (rating == null) return NotFound();

            return Ok(new RatingDTO
            {
                RatingId = rating.RatingId,
                RatedByUserId = rating.RatedByUserId,
                RatedEntityId = rating.RatedEntityId,
                EntityType = rating.EntityType,
                Score = rating.Score,
                Review = rating.Review,
                CreatedAt = rating.CreatedAt
            });
        }

        // GET: api/Ratings/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetRating(int id)
        {
            var rating = await _context.Ratings.FindAsync(id);
            if (rating == null) return NotFound();

            return Ok(rating);
        }

        // GET: api/Ratings/entity/User/5
        // Get all ratings for a specific entity
        [HttpGet("entity/{entityType}/{entityId}")]
        public async Task<IActionResult> GetEntityRatings(string entityType, int entityId)
        {
            var ratings = await _context.Ratings
                .Where(r => r.EntityType == entityType && r.RatedEntityId == entityId)
                .Include(r => r.RatedByUser)
                .Select(r => new RatingDTO
                {
                    RatingId = r.RatingId,
                    RatedByUserId = r.RatedByUserId,
                    RatedByUserName = r.RatedByUser != null ? r.RatedByUser.Name : null,
                    RatedEntityId = r.RatedEntityId,
                    EntityType = r.EntityType,
                    Score = r.Score,
                    Review = r.Review,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync();

            return Ok(ratings);
        }

        // GET: api/Ratings/aggregate/User/5
        // Get rating aggregate for an entity
        [HttpGet("aggregate/{entityType}/{entityId}")]
        public async Task<IActionResult> GetRatingAggregate(string entityType, int entityId)
        {
            var ratings = await _context.Ratings
                .Where(r => r.EntityType == entityType && r.RatedEntityId == entityId)
                .ToListAsync();

            if (!ratings.Any())
            {
                return Ok(new RatingAggregateDTO
                {
                    EntityId = entityId,
                    EntityType = entityType,
                    AverageRating = 0,
                    TotalRatings = 0
                });
            }

            var aggregate = new RatingAggregateDTO
            {
                EntityId = entityId,
                EntityType = entityType,
                AverageRating = ratings.Average(r => r.Score),
                TotalRatings = ratings.Count
            };

            return Ok(aggregate);
        }

        // DELETE: api/Ratings/5
        // Delete a rating (only by creator or admin)
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRating(int id)
        {
            var rating = await _context.Ratings.FindAsync(id);
            if (rating == null) return NotFound();

            var currentUserId = GetCurrentUserId();
            var userRole = GetCurrentUserRole();

            if (userRole != "0" && rating.RatedByUserId != currentUserId)
                return Forbid();

            _context.Ratings.Remove(rating);
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
