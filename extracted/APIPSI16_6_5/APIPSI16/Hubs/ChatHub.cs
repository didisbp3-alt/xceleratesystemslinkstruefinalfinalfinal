using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using System.Security.Claims;
using System.Collections.Concurrent;
using APIPSI16.Data;
using APIPSI16.Models;
using Microsoft.EntityFrameworkCore;

namespace APIPSI16.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly xcleratesystemslinks_SampleDBContext _context;
        private readonly ILogger<ChatHub> _logger;

        private static readonly ConcurrentDictionary<int, HashSet<string>> _userConnections =
            new ConcurrentDictionary<int, HashSet<string>>();

        public ChatHub(xcleratesystemslinks_SampleDBContext context, ILogger<ChatHub> logger)
        {
            _context = context;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                _logger.LogInformation("User {UserId} connected to ChatHub with ConnectionId: {ConnectionId}",
                    userId.Value, Context.ConnectionId);

                _userConnections.AddOrUpdate(
                    userId.Value,
                    new HashSet<string> { Context.ConnectionId },
                    (_, existing) => { existing.Add(Context.ConnectionId); return existing; });

                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId.Value}");

                var chatIds = await _context.ChatUsers
                    .Where(cu => cu.UserId == userId.Value)
                    .Select(cu => cu.ChatId)
                    .ToListAsync();

                foreach (var chatId in chatIds)
                {
                    await Clients.Group($"chat_{chatId}").SendAsync("UserOnline", new
                    {
                        userId = userId.Value,
                        isOnline = true
                    });
                }
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = GetCurrentUserId();
            if (userId.HasValue)
            {
                _logger.LogInformation("User {UserId} disconnected from ChatHub", userId.Value);

                if (_userConnections.TryGetValue(userId.Value, out var connections))
                {
                    connections.Remove(Context.ConnectionId);
                    if (connections.Count == 0)
                    {
                        _userConnections.TryRemove(userId.Value, out _);

                        var chatIds = await _context.ChatUsers
                            .Where(cu => cu.UserId == userId.Value)
                            .Select(cu => cu.ChatId)
                            .ToListAsync();

                        foreach (var chatId in chatIds)
                        {
                            await Clients.Group($"chat_{chatId}").SendAsync("UserOnline", new
                            {
                                userId = userId.Value,
                                isOnline = false
                            });
                        }
                    }
                }

                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId.Value}");
            }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task JoinChat(int chatId)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                await Clients.Caller.SendAsync("Error", "Unauthorized");
                return;
            }

            var isParticipant = await _context.ChatUsers
                .AnyAsync(cu => cu.ChatId == chatId && cu.UserId == userId.Value);

            if (!isParticipant)
            {
                await Clients.Caller.SendAsync("Error", "You are not a participant in this chat");
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"chat_{chatId}");
            _logger.LogInformation("User {UserId} joined chat {ChatId}", userId.Value, chatId);

            var onlineParticipants = await _context.ChatUsers
                .Where(cu => cu.ChatId == chatId && cu.UserId != userId.Value)
                .Select(cu => cu.UserId)
                .ToListAsync();

            var onlineStatuses = onlineParticipants.Select(uid => new
            {
                userId = uid,
                isOnline = _userConnections.ContainsKey(uid)
            }).ToList();

            await Clients.Caller.SendAsync("ParticipantPresence", onlineStatuses);
        }

        public async Task LeaveChat(int chatId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chat_{chatId}");
            var userId = GetCurrentUserId();
            _logger.LogInformation("User {UserId} left chat {ChatId}", userId, chatId);
        }

        public async Task SendMessage(int chatId, string messageText)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                await Clients.Caller.SendAsync("Error", "Unauthorized");
                return;
            }

            var isParticipant = await _context.ChatUsers
                .AnyAsync(cu => cu.ChatId == chatId && cu.UserId == userId.Value);

            if (!isParticipant)
            {
                await Clients.Caller.SendAsync("Error", "You are not a participant in this chat");
                return;
            }

            var message = new ChatMessage
            {
                ChatId = chatId,
                SenderUserId = userId.Value,
                MessageText = messageText,
                CreatedAt = DateTime.UtcNow,
                DeliveredAt = DateTime.UtcNow
            };

            _context.ChatMessages.Add(message);
            await _context.SaveChangesAsync();

            var sender = await _context.Users.FindAsync(userId.Value);

            await Clients.Group($"chat_{chatId}").SendAsync("ReceiveMessage", new
            {
                messageId = message.MessageId,
                chatId = message.ChatId,
                senderUserId = message.SenderUserId,
                senderName = sender?.Name,
                messageText = message.MessageText,
                createdAt = message.CreatedAt,
                deliveredAt = message.DeliveredAt
            });

            _logger.LogInformation("User {UserId} sent message to chat {ChatId}", userId.Value, chatId);
        }

        public async Task TypingIndicator(int chatId, bool isTyping)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return;

            var sender = await _context.Users.FindAsync(userId.Value);

            await Clients.OthersInGroup($"chat_{chatId}").SendAsync("UserTyping", new
            {
                userId = userId.Value,
                userName = sender?.Name,
                isTyping = isTyping
            });
        }

        public async Task MarkMessageAsRead(int chatId, int messageId)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return;

            var message = await _context.ChatMessages.FindAsync(messageId);
            if (message != null && message.ChatId == chatId && message.SenderUserId != userId.Value)
            {
                if (message.ReadAt == null)
                {
                    message.ReadAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    await Clients.Group($"user_{message.SenderUserId}").SendAsync("MessageRead", new
                    {
                        messageId = messageId,
                        chatId = chatId,
                        readBy = userId.Value,
                        readAt = message.ReadAt
                    });
                }
            }
        }

        public static bool IsUserOnline(int userId) => _userConnections.ContainsKey(userId);

        private int? GetCurrentUserId()
        {
            var claim = Context.User?.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
        }
    }
}
