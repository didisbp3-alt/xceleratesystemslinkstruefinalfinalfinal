using System.ComponentModel.DataAnnotations;
using APIPSI16.Services;

namespace APIPSI16.Models.DTOs
{
    public class ApplyDto
    {
        public int OpportunityId { get; set; }
        public string? Name { get; set; }
        public string? CoverLetter { get; set; }

        [RegularExpression(InputValidation.PhonePattern, ErrorMessage = "Invalid phone number format.")]
        public string? PhoneNumber { get; set; }

        [Url(ErrorMessage = "Professional profile must be a valid URL (e.g. https://example.com/yourprofile).")]
        [MaxLength(300)]
        public string? ProfessionalUrl { get; set; }

        [Url(ErrorMessage = "Portfolio URL must be a valid URL (e.g. https://yourportfolio.com).")]
        [MaxLength(300)]
        public string? PortfolioUrl { get; set; }

        public int? YearsOfExperience { get; set; }
        public bool? OpenToRemote { get; set; }
        public string? SelectedJobRoleIds { get; set; }
    }
}

