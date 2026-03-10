namespace APIPSI16.Models.DTOs
{
    public class CurrentUserDto
    {
        public string Id { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string? AvatarUrl { get; set; }
        public int? Role { get; set; }
    }
}