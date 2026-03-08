using Microsoft.EntityFrameworkCore;
using APIPSI16.Models;
using APIPSI16.Data;
using System.Threading.Tasks;

public class UserService : IUserService
{
    private readonly xcleratesystemslinks_SampleDBContext _context;

    public UserService(xcleratesystemslinks_SampleDBContext context)
    {
        _context = context;
    }

    public async Task<User?> FindByEmailAsync(string email)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email.ToLower());
    }

    public async Task UpdatePasswordAsync(User user, string newPassword)
    {
        user.PasswordHash = newPassword;
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }
}