using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using APIPSI16.Data;
using APIPSI16.Models;

namespace APIPSI16.Services
{
    public interface ISessionService
    {
        Task<Session> CreateSessionAsync(int userId, string token, DateTime expiresAt);
        Task InvalidateAllUserSessionsAsync(int userId);
        Task<bool> IsSessionValidAsync(int userId, string token);
        Task InvalidateSessionAsync(string token);
    }

    public class SessionService : ISessionService
    {
        private readonly xcleratesystemslinks_SampleDBContext _db;

        public SessionService(xcleratesystemslinks_SampleDBContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task<Session> CreateSessionAsync(int userId, string token, DateTime expiresAt)
        {
            // FIRST: Invalidate ALL old sessions for this user
            var oldSessions = await _db.Sessions
                .Where(s => s.UserId == userId && s.IsActive)
                .ToListAsync();

            foreach (var oldSession in oldSessions)
            {
                oldSession.IsActive = false;
                oldSession.InvalidatedAt = DateTime.UtcNow;
            }

            if (oldSessions.Any())
            {
                await _db.SaveChangesAsync();
            }

            // THEN: Create the new session
            var session = new Session
            {
                UserId = userId,
                Token = token,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                IsActive = true,
                InvalidatedAt = null
            };

            _db.Sessions.Add(session);
            await _db.SaveChangesAsync();

            return session;
        }

        public async Task InvalidateAllUserSessionsAsync(int userId)
        {
            var activeSessions = await _db.Sessions
                .Where(s => s.UserId == userId && s.IsActive)
                .ToListAsync();

            foreach (var session in activeSessions)
            {
                session.IsActive = false;
                session.InvalidatedAt = DateTime.UtcNow;
            }

            if (activeSessions.Any())
            {
                await _db.SaveChangesAsync();
            }
        }

        public async Task<bool> IsSessionValidAsync(int userId, string token)
        {
            var session = await _db.Sessions
                .FirstOrDefaultAsync(s =>
                    s.UserId == userId &&
                    s.Token == token &&
                    s.IsActive &&
                    s.ExpiresAt > DateTime.UtcNow);

            return session != null;
        }

        public async Task InvalidateSessionAsync(string token)
        {
            var session = await _db.Sessions
                .FirstOrDefaultAsync(s => s.Token == token && s.IsActive);

            if (session != null)
            {
                session.IsActive = false;
                session.InvalidatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
        }
    }
}