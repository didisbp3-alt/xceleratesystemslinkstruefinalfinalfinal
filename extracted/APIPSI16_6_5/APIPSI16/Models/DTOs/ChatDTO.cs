using System;

namespace APIPSI16.Models.DTOs;

public class ChatDTO
{
    public int ChatId { get; set; }
    public string Type { get; set; }
    public DateTime? CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public int UserCount { get; set; }
    public int MessageCount { get; set; }
}