using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace APIPSI16.Models;

public partial class JobApplication
{
    public int JobApplicationId { get; set; }

    public int OpportunityId { get; set; }

    public int UserId { get; set; }

    public byte Status { get; set; }

    public DateTime AppliedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? Name { get; set; }

    public string? CoverLetter { get; set; }
    public string? PhoneNumber { get; set; }

    [Column("LinkedInUrl")]
    public string? ProfessionalUrl { get; set; }

    public string? PortfolioUrl { get; set; }
    public int? YearsOfExperience { get; set; }
    public bool? OpenToRemote { get; set; }
    public string? SelectedJobRoleIds { get; set; }

    public byte? ApplicantResponse { get; set; }

    public string? LatestEmployerMessage { get; set; }

    public virtual Opportunity Opportunity { get; set; } = null!;

    public virtual User User { get; set; } = null!;
    public virtual ICollection<InterviewRound> InterviewRounds { get; set; } = new List<InterviewRound>();
}
