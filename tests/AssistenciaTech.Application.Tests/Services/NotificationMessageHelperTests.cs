using System;
using AssistenciaTech.Models;
using AssistenciaTech.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace AssistenciaTech.Application.Tests.Services
{
    public class NotificationMessageHelperTests
    {
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly Cliente _cliente;
        private readonly OrdemServico _os;

        public NotificationMessageHelperTests()
        {
            _configurationMock = new Mock<IConfiguration>();

            _cliente = new Cliente
            {
                Nome = "João Silva",
                Cpf = "12345678901"
            };

            _os = new OrdemServico
            {
                Id = 100,
                Equipamento = "Notebook Dell",
                ValorOrcamento = 150.50m,
                Status = "Pronto"
            };
        }

        [Fact]
        public void GerarMensagem_WithSpecificTemplate_ShouldUseSpecificTemplate()
        {
            // Arrange
            _configurationMock.Setup(c => c["NotificationTemplates:Pronto"])
                .Returns("Específico: {0}, OS {2}");

            // Act
            var result = NotificationMessageHelper.GerarMensagem(_configurationMock.Object, _cliente, _os, "Em Análise");

            // Assert
            result.Should().Be("Específico: João Silva, OS 100");
        }

        [Fact]
        public void GerarMensagem_WithNoSpecificTemplate_ShouldUseDefaultTemplate()
        {
            // Arrange
            _configurationMock.Setup(c => c["NotificationTemplates:Pronto"]).Returns((string?)null);
            _configurationMock.Setup(c => c["NotificationTemplates:Default"])
                .Returns("Padrão: {0}, OS {2}");

            // Act
            var result = NotificationMessageHelper.GerarMensagem(_configurationMock.Object, _cliente, _os, "Em Análise");

            // Assert
            result.Should().Be("Padrão: João Silva, OS 100");
        }

        [Fact]
        public void GerarMensagem_WithNoTemplateAtAll_ShouldUseHardcodedFallback()
        {
            // Arrange
            _configurationMock.Setup(c => c["NotificationTemplates:Pronto"]).Returns((string?)null);
            _configurationMock.Setup(c => c["NotificationTemplates:Default"]).Returns((string?)null);

            // Act
            var result = NotificationMessageHelper.GerarMensagem(_configurationMock.Object, _cliente, _os, "Em Análise");

            // Assert
            result.Should().Be("Olá João Silva, o status da sua OS #100 (Notebook Dell) foi atualizado de 'Em Análise' para 'Pronto'.");
        }

        [Theory]
        [InlineData("Cancelado")]
        [InlineData("Entregue")]
        public void GerarMensagem_ShouldFormatMessageCorrectly_ForDifferentStatuses(string novoStatus)
        {
            // Arrange
            _os.Status = novoStatus;
            _configurationMock.Setup(c => c[$"NotificationTemplates:{novoStatus}"])
                .Returns("Status da OS {2} mudou de {4} para {5}. Valor: {3}");

            // Act
            var result = NotificationMessageHelper.GerarMensagem(_configurationMock.Object, _cliente, _os, "Pronto");

            // Assert
            result.Should().Contain("Status da OS 100 mudou de Pronto");
            result.Should().Contain($"para {novoStatus}");
            // Use Contains to prevent brittle localization issues with currency
            result.Should().Contain("150");
        }
    }
}
