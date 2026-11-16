using System;
using System.Threading.Tasks;
using AssistenciaTech.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AssistenciaTech.Application.Tests.Services
{
    public class LogMensageriaTransportTests
    {
        private readonly Mock<ILogger<LogMensageriaTransport>> _loggerMock;
        private readonly LogMensageriaTransport _sut;

        public LogMensageriaTransportTests()
        {
            _loggerMock = new Mock<ILogger<LogMensageriaTransport>>();
            _sut = new LogMensageriaTransport(_loggerMock.Object);
        }

        [Fact]
        public async Task EnviarAsync_RespostaBot_RegistraLogERetornaOk()
        {
            var result = await _sut.EnviarAsync(new MensageriaEnvelope
            {
                ChatId = "chat-15",
                Texto = "Telegram vinculado!",
                Origem = MensageriaOrigem.RespostaBot,
                ClienteId = 15,
                ClienteNome = "João"
            });

            result.Sucesso.Should().BeTrue();
            result.Tentativas.Should().Be(1);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("[LogMensageria][Bot]") && v.ToString()!.Contains("chat-15")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task EnviarAsync_NotificacaoStatusComCanaisDisponiveis_SimulaTodosOsCanaisPreenchidos()
        {
            var result = await _sut.EnviarAsync(new MensageriaEnvelope
            {
                ChatId = "chat-10",
                Telefone = "43999999999",
                Email = "ana@email.com",
                Texto = "Sua OS #50 está concluída.",
                Origem = MensageriaOrigem.NotificacaoStatus,
                ClienteId = 10,
                ClienteNome = "Ana",
                OrdemServicoId = 50
            });

            result.Sucesso.Should().BeTrue();
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("[Telegram/Log]")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("[WhatsApp]")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("[Email]")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task EnviarAsync_NotificacaoStatusSemNenhumCanal_RegistraWarningERetornaFalha()
        {
            var result = await _sut.EnviarAsync(new MensageriaEnvelope
            {
                ChatId = null,
                Telefone = "",
                Email = null,
                Texto = "Mensagem",
                Origem = MensageriaOrigem.NotificacaoStatus,
                ClienteId = 30,
                ClienteNome = "Carlos Sem Contato",
                OrdemServicoId = 99
            });

            result.Sucesso.Should().BeFalse();
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Carlos Sem Contato") && v.ToString()!.Contains("OS #99")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}
