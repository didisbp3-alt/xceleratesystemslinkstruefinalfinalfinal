using APIPSI16.Models;

namespace XcelerateLinks.Models.ViewModels
{
    public class ApplicationCreateViewModel
    {
        public JobApplication Application { get; set; } = new();

        public IEnumerable<Opportunity> Opportunities { get; set; } = Array.Empty<Opportunity>();
    }
}
