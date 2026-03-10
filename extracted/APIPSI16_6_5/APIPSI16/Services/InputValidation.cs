using System.ComponentModel.DataAnnotations;

namespace APIPSI16.Services
{
    public static class InputValidation
    {
        public const string PhonePattern = @"^\+?[\d\s\-(). ]{7,20}$";

        public static bool IsValidPhone(string? phone) =>
            string.IsNullOrWhiteSpace(phone) ||
            System.Text.RegularExpressions.Regex.IsMatch(phone, PhonePattern);

        public static bool IsValidEmail(string? email) =>
            string.IsNullOrWhiteSpace(email) ||
            new EmailAddressAttribute().IsValid(email);
    }
}
