using System.ComponentModel.DataAnnotations;

namespace AssistenciaTech.DTOs
{
    public class ClienteCreateDto
    {
        [Required(ErrorMessage = "O nome é obrigatório.")]
        [StringLength(100)]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "O CPF é obrigatório para consulta.")]
        [StringLength(14, MinimumLength = 11, ErrorMessage = "CPF inválido.")]
        public string Cpf { get; set; } = string.Empty;

        [Required(ErrorMessage = "O telefone é obrigatório.")]
        [Phone]
        public string Telefone { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "E-mail inválido.")]
        public string? Email { get; set; }

        [Display(Name = "Telegram Chat ID")]
        public string? TelegramChatId { get; set; }
    }

    public class ClienteUpdateDto
    {
        [Required]
        public int Id { get; set; }

        [Required(ErrorMessage = "O nome é obrigatório.")]
        [StringLength(100)]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "O CPF é obrigatório para consulta.")]
        [StringLength(14, MinimumLength = 11, ErrorMessage = "CPF inválido.")]
        public string Cpf { get; set; } = string.Empty;

        [Required(ErrorMessage = "O telefone é obrigatório.")]
        [Phone]
        public string Telefone { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "E-mail inválido.")]
        public string? Email { get; set; }

        [Display(Name = "Telegram Chat ID")]
        public string? TelegramChatId { get; set; }
    }
}
