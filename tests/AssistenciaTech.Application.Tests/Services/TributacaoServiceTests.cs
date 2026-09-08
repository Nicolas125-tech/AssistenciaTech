using System;
using AssistenciaTech.Models;
using AssistenciaTech.Services;
using FluentAssertions;
using Xunit;

namespace AssistenciaTech.Application.Tests.Services
{
    public class TributacaoServiceTests
    {
        private readonly TributacaoService _sut;

        public TributacaoServiceTests()
        {
            _sut = new TributacaoService();
        }

        [Fact]
        public void CalcularTributos_WithValidOrdemServico_CalculatesCorrectly()
        {
            // Arrange
            var ordemServico = new OrdemServico
            {
                CustoMaoDeObra = 100m,
                CustoPecas = 200m
            };

            // Act
            var result = _sut.CalcularTributos(ordemServico);

            // Assert
            result.Should().NotBeNull();

            result.BaseCalculoISS.Should().Be(100m);
            result.AliquotaISS.Should().Be(0.05m);
            result.ValorISS.Should().Be(5m); // 100 * 0.05

            result.BaseCalculoICMS.Should().Be(200m);
            result.AliquotaICMS.Should().Be(0.18m);
            result.ValorICMS.Should().Be(36m); // 200 * 0.18
        }

        [Fact]
        public void CalcularTributos_WithZeroCosts_ReturnsZeroTaxes()
        {
            // Arrange
            var ordemServico = new OrdemServico
            {
                CustoMaoDeObra = 0m,
                CustoPecas = 0m
            };

            // Act
            var result = _sut.CalcularTributos(ordemServico);

            // Assert
            result.Should().NotBeNull();
            result.ValorISS.Should().Be(0m);
            result.ValorICMS.Should().Be(0m);
        }

        [Fact]
        public void CalcularTributos_WithFractionalValues_RoundsToTwoDecimalPlaces()
        {
            // Arrange
            var ordemServico = new OrdemServico
            {
                CustoMaoDeObra = 100.33m, // 100.33 * 0.05 = 5.0165 -> 5.02
                CustoPecas = 100.55m // 100.55 * 0.18 = 18.099 -> 18.10
            };

            // Act
            var result = _sut.CalcularTributos(ordemServico);

            // Assert
            result.Should().NotBeNull();
            result.ValorISS.Should().Be(5.02m);
            result.ValorICMS.Should().Be(18.10m);
        }

        [Fact]
        public void CalcularTributos_WithNullOrdemServico_ThrowsArgumentNullException()
        {
            // Arrange
            OrdemServico? ordemServico = null;

            // Act
            Action act = () => _sut.CalcularTributos(ordemServico!);

            // Assert
            act.Should().Throw<ArgumentNullException>()
               .WithParameterName("ordemServico");
        }
    }
}
