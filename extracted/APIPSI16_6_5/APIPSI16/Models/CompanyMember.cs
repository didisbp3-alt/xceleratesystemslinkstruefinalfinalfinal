using System;
using System.Collections.Generic;

namespace APIPSI16.Models;

public partial class CompanyMember
{
    public int CompanyMemberId { get; set; }

    public int CompanyId { get; set; }

    public int UserId { get; set; }

    public string? Title { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public int Role { get; set; }

    public virtual Company Company { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
