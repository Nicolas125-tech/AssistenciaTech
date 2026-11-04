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
}
