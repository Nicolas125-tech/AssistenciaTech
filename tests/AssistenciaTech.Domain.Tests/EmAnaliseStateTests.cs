using System;
using Xunit;
using AssistenciaTech.Domain.Entities;
using AssistenciaTech.Domain.States;
using FluentAssertions;
using System.Reflection;

namespace AssistenciaTech.Domain.Tests;

public class EmAnaliseStateTests
{
    [Fact]
    public void Avancar_ComDiagnosticoValido_DeveMudarParaProntoState()
    {
        // Arrange
        var os = new OrdemServico("OS-001", "123.456.789-00", "Modelo", "NS123", "Defeito");
        os.DefinirEstado(new EmAnaliseState());
        os.DefinirDiagnostico("Diagnóstico OK", 100.0m);

        // Act
        os.AvancarStatus();

        // Assert
        os.EstadoAtual.Should().BeOfType<ProntoState>();
    }

    [Fact]
    public void Avancar_SemDiagnostico_DeveLancarExcecao()
    {
        // Arrange
        var os = new OrdemServico("OS-001", "123.456.789-00", "Modelo", "NS123", "Defeito");
        os.DefinirEstado(new EmAnaliseState());
        // Por padrão, DiagnosticoTecnico é nulo na criação

        // Act
        Action act = () => os.AvancarStatus();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Não é possível aprovar ou finalizar uma OS sem o diagnóstico técnico.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Avancar_ComDiagnosticoVazioOuEspacos_DeveLancarExcecao(string diagnosticoVazio)
    {
        // Arrange
        var os = new OrdemServico("OS-001", "123.456.789-00", "Modelo", "NS123", "Defeito");
        os.DefinirEstado(new EmAnaliseState());

        // Usando reflection para forçar um valor vazio/espaços,
        // já que DefinirDiagnostico lança ArgumentException e não permite setar vazio
        typeof(OrdemServico).GetProperty(nameof(OrdemServico.DiagnosticoTecnico))!
            .SetValue(os, diagnosticoVazio);

        // Act
        Action act = () => os.AvancarStatus();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Não é possível aprovar ou finalizar uma OS sem o diagnóstico técnico.");
    }
}
