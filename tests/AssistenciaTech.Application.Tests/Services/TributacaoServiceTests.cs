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
        public void CalcularTributos_WithNullOrdemServico_ThrowsArgumentNullException()
        {
            // Arrange
            OrdemServico ordemServico = null!;

            // Act
            Action act = () => _sut.CalcularTributos(ordemServico);

            // Assert
            act.Should().Throw<ArgumentNullException>()
               .WithParameterName("ordemServico");
        }

        [Fact]
        public void CalcularTributos_WithValidOrdemServico_CalculatesTributosCorrectly()
        {
            // Arrange
            var ordemServico = new OrdemServico
            {
                CustoMaoDeObra = 100m, // ISS = 5% of 100 = 5
                CustoPecas = 200m     // ICMS = 18% of 200 = 36
            };

            // Act
            var result = _sut.CalcularTributos(ordemServico);

            // Assert
            result.Should().NotBeNull();

            result.BaseCalculoISS.Should().Be(100m);
            result.AliquotaISS.Should().Be(0.05m);
            result.ValorISS.Should().Be(5m);

            result.BaseCalculoICMS.Should().Be(200m);
            result.AliquotaICMS.Should().Be(0.18m);
            result.ValorICMS.Should().Be(36m);
        }

        [Fact]
        public void CalcularTributos_WithFractionalValues_RoundsCorrectly()
        {
            // Arrange
            var ordemServico = new OrdemServico
            {
                CustoMaoDeObra = 123.45m, // ISS = 5% of 123.45 = 6.1725 -> 6.17
                CustoPecas = 234.56m      // ICMS = 18% of 234.56 = 42.2208 -> 42.22
            };

            // Act
            var result = _sut.CalcularTributos(ordemServico);

            // Assert
            result.ValorISS.Should().Be(6.17m);
            result.ValorICMS.Should().Be(42.22m);
        }
    }
}
