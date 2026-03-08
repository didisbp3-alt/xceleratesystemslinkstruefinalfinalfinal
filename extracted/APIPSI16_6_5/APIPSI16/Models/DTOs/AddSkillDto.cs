namespace APIPSI16.Models.DTOs
{
    public class ApplyDto
    {
        public int OpportunityId { get; set; }
        public string? Name { get; set; }
        public string? CoverLetter { get; set; }
        public string? PhoneNumber { get; set; }
        public string? LinkedInUrl { get; set; }
        public string? PortfolioUrl { get; set; }
        public int? YearsOfExperience { get; set; }
        public bool? OpenToRemote { get; set; }
        // Comma-separated job role IDs selected by applicant
        public string? SelectedJobRoleIds { get; set; }
    }
}
