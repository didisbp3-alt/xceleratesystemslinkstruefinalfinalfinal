using System;

namespace APIPSI16.Models.DTOs
{
    public class ChatMessageDTO
    {
        public int MessageId { get; set; }
        public int ChatId { get; set; }
        public int SenderUserId { get; set; }
        public string? SenderName { get; set; }
        public string MessageText { get; set; } = null!;
        public DateTime? CreatedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime? ReadAt { get; set; }
    }

    public class SendMessageDTO
    {
        public int ChatId { get; set; }
        public string MessageText { get; set; } = null!;
    }

    public class ChatListDTO
    {
        public int ChatId { get; set; }
        public string? ChatName { get; set; }
        public int UnreadCount { get; set; }
        public ChatMessageDTO? LastMessage { get; set; }
    }
}
