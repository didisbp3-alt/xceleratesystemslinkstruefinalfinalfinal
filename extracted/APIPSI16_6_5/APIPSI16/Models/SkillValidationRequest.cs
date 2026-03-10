using System;

namespace APIPSI16.Models;

public partial class SkillValidationRequest
{
    public int RequestId { get; set; }

    public int UserId { get; set; }

    public int? SkillId { get; set; }

    public string? RequestedSkillName { get; set; }

    public string? DocumentUrl { get; set; }

    public string? Notes { get; set; }

    public int Status { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedAt { get; set; }

    public int? ReviewedByUserId { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual Skill? Skill { get; set; }
}
