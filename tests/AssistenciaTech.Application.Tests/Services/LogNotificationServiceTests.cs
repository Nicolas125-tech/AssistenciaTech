using System;
using System.Threading.Tasks;
using AssistenciaTech.Models;
using AssistenciaTech.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AssistenciaTech.Application.Tests.Services
{
    public class LogNotificationServiceTests
    {
        private readonly Mock<ILogger<LogNotificationService>> _loggerMock;
        private readonly LogNotificationService _sut;

        public LogNotificationServiceTests()
        {
            _loggerMock = new Mock<ILogger<LogNotificationService>>();
            _sut = new LogNotificationService(_loggerMock.Object);
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
    }
}
