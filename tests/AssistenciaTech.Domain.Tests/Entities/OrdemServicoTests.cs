using System;
using AssistenciaTech.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace AssistenciaTech.Domain.Tests.Entities;

public class OrdemServicoTests
{
    private OrdemServico CreateDefaultOrdemServico()
    {
        return new OrdemServico(
            numeroOS: "OS-2023-001",
            clientCpf: "123.456.789-00",
            equipamentoModelo: "Dell Inspiron",
            numeroSerie: "SN123456",
            defeitoRelatado: "Não liga"
        );
    }

    [Fact]
    public void DefinirDiagnostico_ComDadosValidos_DeveAtualizarPropriedades()
    {
        // Arrange
        var os = CreateDefaultOrdemServico();
        var diagnosticoEsperado = "Placa mãe em curto. Necessário substituição de componentes.";
        var orcamentoEsperado = 450.50m;

        // Act
        os.DefinirDiagnostico(diagnosticoEsperado, orcamentoEsperado);

        // Assert
        os.DiagnosticoTecnico.Should().Be(diagnosticoEsperado);
        os.ValorOrcamento.Should().Be(orcamentoEsperado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DefinirDiagnostico_ComDiagnosticoInvalido_DeveLancarArgumentException(string? diagnosticoInvalido)
    {
        // Arrange
        var os = CreateDefaultOrdemServico();
        var orcamento = 100m;

        // Act
        Action act = () => os.DefinirDiagnostico(diagnosticoInvalido!, orcamento);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("O diagnóstico técnico não pode ser vazio.");
    }
}
