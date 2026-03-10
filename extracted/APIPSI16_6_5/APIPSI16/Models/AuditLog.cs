using System;
using System.Collections.Generic;

namespace APIPSI16.Models;

public partial class AuditLog
{
    public int AuditLogId { get; set; }

    public int UserId { get; set; }

    public string Action { get; set; } = null!;

    public string? TargetType { get; set; }

    public int? TargetId { get; set; }

    public string? Metadata { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
