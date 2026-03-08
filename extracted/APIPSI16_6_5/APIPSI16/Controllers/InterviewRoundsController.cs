using APIPSI16.Data;
using APIPSI16.Models;
using APIPSI16.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace APIPSI16.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class InterviewRoundsController : ControllerBase
    {
        private readonly xcleratesystemslinks_SampleDBContext _db;
        public InterviewRoundsController(xcleratesystemslinks_SampleDBContext db) => _db = db;

        [HttpPost("schedule")]
        public async Task<IActionResult> Schedule([FromBody] ScheduleDto dto)
        {
            var actorId = GetUserId();
            if (actorId == null) return Unauthorized();

            var round = new InterviewRound
            {
                JobApplicationId = dto.JobApplicationId,
                RoundNumber = dto.RoundNumber,
                ScheduledAt = dto.ScheduledAt,
                InterviewerUserId = dto.InterviewerUserId,
                Notes = dto.Notes,
                Outcome = dto.Outcome
            };

            await _db.InterviewRounds.AddAsync(round);

            var app = await _db.JobApplications.FindAsync(dto.JobApplicationId);
            if (app != null)
            {
                await _db.Notifications.AddAsync(new Notification
                {
                    UserId = app.UserId,
                    ActorUserId = actorId.Value,
                    Type = "InterviewScheduled",
                    Payload = $"{{\"applicationId\":{dto.JobApplicationId},\"scheduledAt\":\"{dto.ScheduledAt:o}\"}}",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = round.InterviewRoundId }, round);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var r = await _db.InterviewRounds.FindAsync(id);
            if (r == null) return NotFound();
            return Ok(r);
        }

        private int? GetUserId()
        {
            var sid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(sid, out var id) ? id : (int?)null;
        }
    }
}