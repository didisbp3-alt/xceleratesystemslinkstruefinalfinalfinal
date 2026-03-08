using System.ComponentModel.DataAnnotations;

namespace XcelerateLinks.Models.ViewModels
{
    public class RegisterViewModel
    {
        [Required]
        [Display(Name = "Nome")]
        [StringLength(100, ErrorMessage = "O nome deve ter até 100 caracteres.")]
        public string Name { get; set; } = "";

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = "";

        [Required]
        [Display(Name = "Utilizador")]
        [StringLength(50, ErrorMessage = "O nome de utilizador deve ter até 50 caracteres.")]
        public string Username { get; set; } = "";

        [Display(Name = "Telefone")]
        [Phone]
        public string? PhoneNumber { get; set; }

        [Display(Name = "Nacionalidade")]
        public int? Nationality { get; set; }

        [Display(Name = "Preferência de emprego")]
        public int? JobPreference { get; set; }

        [Display(Name = "Bio do perfil")]
        [StringLength(500, ErrorMessage = "A bio deve ter até 500 caracteres.")]
        public string? Profile_Bio { get; set; }

        [Display(Name = "Data de nascimento")]
        [DataType(DataType.Date)]
        public DateTime? DoB { get; set; }

        [Display(Name = "Função")]
        public int? Role { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Senha")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "A senha deve ter pelo menos 6 caracteres.")]
        public string Password { get; set; } = "";

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Confirmar senha")]
        [Compare("Password", ErrorMessage = "As senhas não coincidem.")]
        public string ConfirmPassword { get; set; } = "";
    }
}

