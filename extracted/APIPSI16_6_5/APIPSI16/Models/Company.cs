using System;
using System.Collections.Generic;

namespace APIPSI16.Models;

public partial class Company
{
    public int CompanyId { get; set; }

    public string Name { get; set; } = null!;

    public string? Industry { get; set; }

    public string? Location { get; set; }

    public DateTime? CreatedAt { get; set; }

    public string? CompanyLogoUrl { get; set; }

    public virtual ICollection<Opportunity> Opportunities { get; set; } = new List<Opportunity>();
    public virtual ICollection<EmployerCandidateHistory> EmployerCandidateHistories { get; set; } = new List<EmployerCandidateHistory>();
    public virtual ICollection<CompanyMember> CompanyMembers { get; set; } = new List<CompanyMember>();
}