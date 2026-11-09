using System;
using Xunit;
using FluentAssertions;
using AssistenciaTech.Models;

namespace AssistenciaTech.Data.Tests.Models;

public class OrdemServicoTest
{
    [Fact]
    public void DataVencimentoGarantia_QuandoDataEntregaNula_DeveSerNula()
    {
        // Arrange
        var os = new OrdemServico
        {
            DataEntregaCliente = null
        };

        // Act
        var vencimentoGarantia = os.DataVencimentoGarantia;

        // Assert
        vencimentoGarantia.Should().BeNull();
    }

    [Fact]
    public void DataVencimentoGarantia_QuandoDataEntregaPreenchida_DeveAdicionar90Dias()
    {
        // Arrange
        var dataEntrega = new DateTime(2023, 1, 1);
        var os = new OrdemServico
        {
            DataEntregaCliente = dataEntrega
        };

        // Act
        var vencimentoGarantia = os.DataVencimentoGarantia;

        // Assert
        vencimentoGarantia.Should().Be(dataEntrega.AddDays(90));
    }

    [Fact]
    public void GarantiaAtiva_QuandoDataEntregaNula_DeveSerFalso()
    {
        // Arrange
        var os = new OrdemServico
        {
            DataEntregaCliente = null
        };

        // Act
        var garantiaAtiva = os.GarantiaAtiva;

        // Assert
        garantiaAtiva.Should().BeFalse();
    }

    [Fact]
    public void GarantiaAtiva_QuandoVencimentoNoFuturo_DeveSerVerdadeiro()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var os = new OrdemServico
        {
            DataEntregaCliente = today.AddDays(-89)
        };

        // Act
        var garantiaAtiva = os.GarantiaAtiva;

        // Assert
        if (DateTime.UtcNow.Date != today) return; // Evita flake na virada de dia
        garantiaAtiva.Should().BeTrue();
    }

    [Fact]
    public void GarantiaAtiva_QuandoVencimentoExatamenteHoje_DeveSerVerdadeiro()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var os = new OrdemServico
        {
            DataEntregaCliente = today.AddDays(-90)
        };

        // Act
        var garantiaAtiva = os.GarantiaAtiva;

        // Assert
        if (DateTime.UtcNow.Date != today) return; // Evita flake na virada de dia
        garantiaAtiva.Should().BeTrue();
    }

    [Fact]
    public void GarantiaAtiva_QuandoVencimentoNoPassado_DeveSerFalso()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var os = new OrdemServico
        {
            DataEntregaCliente = today.AddDays(-91)
        };

        // Act
        var garantiaAtiva = os.GarantiaAtiva;

        // Assert
        if (DateTime.UtcNow.Date != today) return; // Evita flake na virada de dia
        garantiaAtiva.Should().BeFalse();
    }

}
