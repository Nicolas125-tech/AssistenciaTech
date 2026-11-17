using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssistenciaTech.Services
{
    public class MensageriaEngine : IMensageriaEngine
    {
        private static readonly Regex _startCommandRegex = new(
            @"^/start(?:@[A-Za-z0-9_]+)?(?:\s+(.*))?$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly AppDbContext _context;
        private readonly IMensageriaTransport _transport;
        private readonly MensageriaOptions _options;
        private readonly ILogger<MensageriaEngine> _logger;

        public MensageriaEngine(
            AppDbContext context,
            IMensageriaTransport transport,
            IOptions<MensageriaOptions> options,
            ILogger<MensageriaEngine> logger)
        {
            _context = context;
            _transport = transport;
            _options = options.Value;
            _logger = logger;
        }

        public Task EnviarNotificacaoStatusAsync(Cliente cliente, OrdemServico os, string statusAnterior) =>
            EnviarNotificacaoStatusComResultadoAsync(cliente, os, statusAnterior);

        public async Task<MensageriaDeliveryResult> EnviarNotificacaoStatusComResultadoAsync(
            Cliente? cliente,
            OrdemServico os,
            string statusAnterior,
            CancellationToken ct = default)
        {
            if (cliente == null)
            {
                _logger.LogWarning("[Notificação] Tentativa de notificação para OS #{OsId} sem cliente associado.", os.Id);
                return MensageriaDeliveryResult.Falha("Cliente não associado à OS.", tentativas: 0);
            }

            string mensagem = RenderizarMensagemStatus(cliente, os, statusAnterior);
            var envelope = new MensageriaEnvelope
            {
                ChatId = cliente.TelegramChatId,
                Texto = mensagem,
                Origem = MensageriaOrigem.NotificacaoStatus,
                ClienteId = cliente.Id,
                ClienteNome = cliente.Nome,
                Telefone = cliente.Telefone,
                Email = cliente.Email,
                OrdemServicoId = os.Id
            };

            var delivery = await _transport.EnviarAsync(envelope, ct);
            if (delivery.BloqueadoPeloUsuario)
            {
                _logger.LogWarning(
                    "[Mensageria] Cliente {ClienteNome} (ID: {ClienteId}) bloqueou o bot ao notificar OS #{OsId}; TelegramChatId preservado no banco.",
                    cliente.Nome, cliente.Id, os.Id);
            }

            return delivery;
        }

        public async Task<WebhookProcessResult> ProcessarWebhookAsync(
            JsonElement update,
            string? secretToken,
            CancellationToken ct = default)
        {
            if (!ValidarSecretToken(secretToken))
            {
                _logger.LogWarning("Unauthorized webhook request. Token mismatch or not configured.");
                return WebhookProcessResult.NaoAutorizado("Webhook secret token ausente ou inválido.");
            }

            if (!TentarExtrairMensagem(update, out string text, out string chatId))
            {
                return WebhookProcessResult.Ignorado("Update sem mensagem de texto e chatId válidos.");
            }

            string normalized = text.Trim();
            var match = _startCommandRegex.Match(normalized);

            if (!match.Success)
            {
                var entregaOrientacao = await EnviarRespostaBotAsync(
                    chatId,
                    "Este canal automático envia notificações das suas Ordens de Serviço. Para vincular sua conta, use o link de ativação enviado pela assistência.",
                    ct);
                return WebhookProcessResult.Processado(entregaOrientacao, "Texto livre respondido com orientação.");
            }

            string arg = match.Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(arg))
            {
                var entregaAjuda = await EnviarRespostaBotAsync(
                    chatId,
                    "Olá! Para receber notificações, você precisa clicar no link de ativação enviado pela Assistência.",
                    ct);
                return WebhookProcessResult.Processado(entregaAjuda, "Comando /start sem identificador.");
            }

            if (!int.TryParse(arg, out int clienteId) || clienteId <= 0)
            {
                var entregaInvalida = await EnviarRespostaBotAsync(
                    chatId,
                    "Código de ativação inválido. Solicite um novo link de vinculação na assistência.",
                    ct);
                return WebhookProcessResult.Processado(entregaInvalida, "Código de ativação inválido.");
            }

            return await VincularClientePorStartAsync(clienteId, chatId, ct);
        }

        private async Task<WebhookProcessResult> VincularClientePorStartAsync(int clienteId, string chatId, CancellationToken ct)
        {
            var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.Id == clienteId, ct);
            if (cliente == null)
            {
                var entregaNaoEncontrado = await EnviarRespostaBotAsync(
                    chatId,
                    "Cliente não encontrado. Por favor, solicite um novo link de vinculação.",
                    ct);
                return WebhookProcessResult.Processado(entregaNaoEncontrado, "Cliente não encontrado.");
            }

            if (!string.IsNullOrWhiteSpace(cliente.TelegramChatId))
            {
                if (string.Equals(cliente.TelegramChatId, chatId, StringComparison.Ordinal))
                {
                    var entregaIdempotente = await EnviarRespostaBotAsync(
                        chatId,
                        $"Olá {cliente.Nome}! Seu Telegram já estava vinculado a este chat e continua ativo.",
                        ct,
                        cliente);
                    return WebhookProcessResult.Processado(entregaIdempotente, "Vínculo reconfirmado.");
                }

                _logger.LogWarning(
                    "[Telegram Webhook] Tentativa bloqueada de sobrescrever vínculo do Cliente ID {ClienteId} pelo ChatId {ChatId}.",
                    clienteId, chatId);

                var entregaBloqueio = await EnviarRespostaBotAsync(
                    chatId,
                    "Este cadastro já possui outro Telegram vinculado. Solicite a desvinculação na assistência antes de trocar de aparelho.",
                    ct,
                    cliente);
                return WebhookProcessResult.Processado(entregaBloqueio, "Sobrescrita de vínculo bloqueada.");
            }

            cliente.TelegramChatId = chatId;
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("[Telegram Webhook] Chat ID {ChatId} vinculado com sucesso ao Cliente ID {ClienteId}", chatId, clienteId);

            var entregaSucesso = await EnviarRespostaBotAsync(
                chatId,
                $"Olá {cliente.Nome}! Seu Telegram foi vinculado com sucesso. A partir de agora você receberá notificações das suas Ordens de Serviço por aqui.",
                ct,
                cliente);
            return WebhookProcessResult.Processado(entregaSucesso, "Vínculo realizado com sucesso.");
        }

        private Task<MensageriaDeliveryResult> EnviarRespostaBotAsync(
            string chatId,
            string texto,
            CancellationToken ct,
            Cliente? cliente = null)
        {
            var envelope = new MensageriaEnvelope
            {
                ChatId = chatId,
                Texto = texto,
                Origem = MensageriaOrigem.RespostaBot,
                ClienteId = cliente?.Id,
                ClienteNome = cliente?.Nome
            };
            return _transport.EnviarAsync(envelope, ct);
        }

        private string RenderizarMensagemStatus(Cliente cliente, OrdemServico os, string statusAnterior)
        {
            string fallback = $"Olá {cliente.Nome}, o status da sua OS #{os.Id} ({os.Equipamento}) foi atualizado de '{statusAnterior}' para '{os.Status}'.";

            string? template = null;
            if (!string.IsNullOrEmpty(os.Status))
            {
                _options.Templates.TryGetValue(os.Status, out template);
            }

            if (string.IsNullOrEmpty(template))
            {
                _options.Templates.TryGetValue("Default", out template);
            }

            if (string.IsNullOrEmpty(template))
            {
                return fallback;
            }

            try
            {
                return string.Format(
                    template,
                    cliente.Nome,
                    os.Equipamento,
                    os.Id,
                    os.ValorOrcamento.ToString("C"),
                    statusAnterior,
                    os.Status);
            }
            catch (FormatException ex)
            {
                _logger.LogWarning(
                    ex,
                    "[Mensageria] Template inválido para status '{Status}' na OS #{OsId}. Utilizando mensagem padrão segura.",
                    os.Status, os.Id);
                return fallback;
            }
        }

        private bool ValidarSecretToken(string? secretToken)
        {
            if (string.IsNullOrEmpty(_options.WebhookSecretToken) || string.IsNullOrEmpty(secretToken))
            {
                return false;
            }

            byte[] secretBytes = Encoding.UTF8.GetBytes(secretToken);
            byte[] configuredBytes = Encoding.UTF8.GetBytes(_options.WebhookSecretToken);

            return secretBytes.Length == configuredBytes.Length &&
                   CryptographicOperations.FixedTimeEquals(secretBytes, configuredBytes);
        }

        private static bool TentarExtrairMensagem(JsonElement update, out string text, out string chatId)
        {
            text = string.Empty;
            chatId = string.Empty;

            if (update.ValueKind != JsonValueKind.Object ||
                !update.TryGetProperty("message", out JsonElement message) ||
                message.ValueKind != JsonValueKind.Object ||
                !message.TryGetProperty("text", out JsonElement textElement) ||
                textElement.ValueKind != JsonValueKind.String ||
                !message.TryGetProperty("chat", out JsonElement chat) ||
                chat.ValueKind != JsonValueKind.Object ||
                !chat.TryGetProperty("id", out JsonElement chatIdElement))
            {
                return false;
            }

            text = textElement.GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (chatIdElement.ValueKind == JsonValueKind.Number && chatIdElement.TryGetInt64(out long idNum))
            {
                chatId = idNum.ToString();
            }
            else if (chatIdElement.ValueKind == JsonValueKind.String)
            {
                chatId = chatIdElement.GetString() ?? string.Empty;
            }

            return !string.IsNullOrWhiteSpace(chatId);
        }
    }
}
