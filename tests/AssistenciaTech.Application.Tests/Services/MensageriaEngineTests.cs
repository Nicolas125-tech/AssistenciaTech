using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using AssistenciaTech.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace AssistenciaTech.Application.Tests.Services
{
    public class MensageriaEngineTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly Mock<IMensageriaTransport> _transportMock;
        private readonly Mock<ILogger<MensageriaEngine>> _loggerMock;

        public MensageriaEngineTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);
            _transportMock = new Mock<IMensageriaTransport>();
            _transportMock
                .Setup(t => t.EnviarAsync(It.IsAny<MensageriaEnvelope>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(MensageriaDeliveryResult.Ok());
            _loggerMock = new Mock<ILogger<MensageriaEngine>>();
        }

        private MensageriaEngine CreateSut(Action<MensageriaOptions>? configure = null)
        {
            var opts = new MensageriaOptions
            {
                BotToken = "valid-token",
                WebhookSecretToken = "secret-token-123",
                TransportMode = MensageriaTransportMode.Auto,
                Templates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Recebido"] = "Olá {0}, seu equipamento '{1}' foi recebido na assistência técnica. OS #{2}.",
                    ["Em Reparo"] = "Olá {0}, seu equipamento '{1}' (OS #{2}) está em reparo.",
                    ["Concluído"] = "Olá {0}, o reparo do seu equipamento '{1}' (OS #{2}) foi concluído! Valor: {3}. Já está disponível para retirada.",
                    ["Cancelado"] = "Olá {0}, a OS #{2} ({1}) foi cancelada. Em caso de dúvidas, entre em contato com a assistência.",
                    ["Default"] = "Olá {0}, o status da sua OS #{2} ({1}) foi atualizado de '{4}' para '{5}'."
                }
            };
            configure?.Invoke(opts);
            return new MensageriaEngine(_context, _transportMock.Object, Options.Create(opts), _loggerMock.Object);
        }

        private static JsonElement ParseJson(string json)
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }

        [Theory]
        [InlineData(MensageriaTransportMode.Auto, "token-123", true)]
        [InlineData(MensageriaTransportMode.Auto, null, false)]
        [InlineData(MensageriaTransportMode.Auto, "  ", false)]
        [InlineData(MensageriaTransportMode.Telegram, null, true)]
        [InlineData(MensageriaTransportMode.Log, "token-123", false)]
        public void MensageriaOptions_DeveUsarTelegramApi_RespeitaModoEToken(
            MensageriaTransportMode mode,
            string? token,
            bool expectedTelegram)
        {
            var opts = new MensageriaOptions { TransportMode = mode, BotToken = token };
            opts.DeveUsarTelegramApi().Should().Be(expectedTelegram);
        }

        [Fact]
        public void MensageriaOptions_BindFromConfiguration_CarregaSecaoTelegramFallbackTokenETemplates()
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["TelegramBotToken"] = "legacy-root-token",
                    ["Telegram:WebhookSecretToken"] = "hook-sec",
                    ["Telegram:TransportMode"] = "Log",
                    ["Telegram:MaxRetries"] = "4",
                    ["NotificationTemplates:Cancelado"] = "Cancelada {2}"
                })
                .Build();

            var opts = new MensageriaOptions();
            opts.BindFromConfiguration(config);

            opts.BotToken.Should().Be("legacy-root-token");
            opts.WebhookSecretToken.Should().Be("hook-sec");
            opts.TransportMode.Should().Be(MensageriaTransportMode.Log);
            opts.MaxRetries.Should().Be(4);
            opts.Templates["Cancelado"].Should().Be("Cancelada {2}");
            opts.DeveUsarTelegramApi().Should().BeFalse();
        }

        [Fact]
        public async Task EnviarNotificacaoStatusComResultadoAsync_ComClienteNulo_NaoAcionaTransportERetornaFalha()
        {
            var sut = CreateSut();
            var os = new OrdemServico { Id = 1, Equipamento = "PC", Status = WorkflowStatus.Concluido };

            var result = await sut.EnviarNotificacaoStatusComResultadoAsync(null, os, WorkflowStatus.EmReparo);

            result.Sucesso.Should().BeFalse();
            _transportMock.Verify(t => t.EnviarAsync(It.IsAny<MensageriaEnvelope>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task EnviarNotificacaoStatusComResultadoAsync_ComStatusCancelado_UsaTemplateCancelado()
        {
            MensageriaEnvelope? captured = null;
            _transportMock
                .Setup(t => t.EnviarAsync(It.IsAny<MensageriaEnvelope>(), It.IsAny<CancellationToken>()))
                .Callback<MensageriaEnvelope, CancellationToken>((env, _) => captured = env)
                .ReturnsAsync(MensageriaDeliveryResult.Ok());

            var sut = CreateSut();
            var cliente = new Cliente { Id = 10, Nome = "Maria", TelegramChatId = "chat-10", Telefone = "43999", Email = "m@m.com" };
            var os = new OrdemServico { Id = 108, ClienteId = 10, Equipamento = "MacBook", Status = "Cancelado", ValorOrcamento = 200m };

            var result = await sut.EnviarNotificacaoStatusComResultadoAsync(cliente, os, WorkflowStatus.EmReparo);

            result.Sucesso.Should().BeTrue();
            captured.Should().NotBeNull();
            captured!.Origem.Should().Be(MensageriaOrigem.NotificacaoStatus);
            captured.ChatId.Should().Be("chat-10");
            captured.Texto.Should().Contain("OS #108 (MacBook) foi cancelada");
        }

        [Fact]
        public async Task EnviarNotificacaoStatusComResultadoAsync_QuandoTemplateTemMarcadorInvalido_CaiParaMensagemSeguraSemLancar()
        {
            MensageriaEnvelope? captured = null;
            _transportMock
                .Setup(t => t.EnviarAsync(It.IsAny<MensageriaEnvelope>(), It.IsAny<CancellationToken>()))
                .Callback<MensageriaEnvelope, CancellationToken>((env, _) => captured = env)
                .ReturnsAsync(MensageriaDeliveryResult.Ok());

            var sut = CreateSut(o =>
            {
                o.Templates["Concluído"] = "Template quebrado {9}";
            });

            var cliente = new Cliente { Id = 11, Nome = "João", TelegramChatId = "chat-11" };
            var os = new OrdemServico { Id = 104, ClienteId = 11, Equipamento = "Dell G15", Status = WorkflowStatus.Concluido, ValorOrcamento = 450m };

            var result = await sut.EnviarNotificacaoStatusComResultadoAsync(cliente, os, WorkflowStatus.EmReparo);

            result.Sucesso.Should().BeTrue();
            captured!.Texto.Should().Contain("Olá João, o status da sua OS #104 (Dell G15) foi atualizado de 'Em Reparo' para 'Concluído'.");
        }

        [Fact]
        public async Task EnviarNotificacaoStatusComResultadoAsync_ComStatusDesconhecido_UsaTemplateDefault()
        {
            MensageriaEnvelope? captured = null;
            _transportMock
                .Setup(t => t.EnviarAsync(It.IsAny<MensageriaEnvelope>(), It.IsAny<CancellationToken>()))
                .Callback<MensageriaEnvelope, CancellationToken>((env, _) => captured = env)
                .ReturnsAsync(MensageriaDeliveryResult.Ok());

            var sut = CreateSut();
            var cliente = new Cliente { Id = 13, Nome = "Paulo", TelegramChatId = "chat-13" };
            var os = new OrdemServico { Id = 120, ClienteId = 13, Equipamento = "iMac", Status = "Aguardando Retirada", ValorOrcamento = 300m };

            await sut.EnviarNotificacaoStatusAsync(cliente, os, "Em Reparo");

            captured!.Texto.Should().Be("Olá Paulo, o status da sua OS #120 (iMac) foi atualizado de 'Em Reparo' para 'Aguardando Retirada'.");
        }

        [Fact]
        public async Task EnviarNotificacaoStatusComResultadoAsync_SemTemplatesConfigurados_UsaMensagemPadraoInterna()
        {
            MensageriaEnvelope? captured = null;
            _transportMock
                .Setup(t => t.EnviarAsync(It.IsAny<MensageriaEnvelope>(), It.IsAny<CancellationToken>()))
                .Callback<MensageriaEnvelope, CancellationToken>((env, _) => captured = env)
                .ReturnsAsync(MensageriaDeliveryResult.Ok());

            var sut = CreateSut(o => o.Templates.Clear());
            var cliente = new Cliente { Id = 14, Nome = "Bia", TelegramChatId = "chat-14" };
            var os = new OrdemServico { Id = 121, ClienteId = 14, Equipamento = "Monitor", Status = "Recebido" };

            await sut.EnviarNotificacaoStatusAsync(cliente, os, "Inicial");

            captured!.Texto.Should().Be("Olá Bia, o status da sua OS #121 (Monitor) foi atualizado de 'Inicial' para 'Recebido'.");
        }

        [Fact]
        public async Task EnviarNotificacaoStatusComResultadoAsync_QuandoTransportSinaliza403Bloqueado_RegistraWarningENaoAlteraBanco()
        {
            var cliente = new Cliente { Id = 12, Nome = "Lucas", TelegramChatId = "chat-bloqueado" };
            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            _transportMock
                .Setup(t => t.EnviarAsync(It.IsAny<MensageriaEnvelope>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(MensageriaDeliveryResult.Falha("Forbidden", statusCode: 403, bloqueadoPeloUsuario: true, tentativas: 1));

            var sut = CreateSut();
            var os = new OrdemServico { Id = 110, ClienteId = 12, Equipamento = "Lenovo", Status = WorkflowStatus.Concluido };

            var result = await sut.EnviarNotificacaoStatusComResultadoAsync(cliente, os, WorkflowStatus.EmReparo);

            result.Sucesso.Should().BeFalse();
            result.BloqueadoPeloUsuario.Should().BeTrue();
            (await _context.Clientes.FindAsync(12))!.TelegramChatId.Should().Be("chat-bloqueado");

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("bloqueou o bot")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Theory]
        [InlineData(null, "secret-token-123")]
        [InlineData("", "secret-token-123")]
        [InlineData("errado", "secret-token-123")]
        [InlineData("secret-token-123", null)]
        public async Task ProcessarWebhookAsync_ComSegredoInvalidoOuAusente_RetornaNaoAutorizado(string? header, string? configuredSecret)
        {
            var sut = CreateSut(o => o.WebhookSecretToken = configuredSecret);
            var update = ParseJson("""{"message":{"text":"/start 1","chat":{"id":12345}}}""");

            var result = await sut.ProcessarWebhookAsync(update, header);

            result.Status.Should().Be(WebhookProcessStatus.NaoAutorizado);
            _transportMock.Verify(t => t.EnviarAsync(It.IsAny<MensageriaEnvelope>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData("{}")]
        [InlineData("""{"message":{"chat":{"id":123}}}""")]
        [InlineData("""{"message":{"text":"   ","chat":{"id":123}}}""")]
        [InlineData("""{"message":{"text":"/start"}}""")]
        public async Task ProcessarWebhookAsync_ComPayloadIncompleto_RetornaIgnoradoSemAcionarTransport(string json)
        {
            var sut = CreateSut();

            var result = await sut.ProcessarWebhookAsync(ParseJson(json), "secret-token-123");

            result.Status.Should().Be(WebhookProcessStatus.Ignorado);
            _transportMock.Verify(t => t.EnviarAsync(It.IsAny<MensageriaEnvelope>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ProcessarWebhookAsync_StartComBotMentionEIdValido_VinculaClienteERespondePeloTransport()
        {
            _context.Clientes.Add(new Cliente { Id = 15, Nome = "João Silva" });
            await _context.SaveChangesAsync();

            MensageriaEnvelope? reply = null;
            _transportMock
                .Setup(t => t.EnviarAsync(It.IsAny<MensageriaEnvelope>(), It.IsAny<CancellationToken>()))
                .Callback<MensageriaEnvelope, CancellationToken>((env, _) => reply = env)
                .ReturnsAsync(MensageriaDeliveryResult.Ok());

            var sut = CreateSut();
            var update = ParseJson("""{"message":{"text":"/start@TechOSaviso_bot 15","chat":{"id":987654}}}""");

            var result = await sut.ProcessarWebhookAsync(update, "secret-token-123");

            result.Status.Should().Be(WebhookProcessStatus.AutorizadoEProcessado);
            (await _context.Clientes.FindAsync(15))!.TelegramChatId.Should().Be("987654");
            reply.Should().NotBeNull();
            reply!.Origem.Should().Be(MensageriaOrigem.RespostaBot);
            reply.ChatId.Should().Be("987654");
            reply.Texto.Should().Contain("vinculado com sucesso");
        }

        [Fact]
        public async Task ProcessarWebhookAsync_StartComOutroChatQuandoClienteJaVinculado_BloqueiaTakeoverENaoAlteraBanco()
        {
            _context.Clientes.Add(new Cliente { Id = 22, Nome = "Maria Oliveira", TelegramChatId = "chat-2200" });
            await _context.SaveChangesAsync();

            MensageriaEnvelope? reply = null;
            _transportMock
                .Setup(t => t.EnviarAsync(It.IsAny<MensageriaEnvelope>(), It.IsAny<CancellationToken>()))
                .Callback<MensageriaEnvelope, CancellationToken>((env, _) => reply = env)
                .ReturnsAsync(MensageriaDeliveryResult.Ok());

            var sut = CreateSut();
            var update = ParseJson("""{"message":{"text":"/start 22","chat":{"id":9999}}}""");

            var result = await sut.ProcessarWebhookAsync(update, "secret-token-123");

            result.Status.Should().Be(WebhookProcessStatus.AutorizadoEProcessado);
            (await _context.Clientes.FindAsync(22))!.TelegramChatId.Should().Be("chat-2200");
            reply!.ChatId.Should().Be("9999");
            reply.Texto.Should().Contain("já possui outro Telegram vinculado");
        }

        [Fact]
        public async Task ProcessarWebhookAsync_StartComMesmoChat_ConfirmaDeFormaIdempotente()
        {
            _context.Clientes.Add(new Cliente { Id = 22, Nome = "Maria Oliveira", TelegramChatId = "2200" });
            await _context.SaveChangesAsync();

            MensageriaEnvelope? reply = null;
            _transportMock
                .Setup(t => t.EnviarAsync(It.IsAny<MensageriaEnvelope>(), It.IsAny<CancellationToken>()))
                .Callback<MensageriaEnvelope, CancellationToken>((env, _) => reply = env)
                .ReturnsAsync(MensageriaDeliveryResult.Ok());

            var sut = CreateSut();
            var update = ParseJson("""{"message":{"text":"/start 22","chat":{"id":2200}}}""");

            var result = await sut.ProcessarWebhookAsync(update, "secret-token-123");

            result.Status.Should().Be(WebhookProcessStatus.AutorizadoEProcessado);
            reply!.Texto.Should().Contain("já estava vinculado");
        }

        [Theory]
        [InlineData("/start abc", "Código de ativação inválido")]
        [InlineData("/start -3", "Código de ativação inválido")]
        [InlineData("/start 999", "Cliente não encontrado")]
        [InlineData("/start", "link de ativação enviado pela Assistência")]
        [InlineData("Oi, cadê minha OS?", "canal automático")]
        public async Task ProcessarWebhookAsync_ComComandosEspeciaisOuTextoLivre_EnviaOrientacaoPeloTransport(
            string text,
            string expectedFragment)
        {
            MensageriaEnvelope? reply = null;
            _transportMock
                .Setup(t => t.EnviarAsync(It.IsAny<MensageriaEnvelope>(), It.IsAny<CancellationToken>()))
                .Callback<MensageriaEnvelope, CancellationToken>((env, _) => reply = env)
                .ReturnsAsync(MensageriaDeliveryResult.Ok());

            var sut = CreateSut();
            var update = ParseJson(JsonSerializer.Serialize(new { message = new { text, chat = new { id = 777 } } }));

            var result = await sut.ProcessarWebhookAsync(update, "secret-token-123");

            result.Status.Should().Be(WebhookProcessStatus.AutorizadoEProcessado);
            reply.Should().NotBeNull();
            reply!.Texto.Should().Contain(expectedFragment);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }
}
