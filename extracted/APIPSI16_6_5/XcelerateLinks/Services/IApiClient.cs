using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XcelerateLinks.Models;
using APIPSI16.Models;
using APIPSI16.Models.DTOs;

namespace XcelerateLinks.Mvc.Services
{
    public interface IApiClient
    {
        Task<IEnumerable<UserDTO>> GetUsersByNationalityAsync(int nationalityId, CancellationToken ct = default);
        Task<IEnumerable<UserDTO>> GetUsersByJobRoleAsync(int jobRoleId, CancellationToken ct = default);
        // Add other API methods as needed
    }
}