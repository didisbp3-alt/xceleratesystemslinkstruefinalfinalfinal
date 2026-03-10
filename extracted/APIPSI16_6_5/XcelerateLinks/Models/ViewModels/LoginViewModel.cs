using System.ComponentModel.DataAnnotations;

namespace XcelerateLinks.Models.ViewModels
{
    public class LoginViewModel
    {
        [Required]
        [Display(Name = "Utilizador")]
        public string Username { get; set; } = "";

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Senha")]
        public string Password { get; set; } = "";

        [Display(Name = "Lembrar-me")]
        public bool RememberMe { get; set; } = false;

        // used to redirect back after login; view contains a hidden input for this
        public string? ReturnUrl { get; set; }
    }
}