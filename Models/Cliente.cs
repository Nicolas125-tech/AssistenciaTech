using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AssistenciaTech.Models
{
    /// <summary>
    /// Modelo que representa um Cliente da assistência técnica.
    /// </summary>
    public class Cliente : ClienteBase
    {
        [Key]
        public int Id { get; set; }

        // Um cliente pode ter várias ordens de serviço
        public List<OrdemServico> OrdensServico { get; set; } = new List<OrdemServico>();
    }
}
