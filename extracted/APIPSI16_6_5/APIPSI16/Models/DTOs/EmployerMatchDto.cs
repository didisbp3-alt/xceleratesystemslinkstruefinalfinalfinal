namespace APIPSI16.Models.DTOs
{
    /// <summary>
    /// Returned by GET /api/opportunities/{id}/employer-matches.
    /// Represents a previous contact scored against a specific opportunity.
    /// </summary>
    public class EmployerMatchDto
    {
        public int UserId { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public string? LocationName { get; set; }
        public string? CountryName { get; set; }
        public int? JobPreference { get; set; }
        public bool IsAvailable { get; set; }
        public string? PreviousOutcome { get; set; }
        public string? PreviousStage { get; set; }
        public string? PreviousOpportunityTitle { get; set; }
        public DateTime LastContactAt { get; set; }
        public int? PriorityId { get; set; }
        public int EmployerCandidateHistoryId { get; set; }
        public int MatchScore { get; set; }
    }
}
