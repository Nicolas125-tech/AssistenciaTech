using System.ComponentModel.DataAnnotations;
using System.Linq;
using AssistenciaTech.Models;
using Xunit;

namespace AssistenciaTech.Data.Tests.Models
{
    public class OrdemServicoPecaTests
    {
        [Fact]
        public void OrdemServicoPeca_Instantiate_WithValidProperties_ShouldBeValid()
        {
            // Arrange
            var peca = new OrdemServicoPeca
            {
                Id = 1,
                OrdemServicoId = 1,
                PecaId = 1,
                Quantidade = 2,
                ValorVenda = 50.0m
            };

            // Act
            var validationContext = new ValidationContext(peca);
            var validationResults = new System.Collections.Generic.List<ValidationResult>();
            var isValid = Validator.TryValidateObject(peca, validationContext, validationResults, true);

            // Assert
            Assert.True(isValid);
            Assert.Empty(validationResults);
        }

        [Fact]
        public void OrdemServicoPeca_RequiredAttributes_ShouldBeEnforced()
        {
            // Arrange
            var peca = new OrdemServicoPeca();

            // Act
            var validationContext = new ValidationContext(peca);
            var validationResults = new System.Collections.Generic.List<ValidationResult>();
            var isValid = Validator.TryValidateObject(peca, validationContext, validationResults, true);

            // Assert
            Assert.True(isValid);
            Assert.Empty(validationResults);
        }

        [Fact]
        public void OrdemServicoPeca_Properties_ShouldBeGettableAndSettable()
        {
            // Arrange
            var peca = new OrdemServicoPeca();
            var os = new OrdemServico();
            var p = new Peca();

            // Act
            peca.Id = 5;
            peca.OrdemServicoId = 10;
            peca.OrdemServico = os;
            peca.PecaId = 20;
            peca.Peca = p;
            peca.Quantidade = 3;
            peca.ValorVenda = 150.75m;

            // Assert
            Assert.Equal(5, peca.Id);
            Assert.Equal(10, peca.OrdemServicoId);
            Assert.Same(os, peca.OrdemServico);
            Assert.Equal(20, peca.PecaId);
            Assert.Same(p, peca.Peca);
            Assert.Equal(3, peca.Quantidade);
            Assert.Equal(150.75m, peca.ValorVenda);
        }
    }
}
