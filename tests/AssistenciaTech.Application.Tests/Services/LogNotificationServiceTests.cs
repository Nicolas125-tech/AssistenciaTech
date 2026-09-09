using System;
using System.Threading.Tasks;
using AssistenciaTech.Models;
using AssistenciaTech.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AssistenciaTech.Application.Tests.Services
{
    public class LogNotificationServiceTests
    {
        private readonly Mock<ILogger<LogNotificationService>> _loggerMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly LogNotificationService _sut;

        public LogNotificationServiceTests()
        {
            _loggerMock = new Mock<ILogger<LogNotificationService>>();
            _configurationMock = new Mock<IConfiguration>();

            // Setup default templates to keep tests passing exactly as before
            _configurationMock.Setup(c => c["NotificationTemplates:Recebido"]).Returns("Olá {0}, seu equipamento '{1}' foi recebido na assistência técnica. OS #{2}.");
            _configurationMock.Setup(c => c["NotificationTemplates:Em Análise"]).Returns("Olá {0}, seu equipamento '{1}' (OS #{2}) está sendo analisado pelo nosso técnico.");
            _configurationMock.Setup(c => c["NotificationTemplates:Aguardando Aprovação do Orçamento"]).Returns("Olá {0}, o orçamento da OS #{2} ({1}) está pronto: {3}. Aguardamos sua aprovação.");
            _configurationMock.Setup(c => c["NotificationTemplates:Aguardando Peças"]).Returns("Olá {0}, estamos aguardando a chegada de peças para o reparo do seu equipamento '{1}' (OS #{2}).");
            _configurationMock.Setup(c => c["NotificationTemplates:Em Reparo"]).Returns("Olá {0}, seu equipamento '{1}' (OS #{2}) está em reparo.");
            _configurationMock.Setup(c => c["NotificationTemplates:Concluído"]).Returns("Olá {0}, o reparo do seu equipamento '{1}' (OS #{2}) foi concluído! Valor: {3}. Já está disponível para retirada.");
            _configurationMock.Setup(c => c["NotificationTemplates:Entregue ao Cliente"]).Returns("Olá {0}, confirmamos a entrega do seu equipamento '{1}' (OS #{2}). Obrigado pela preferência!");
            _configurationMock.Setup(c => c["NotificationTemplates:Default"]).Returns("Olá {0}, o status da sua OS #{2} ({1}) foi atualizado de '{4}' para '{5}'.");

            _sut = new LogNotificationService(_loggerMock.Object, _configurationMock.Object);
        }

        [Fact]
        public async Task EnviarNotificacaoStatusAsync_ShouldLogWarning_WhenClienteIsNull()
        {
            // Arrange
            Cliente cliente = null!;
            var os = new OrdemServico { Id = 1 };

            // Act
            await _sut.EnviarNotificacaoStatusAsync(cliente, os, "Anterior");

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Tentativa de notificação para OS #1 sem cliente associado.")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task EnviarNotificacaoStatusAsync_ShouldLogInformation_WhenClienteHasOnlyTelefone()
        {
            // Arrange
            var cliente = new Cliente { Id = 1, Nome = "Test", Telefone = "12345", Email = "" };
            var os = new OrdemServico { Id = 1, Equipamento = "PC", Status = WorkflowStatus.Concluido, ValorOrcamento = 100 };

            // Act
            await _sut.EnviarNotificacaoStatusAsync(cliente, os, "Anterior");

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[WhatsApp] Notificação enviada para Test")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[Email] Notificação enviada para")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        [Fact]
        public async Task EnviarNotificacaoStatusAsync_ShouldLogInformation_WhenClienteHasOnlyEmail()
        {
            // Arrange
            var cliente = new Cliente { Id = 1, Nome = "Test", Telefone = "", Email = "test@test.com" };
            var os = new OrdemServico { Id = 1, Equipamento = "PC", Status = WorkflowStatus.Concluido, ValorOrcamento = 100 };

            // Act
            await _sut.EnviarNotificacaoStatusAsync(cliente, os, "Anterior");

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[WhatsApp] Notificação enviada para")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[Email] Notificação enviada para Test")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task EnviarNotificacaoStatusAsync_ShouldLogInformationBoth_WhenClienteHasEmailAndTelefone()
        {
            // Arrange
            var cliente = new Cliente { Id = 1, Nome = "Test", Telefone = "12345", Email = "test@test.com" };
            var os = new OrdemServico { Id = 1, Equipamento = "PC", Status = WorkflowStatus.Concluido, ValorOrcamento = 100 };

            // Act
            await _sut.EnviarNotificacaoStatusAsync(cliente, os, "Anterior");

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[WhatsApp] Notificação enviada para Test")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[Email] Notificação enviada para Test")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task EnviarNotificacaoStatusAsync_ShouldLogWarning_WhenClienteHasNeitherEmailNorTelefone()
        {
            // Arrange
            var cliente = new Cliente { Id = 1, Nome = "Test", Telefone = "", Email = "" };
            var os = new OrdemServico { Id = 1, Equipamento = "PC", Status = WorkflowStatus.Concluido, ValorOrcamento = 100 };

            // Act
            await _sut.EnviarNotificacaoStatusAsync(cliente, os, "Anterior");

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[Notificação] Cliente Test (ID: 1) não possui telefone nem e-mail cadastrado")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Notificação enviada para")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Never);
        }

        [Fact]
        public async Task EnviarNotificacaoStatusAsync_ShouldGenerateCorrectMessage_ForRecebido()
        {
            // Arrange
            var cliente = new Cliente { Id = 1, Nome = "Test", Telefone = "123" };
            var os = new OrdemServico { Id = 1, Equipamento = "PC", Status = WorkflowStatus.Recebido };

            // Act
            await _sut.EnviarNotificacaoStatusAsync(cliente, os, "Anterior");

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Olá Test, seu equipamento 'PC' foi recebido na assistência técnica. OS #1.")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task EnviarNotificacaoStatusAsync_ShouldGenerateCorrectMessage_ForDefaultStatus()
        {
            // Arrange
            var cliente = new Cliente { Id = 1, Nome = "Test", Telefone = "123" };
            var os = new OrdemServico { Id = 1, Equipamento = "PC", Status = "Novo Status" };

            // Act
            await _sut.EnviarNotificacaoStatusAsync(cliente, os, "Anterior");

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Olá Test, o status da sua OS #1 (PC) foi atualizado de 'Anterior' para 'Novo Status'.")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Theory]
        [InlineData(WorkflowStatus.EmAnalise, "está sendo analisado pelo nosso técnico")]
        [InlineData(WorkflowStatus.AguardandoAprovacao, "está pronto:")]
        [InlineData(WorkflowStatus.AguardandoPecas, "estamos aguardando a chegada de peças para o reparo")]
        [InlineData(WorkflowStatus.EmReparo, "está em reparo")]
        [InlineData(WorkflowStatus.Concluido, "foi concluído! Valor:")]
        [InlineData(WorkflowStatus.Entregue, "confirmamos a entrega do seu equipamento")]
        public async Task EnviarNotificacaoStatusAsync_ShouldGenerateCorrectMessage_ForAllKnownStatuses(string status, string expectedMessageFragment)
        {
            // Arrange
            var cliente = new Cliente { Id = 1, Nome = "Test", Telefone = "123" };
            var os = new OrdemServico { Id = 1, Equipamento = "PC", Status = status, ValorOrcamento = 100 };

            // Act
            await _sut.EnviarNotificacaoStatusAsync(cliente, os, "Anterior");

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessageFragment)),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
}
}
