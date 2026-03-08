using System;

namespace APIPSI16.DTOs
{
    public class CompanyMemberDTO
    {
        public int CompanyMemberId { get; set; }
        public int CompanyId { get; set; }
        public int UserId { get; set; }
        public string? UserName { get; set; }
        public string? UserEmail { get; set; }
        public string? Role { get; set; }
        public DateTime? JoinedAt { get; set; }
    }
}