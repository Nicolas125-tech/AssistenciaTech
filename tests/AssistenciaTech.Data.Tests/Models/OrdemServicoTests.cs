using System;
using AssistenciaTech.Models;
using Xunit;
using FluentAssertions;

namespace AssistenciaTech.Data.Tests.Models
{
    public class OrdemServicoTests
    {
        [Theory]
        [InlineData(100.0, 50.0, 20.0, 130.0)]
        [InlineData(0, 0, 0, 0)]
        [InlineData(100.0, 50.0, 0, 150.0)]
        [InlineData(100.0, 50.0, 150.0, 0)]
        [InlineData(100.0, 50.0, 200.0, -50.0)]
        public void ValorTotalCalculado_DeveCalcularCorretamente(decimal custoPecas, decimal custoMaoDeObra, decimal descontoAplicado, decimal valorTotalEsperado)
        {
            // Arrange
            var os = new OrdemServico
            {
                CustoPecas = custoPecas,
                CustoMaoDeObra = custoMaoDeObra,
                DescontoAplicado = descontoAplicado
            };

            // Act
            var valorTotal = os.ValorTotalCalculado;

            // Assert
            valorTotal.Should().Be(valorTotalEsperado);
        }
    }
}
