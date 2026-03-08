using System.Net.Http;
using System.Net.Http.Json;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XcelerateLinks.Models;
using XcelerateLinks.Mvc.Services;
using APIPSI16.Models.DTOs;
using APIPSI16.Models;

namespace XcelerateLinks.Mvc.Services
{
    public class ApiClient : IApiClient
    {
        private readonly HttpClient _http;

        public ApiClient(HttpClient http) => _http = http;

        public async Task<IEnumerable<UserDTO>> GetUsersByNationalityAsync(int nationalityId, CancellationToken ct = default)
        {
            // Expected API route: GET /api/users/byNationality/{id}
            var res = await _http.GetFromJsonAsync<IEnumerable<UserDTO>>($"api/users/byNationality/{nationalityId}", ct);
            return res ?? Array.Empty<UserDTO>();
        }

        public async Task<IEnumerable<UserDTO>> GetUsersByJobRoleAsync(int jobRoleId, CancellationToken ct = default)
        {
            var res = await _http.GetFromJsonAsync<IEnumerable<UserDTO>>($"api/users/byJobRole/{jobRoleId}", ct);
            return res ?? Array.Empty<UserDTO>();
        }
    }
}