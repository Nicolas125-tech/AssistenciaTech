using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AssistenciaTech.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

namespace AssistenciaTech.Infrastructure.Tests.Services
{
    public class TelegramBotApiTransportTests : IDisposable
    {
        private readonly Mock<HttpMessageHandler> _handlerMock;
        private readonly HttpClient _httpClient;
        private readonly Mock<ILogger<TelegramBotApiTransport>> _loggerMock;

        public TelegramBotApiTransportTests()
        {
            _handlerMock = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_handlerMock.Object);
            _loggerMock = new Mock<ILogger<TelegramBotApiTransport>>();
        }

        private TelegramBotApiTransport CreateSut(string? botToken = "valid_token", int maxRetries = 2)
        {
            var options = Options.Create(new MensageriaOptions
            {
                BotToken = botToken,
                MaxRetries = maxRetries,
                RetryBaseDelayMs = 0
            });
            return new TelegramBotApiTransport(_httpClient, options, _loggerMock.Object);
        }

        private List<HttpContent?> SetupHttpResponses(Func<int, Task<HttpResponseMessage>> responseFactory)
        {
            var capturedContents = new List<HttpContent?>();
            int callIndex = 0;

            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns<HttpRequestMessage, CancellationToken>((req, _) =>
                {
                    callIndex++;
                    capturedContents.Add(req.Content);
                    return responseFactory(callIndex);
                });

            return capturedContents;
        }

