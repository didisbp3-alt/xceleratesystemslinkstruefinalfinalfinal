using System.Threading.Tasks;
using APIPSI16.Models;

public interface IUserService
{
    Task<User?> FindByEmailAsync(string email);
    Task UpdatePasswordAsync(User user, string newPassword);
}