using APIPSI16.DTOs;

public class CompanyProfileDTO
{
    public int CompanyId { get; set; }
    public string Name { get; set; }
    public string? Industry { get; set; }
    public string? Location { get; set; }
    public string? CompanyLogoUrl { get; set; }
    public DateTime? CreatedAt { get; set; }
    public List<OpportunityDTO> Opportunities { get; set; } = new();
    public List<CompanyMemberSummaryDTO> Members { get; set; } = new();
}

public class CompanyMemberSummaryDTO
{
    public int UserId { get; set; }
    public string? UserName { get; set; }
    public int Role { get; set; }
    public string? Title { get; set; }
}