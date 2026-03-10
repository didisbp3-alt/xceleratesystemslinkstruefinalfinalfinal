using System;
using System.Collections.Generic;

namespace APIPSI16.Models.DTOs;

public class ChatDetailDTO
{
    public int ChatId { get; set; }
    public string Type { get; set; }
    public DateTime? CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public string? CreatedByUserName { get; set; }
    public List<ChatUserDetailDTO> ChatUsers { get; set; } = new();
    public List<ChatMessageDTO> ChatMessages { get; set; } = new();
}

public class ChatUserDetailDTO
{
    public int ChatUserId { get; set; }
    public int ChatId { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; }
    public DateTime? JoinedAt { get; set; }
    public string Role { get; set; }
}

