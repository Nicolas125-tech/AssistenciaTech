using System;
using Xunit;
using FluentAssertions;
using Moq;
using AssistenciaTech.Domain.Entities;
using AssistenciaTech.Domain.Interfaces;
using AssistenciaTech.Domain.States;

namespace AssistenciaTech.Domain.Tests;

public class OrdemServicoTests
{
    [Fact]
    public void Constructor_DeveInicializarPropriedadesEEstado()
    {
        var os = new OrdemServico("OS-001", "123", "Modelo", "NS123", "Defeito");
        os.Id.Should().NotBeEmpty();
        os.NumeroOS.Should().Be("OS-001");
        os.ClientCpf.Should().Be("123");
        os.EquipamentoModelo.Should().Be("Modelo");
        os.NumeroSerie.Should().Be("NS123");
        os.DefeitoRelatado.Should().Be("Defeito");
        os.EstadoAtual.Should().BeOfType<RecebidoState>();
        os.DataEntrada.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void DefinirEstado_DeveAtualizarEstado()
    {
        var os = new OrdemServico("OS-001", "123", "Modelo", "NS123", "Defeito");
        var novoEstado = new EmAnaliseState();
        os.DefinirEstado(novoEstado);
        os.EstadoAtual.Should().Be(novoEstado);
    }

    [Fact]
    public void DefinirDiagnostico_Valido_DeveAtualizarDiagnosticoEOrcamento()
    {
        var os = new OrdemServico("OS-001", "123", "Modelo", "NS123", "Defeito");
        os.DefinirDiagnostico("Diagnostico", 150.0m);
        os.DiagnosticoTecnico.Should().Be("Diagnostico");
        os.ValorOrcamento.Should().Be(150.0m);
    }

    [Fact]
    public void DefinirDiagnostico_Vazio_DeveLancarArgumentException()
    {
        var os = new OrdemServico("OS-001", "123", "Modelo", "NS123", "Defeito");
        Action action = () => os.DefinirDiagnostico(" ", 150.0m);
        action.Should().Throw<ArgumentException>().WithMessage("O diagnóstico técnico não pode ser vazio.");
    }

    [Fact]
    public void DefinirDataSaida_DeveAtualizarDataSaida()
    {
        var os = new OrdemServico("OS-001", "123", "Modelo", "NS123", "Defeito");
        os.DefinirDataSaida();
        os.DataSaida.Should().NotBeNull();
        os.DataSaida.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void DefinirCancelamento_DeveAtualizarMotivo()
    {
        var os = new OrdemServico("OS-001", "123", "Modelo", "NS123", "Defeito");
        os.DefinirCancelamento("Motivo");
        os.MotivoCancelamento.Should().Be("Motivo");
    }

    [Fact]
    public void AvancarStatus_DeveChamarAvancarNoEstado()
    {
        var os = new OrdemServico("OS-001", "123", "Modelo", "NS123", "Defeito");
        var estadoMock = new Mock<IOrdemServicoState>();
        os.DefinirEstado(estadoMock.Object);
        os.AvancarStatus();
        estadoMock.Verify(x => x.Avancar(os), Times.Once);
    }

    [Fact]
    public void CancelarOS_DeveChamarCancelarNoEstado()
    {
        var os = new OrdemServico("OS-001", "123", "Modelo", "NS123", "Defeito");
        var estadoMock = new Mock<IOrdemServicoState>();
        os.DefinirEstado(estadoMock.Object);
        os.CancelarOS("Motivo");
        estadoMock.Verify(x => x.Cancelar(os, "Motivo"), Times.Once);
    }
}
