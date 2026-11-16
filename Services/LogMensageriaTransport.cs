using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AssistenciaTech.Services
{
    public class LogMensageriaTransport : IMensageriaTransport
    {
        private readonly ILogger<LogMensageriaTransport> _logger;

        public LogMensageriaTransport(ILogger<LogMensageriaTransport> logger)
        {
            _logger = logger;
        }

        public Task<MensageriaDeliveryResult> EnviarAsync(MensageriaEnvelope envelope, CancellationToken ct = default)
        {
            if (envelope.Origem == MensageriaOrigem.RespostaBot)
            {
                _logger.LogInformation(
                    "[LogMensageria][Bot] Resposta simulada para ChatId {ChatId}: {Mensagem}",
                    envelope.ChatId ?? "-", envelope.Texto);
                return Task.FromResult(MensageriaDeliveryResult.Ok(statusCode: null, tentativas: 1));
            }

            bool enviouAlgumCanal = false;

            if (!string.IsNullOrWhiteSpace(envelope.ChatId))
            {
                enviouAlgumCanal = true;
                _logger.LogInformation(
                    "[Telegram/Log] Notificação simulada para {ClienteNome} ({ChatId}): {Mensagem}",
                    envelope.ClienteNome ?? "-", envelope.ChatId, envelope.Texto);
            }

            if (!string.IsNullOrWhiteSpace(envelope.Telefone))
            {
                enviouAlgumCanal = true;
                _logger.LogInformation(
                    "[WhatsApp] Notificação enviada para {ClienteNome} ({Telefone}): {Mensagem}",
                    envelope.ClienteNome ?? "-", envelope.Telefone, envelope.Texto);
            }

            if (!string.IsNullOrWhiteSpace(envelope.Email))
            {
                enviouAlgumCanal = true;
                _logger.LogInformation(
                    "[Email] Notificação enviada para {ClienteNome} ({Email}): {Mensagem}",
                    envelope.ClienteNome ?? "-", envelope.Email, envelope.Texto);
            }

            if (!enviouAlgumCanal)
            {
                _logger.LogWarning(
                    "[Notificação] Cliente {ClienteNome} (ID: {ClienteId}) não possui canal de contato cadastrado para OS #{OsId}.",
                    envelope.ClienteNome ?? "-", envelope.ClienteId, envelope.OrdemServicoId);
                return Task.FromResult(MensageriaDeliveryResult.Falha("Cliente sem canal de contato cadastrado.", tentativas: 1));
            }

            return Task.FromResult(MensageriaDeliveryResult.Ok(statusCode: null, tentativas: 1));
        }
    }
}
