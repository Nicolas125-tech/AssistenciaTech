using System.ComponentModel.DataAnnotations;
using AssistenciaTech.Models;

namespace AssistenciaTech.DTOs
{
    public class ClienteCreateDto : ClienteBase
    {
    }

    public class ClienteUpdateDto : ClienteBase
    {
        [Required]
        public int Id { get; set; }
    }
}
