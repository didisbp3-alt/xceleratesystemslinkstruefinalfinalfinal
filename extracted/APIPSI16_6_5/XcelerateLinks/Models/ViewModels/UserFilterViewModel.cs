using APIPSI16.Models;
using APIPSI16.Models.DTOs;

namespace XcelerateLinks.Models.ViewModels
{
    public class UserFilterViewModel
    {
        public int? JobPreference { get; set; }

        public int? Nationality { get; set; }

        public IEnumerable<UserDTO> Users { get; set; } = Enumerable.Empty<UserDTO>();

        public string? ErrorMessage { get; set; }
    }
}
