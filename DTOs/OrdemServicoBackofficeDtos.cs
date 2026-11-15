using System.Collections.Generic;
using AssistenciaTech.Models;

namespace AssistenciaTech.DTOs
{
    public record SelecaoItemDto(int Id, string Descricao);

    public class OrdemEdicaoLookupsDto
    {
        public List<SelecaoItemDto> Tecnicos { get; init; } = new();
        public List<SelecaoItemDto> EquipamentosBackup { get; init; } = new();
        public List<SelecaoItemDto> Contratos { get; init; } = new();
    }

    public class OrdemEdicaoDto
    {
        public OrdemServico Ordem { get; init; } = null!;
        public OrdemEdicaoLookupsDto Lookups { get; init; } = new();
    }
}
