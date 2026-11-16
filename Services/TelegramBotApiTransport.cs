using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AssistenciaTech.Services
{
    public class TelegramBotApiTransport : IMensageriaTransport
    {
        private readonly HttpClient _httpClient;
        private readonly MensageriaOptions _options;
        private readonly ILogger<TelegramBotApiTransport> _logger;

        public TelegramBotApiTransport(
            HttpClient httpClient,
            IOptions<MensageriaOptions> options,
            ILogger<TelegramBotApiTransport> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<MensageriaDeliveryResult> EnviarAsync(MensageriaEnvelope envelope, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_options.BotToken))
            {
                _logger.LogWarning(
                    "[Telegram] Token do bot não configurado. Mensagem para cliente {ClienteNome} (ID: {ClienteId}) não enviada.",
                    envelope.ClienteNome ?? "-", envelope.ClienteId);
                return MensageriaDeliveryResult.Falha("Token do bot do Telegram não configurado.", tentativas: 0);
            }

            if (string.IsNullOrWhiteSpace(envelope.ChatId))
            {
                _logger.LogWarning(
                    "[Telegram] Cliente {ClienteNome} (ID: {ClienteId}) não possui TelegramChatId cadastrado.",
                    envelope.ClienteNome ?? "-", envelope.ClienteId);
                return MensageriaDeliveryResult.Falha("ChatId do Telegram não informado.", tentativas: 0);
            }

            string payloadJson = JsonSerializer.Serialize(new
            {
                chat_id = envelope.ChatId,
                text = envelope.Texto
            });
            string endpoint = $"https://api.telegram.org/bot{_options.BotToken}/sendMessage";
            int maxAttempts = 1 + Math.Max(0, _options.MaxRetries);

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    using var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
                    using var response = await _httpClient.PostAsync(endpoint, content, ct);

                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation(
                            "[Telegram] Mensagem ({Origem}) enviada para ChatId {ChatId} na tentativa {Tentativa}.",
                            envelope.Origem, envelope.ChatId, attempt);
                        return MensageriaDeliveryResult.Ok((int)response.StatusCode, attempt);
                    }

                    string responseBody = await response.Content.ReadAsStringAsync(ct);

                    if (response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        _logger.LogWarning(
                            "[Telegram] Bot bloqueado pelo usuário {ClienteNome} (ChatId: {ChatId}). Status: 403. Resposta: {Response}",
                            envelope.ClienteNome ?? "-", envelope.ChatId, responseBody);
                        return MensageriaDeliveryResult.Falha(
                            string.IsNullOrWhiteSpace(responseBody) ? "Forbidden (bot bloqueado pelo usuário)" : responseBody,
                            statusCode: (int)response.StatusCode,
                            bloqueadoPeloUsuario: true,
                            tentativas: attempt);
                    }

                    if (IsTransientStatus(response.StatusCode) && attempt < maxAttempts)
                    {
                        _logger.LogWarning(
                            "[Telegram] Erro transitório {StatusCode} ao enviar para ChatId {ChatId} (tentativa {Tentativa}/{Max}). Reintentando.",
                            (int)response.StatusCode, envelope.ChatId, attempt, maxAttempts);
                        await AguardarRetryAsync(attempt, ct);
                        continue;
                    }

                    _logger.LogError(
                        "[Telegram] Falha ao enviar mensagem para {ClienteNome} (ChatId: {ChatId}). Status: {StatusCode}, Tentativas: {Tentativas}, Resposta: {Response}",
                        envelope.ClienteNome ?? "-", envelope.ChatId, (int)response.StatusCode, attempt, responseBody);

                    return MensageriaDeliveryResult.Falha(
                        string.IsNullOrWhiteSpace(responseBody) ? $"HTTP {(int)response.StatusCode}" : responseBody,
                        statusCode: (int)response.StatusCode,
                        tentativas: attempt);
                }
                catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
                {
                    if (attempt < maxAttempts)
                    {
                        _logger.LogWarning(
                            ex,
                            "[Telegram] Falha de rede ao enviar para ChatId {ChatId} (tentativa {Tentativa}/{Max}). Reintentando.",
                            envelope.ChatId, attempt, maxAttempts);
                        await AguardarRetryAsync(attempt, ct);
                        continue;
                    }

                    _logger.LogError(
                        ex,
                        "[Telegram] Erro de rede ao enviar mensagem para {ClienteNome} (ChatId: {ChatId}) após {Tentativas} tentativa(s).",
                        envelope.ClienteNome ?? "-", envelope.ChatId, attempt);
                    return MensageriaDeliveryResult.Falha(ex.Message, tentativas: attempt);
                }
            }

            return MensageriaDeliveryResult.Falha("Falha desconhecida ao enviar mensagem pelo Telegram.", tentativas: maxAttempts);
        }

        private static bool IsTransientStatus(HttpStatusCode statusCode) =>
            (int)statusCode == 429 || (int)statusCode >= 500;

        private Task AguardarRetryAsync(int attempt, CancellationToken ct) =>
            _options.RetryBaseDelayMs > 0
                ? Task.Delay(_options.RetryBaseDelayMs * attempt, ct)
                : Task.CompletedTask;
    }
}
