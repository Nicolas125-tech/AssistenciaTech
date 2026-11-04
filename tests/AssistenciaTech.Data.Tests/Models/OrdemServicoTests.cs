using System;
using Xunit;
using FluentAssertions;
using AssistenciaTech.Models;

namespace AssistenciaTech.Data.Tests.Models
{
    public class OrdemServicoTests
    {
        [Fact]
        public void ValorComissao_TecnicoResponsavelIsNull_ReturnsZero()
        {
            // Arrange
            var os = new OrdemServico
            {
                TecnicoResponsavel = null,
                CustoMaoDeObra = 100m
            };

            // Act
            var comissao = os.ValorComissao;

            // Assert
            comissao.Should().Be(0m);
        }

        [Fact]
        public void ValorComissao_TecnicoResponsavelIsNotNull_ReturnsCorrectCommission()
        {
            // Arrange
            var os = new OrdemServico
            {
                TecnicoResponsavel = new Tecnico { PercentualComissao = 10m },
                CustoMaoDeObra = 150m
            };

            // Act
            var comissao = os.ValorComissao;

            // Assert
            comissao.Should().Be(15m);
        }
    }
}
