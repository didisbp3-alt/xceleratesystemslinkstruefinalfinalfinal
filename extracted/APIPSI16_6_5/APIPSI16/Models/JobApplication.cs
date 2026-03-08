using System;

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

    // Extended application fields
    public string? CoverLetter { get; set; }
    public string? PhoneNumber { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? PortfolioUrl { get; set; }
    public int? YearsOfExperience { get; set; }
    public bool? OpenToRemote { get; set; }
    // Comma-separated job role IDs chosen by the applicant
    public string? SelectedJobRoleIds { get; set; }

    /// <summary>Response from the applicant: null=no action needed, 1=accepted, 2=declined.</summary>
    public byte? ApplicantResponse { get; set; }

    /// <summary>Latest message/note from the employer, shown to the applicant at the current stage.</summary>
    public string? LatestEmployerMessage { get; set; }

    public virtual Opportunity Opportunity { get; set; } = null!;

    public virtual User User { get; set; } = null!;
    public virtual ICollection<InterviewRound> InterviewRounds { get; set; } = new List<InterviewRound>();
}