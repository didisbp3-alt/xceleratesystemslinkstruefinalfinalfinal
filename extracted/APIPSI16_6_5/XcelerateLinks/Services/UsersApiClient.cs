using APIPSI16.Models.DTOs;
using Microsoft.AspNetCore.Http;
using XcelerateLinks.Models;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using APIPSI16.Services;

namespace XcelerateLinks.Mvc.Services
{
    public class UsersApiClient : IUsersApiClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        public UsersApiClient(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor)
        {
            _httpClientFactory = httpClientFactory;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<CurrentUserDto?> GetCurrentUserAsync()
        {
            var client = _httpClientFactory.CreateClient("Api"); // TokenHandler will attach the token now
            using var resp = await client.GetAsync("users/me"); // adjust path if necessary
            if (!resp.IsSuccessStatusCode) return null;

            await using var stream = await resp.Content.ReadAsStreamAsync();
            return await JsonSerializer.DeserializeAsync<CurrentUserDto>(stream, _jsonOptions);
        }
    }
}