using System;
using System.Collections.Generic;

namespace APIPSI16.Models;

public partial class Notification
{
    public int NotificationId { get; set; }

    public int UserId { get; set; }

    public int? ActorUserId { get; set; }

    public string Type { get; set; } = null!;

    public string? Payload { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User? ActorUser { get; set; }

    public virtual User User { get; set; } = null!;
}
