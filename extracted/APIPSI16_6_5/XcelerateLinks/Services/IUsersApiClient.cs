using APIPSI16.Models.DTOs;
using System.Threading.Tasks;
using APIPSI16.Models;

namespace XcelerateLinks.Mvc.Services
{
    public interface IUsersApiClient
    {
        Task<CurrentUserDto?> GetCurrentUserAsync();
    }
}