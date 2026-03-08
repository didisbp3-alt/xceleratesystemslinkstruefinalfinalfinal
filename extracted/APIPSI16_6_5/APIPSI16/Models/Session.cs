using System;

namespace APIPSI16.Models;

public partial class Session
{
    public int SessionId { get; set; }

    public int UserId { get; set; }

    public string Token { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public bool IsActive { get; set; }

    public DateTime? InvalidatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}