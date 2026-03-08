using System;
using System.Collections.Generic;

namespace APIPSI16.Models;

public partial class Opportunity
{
    public int Id { get; set; }

    public string? Title { get; set; }

    public int? CreatorId { get; set; }

    public int? CompanyId { get; set; }

    public byte? EmploymentType { get; set; }

    public byte? SeniorityLevel { get; set; }

    public string? Location { get; set; }

    /// <summary>FK to the Locations table. Used for structured location-based matching.</summary>
    public int? LocationId { get; set; }

    /// <summary>FK to the Countries table.</summary>
    public int? CountryId { get; set; }

    public virtual Location? LocationNav { get; set; }

    public virtual Country? CountryNav { get; set; }

    public byte? RemoteOption { get; set; }

    /// <summary>
    /// 0=Standard, 1=GuidedApplication (candidatura acompanhada – live employer oversight),
    /// 2=LongTerm (candidatura de longo prazo com fase de testes)
    /// </summary>
    public byte? OpportunityType { get; set; }

    /// <summary>
    /// 0=External (talento fora da empresa), 1=Internal (talento já dentro da empresa), 2=Mixed
    /// </summary>
    public byte? ApplicationScope { get; set; }

    /// <summary>Comma-separated JobRoleId values (e.g. "1,3,7")</summary>
    public string? RequiredJobRoleIds { get; set; }

    public virtual Company? Company { get; set; }

    public virtual User? Creator { get; set; }

    public virtual ICollection<JobApplication> JobApplications { get; set; } = new List<JobApplication>();
    public virtual ICollection<EmployerCandidateHistory> EmployerCandidateHistories { get; set; } = new List<EmployerCandidateHistory>();
}