        [Fact]
        public async Task EnviarAsync_ComDadosValidos_FazPostParaSendMessageERetornaOk()
        {
            string? capturedBody = null;
            HttpRequestMessage? capturedReq = null;

            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
                {
                    capturedReq = req;
                    capturedBody = req.Content!.ReadAsStringAsync().Result;
                })
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });

            var sut = CreateSut();
            var result = await sut.EnviarAsync(new MensageriaEnvelope
            {
                ChatId = "12345",
                Texto = "OS #10 concluída!",
                Origem = MensageriaOrigem.NotificacaoStatus,
                ClienteId = 1,
                ClienteNome = "Maria"
            });

            result.Sucesso.Should().BeTrue();
            result.StatusCode.Should().Be(200);
            result.Tentativas.Should().Be(1);
            result.BloqueadoPeloUsuario.Should().BeFalse();

            capturedReq!.Method.Should().Be(HttpMethod.Post);
            capturedReq.RequestUri!.ToString().Should().Be("https://api.telegram.org/botvalid_token/sendMessage");
            capturedBody.Should().Contain("\"chat_id\":\"12345\"");
            capturedBody.Should().Contain("OS #10 conclu");
        }

        [Theory]
        [InlineData(null, "12345")]
        [InlineData("", "12345")]
        [InlineData("valid_token", null)]
        [InlineData("valid_token", "")]
        public async Task EnviarAsync_SemTokenOuChatId_NaoChamaHttpERetornaFalha(string? token, string? chatId)
        {
            var sut = CreateSut(botToken: token);

            var result = await sut.EnviarAsync(new MensageriaEnvelope
            {
                ChatId = chatId,
                Texto = "Teste",
                Origem = MensageriaOrigem.NotificacaoStatus
            });

            result.Sucesso.Should().BeFalse();
            result.Tentativas.Should().Be(0);
            _handlerMock.Protected().Verify(
                "SendAsync",
                Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task EnviarAsync_QuandoApiRetorna429Depois200_FazRetryComNovoConteudoERecupera()
        {
            var contents = SetupHttpResponses(call => Task.FromResult(call == 1
                ? new HttpResponseMessage((HttpStatusCode)429) { Content = new StringContent("Too Many Requests") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") }));

            var sut = CreateSut();
            var result = await sut.EnviarAsync(new MensageriaEnvelope
            {
                ChatId = "12345",
                Texto = "Tentativa com retry",
                Origem = MensageriaOrigem.RespostaBot
            });

            result.Sucesso.Should().BeTrue();
            result.StatusCode.Should().Be(200);
            result.Tentativas.Should().Be(2);
            contents.Should().HaveCount(2);
            ReferenceEquals(contents[0], contents[1]).Should().BeFalse();
        }

        [Fact]
        public async Task EnviarAsync_QuandoApiRetorna500Persistente_EsgotaRetriesERetornaFalhaSemLancar()
        {
            var contents = SetupHttpResponses(_ => Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("Server Error") }));

            var sut = CreateSut(maxRetries: 2);
            var result = await sut.EnviarAsync(new MensageriaEnvelope
            {
                ChatId = "12345",
                Texto = "Teste 500",
                Origem = MensageriaOrigem.NotificacaoStatus
            });

            result.Sucesso.Should().BeFalse();
            result.StatusCode.Should().Be(500);
            result.Tentativas.Should().Be(3);
            result.BloqueadoPeloUsuario.Should().BeFalse();
            contents.Should().HaveCount(3);
        }

        [Fact]
        public async Task EnviarAsync_QuandoOcorreHttpRequestException_EsgotaRetriesERetornaFalhaSemLancar()
        {
            var contents = SetupHttpResponses(_ =>
                Task.FromException<HttpResponseMessage>(new HttpRequestException("Network failure")));

            var sut = CreateSut(maxRetries: 2);
            var result = await sut.EnviarAsync(new MensageriaEnvelope
            {
                ChatId = "12345",
                Texto = "Teste rede",
                Origem = MensageriaOrigem.NotificacaoStatus
            });

            result.Sucesso.Should().BeFalse();
            result.Tentativas.Should().Be(3);
            result.MotivoFalha.Should().Contain("Network failure");
            contents.Should().HaveCount(3);
        }

        [Fact]
        public async Task EnviarAsync_QuandoOcorreTimeoutTransitorio_FazRetryERecupera()
        {
            var contents = SetupHttpResponses(call => call == 1
                ? Task.FromException<HttpResponseMessage>(new TaskCanceledException("HttpClient request timed out"))
                : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") }));

            var sut = CreateSut(maxRetries: 2);
            var result = await sut.EnviarAsync(new MensageriaEnvelope
            {
                ChatId = "12345",
                Texto = "Teste timeout",
                Origem = MensageriaOrigem.NotificacaoStatus
            });

            result.Sucesso.Should().BeTrue();
            result.Tentativas.Should().Be(2);
            contents.Should().HaveCount(2);
        }

        [Fact]
        public async Task EnviarAsync_QuandoApiRetorna403_NaoFazRetryESinalizaBloqueadoPeloUsuario()
        {
            var contents = SetupHttpResponses(_ => Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("Forbidden: bot was blocked by the user") }));

            var sut = CreateSut(maxRetries: 2);
            var result = await sut.EnviarAsync(new MensageriaEnvelope
            {
                ChatId = "12345",
                Texto = "Aviso",
                Origem = MensageriaOrigem.NotificacaoStatus,
                ClienteId = 1,
                ClienteNome = "Cliente"
            });

            result.Sucesso.Should().BeFalse();
            result.StatusCode.Should().Be(403);
            result.BloqueadoPeloUsuario.Should().BeTrue();
            result.Tentativas.Should().Be(1);
            contents.Should().HaveCount(1);
        }

        [Fact]
        public async Task EnviarAsync_QuandoApiRetorna400_NaoFazRetryERetornaFalha()
        {
            var contents = SetupHttpResponses(_ => Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("Bad Request") }));

            var sut = CreateSut(maxRetries: 2);
            var result = await sut.EnviarAsync(new MensageriaEnvelope
            {
                ChatId = "12345",
                Texto = "Teste 400",
                Origem = MensageriaOrigem.RespostaBot
            });

            result.Sucesso.Should().BeFalse();
            result.StatusCode.Should().Be(400);
            result.BloqueadoPeloUsuario.Should().BeFalse();
            result.Tentativas.Should().Be(1);
            contents.Should().HaveCount(1);
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
