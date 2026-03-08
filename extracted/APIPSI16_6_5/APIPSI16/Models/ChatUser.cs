using System;
using System.Collections.Generic;

namespace APIPSI16.Models;

public partial class ChatUser
{
    public int ChatUserId { get; set; }

    public int ChatId { get; set; }

    public int UserId { get; set; }

    public DateTime? JoinedAt { get; set; }

    public string? Role { get; set; }

    public virtual Chat Chat { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
