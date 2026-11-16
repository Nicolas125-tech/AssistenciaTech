using System.Threading;
using System.Threading.Tasks;

namespace AssistenciaTech.Services
{
    public enum MensageriaOrigem
    {
        NotificacaoStatus,
        RespostaBot
    }

    public class MensageriaEnvelope
    {
        public string? ChatId { get; init; }
        public string Texto { get; init; } = string.Empty;
        public MensageriaOrigem Origem { get; init; }
        public int? ClienteId { get; init; }
        public string? ClienteNome { get; init; }
        public string? Telefone { get; init; }
        public string? Email { get; init; }
        public int? OrdemServicoId { get; init; }
    }

    public class MensageriaDeliveryResult
    {
        public bool Sucesso { get; init; }
        public int? StatusCode { get; init; }
        public string? MotivoFalha { get; init; }
        public bool BloqueadoPeloUsuario { get; init; }
        public int Tentativas { get; init; }

        public static MensageriaDeliveryResult Ok(int? statusCode = 200, int tentativas = 1) =>
            new()
            {
                Sucesso = true,
                StatusCode = statusCode,
                Tentativas = tentativas
            };

        public static MensageriaDeliveryResult Falha(
            string motivo,
            int? statusCode = null,
            bool bloqueadoPeloUsuario = false,
            int tentativas = 1) =>
            new()
            {
                Sucesso = false,
                MotivoFalha = motivo,
                StatusCode = statusCode,
                BloqueadoPeloUsuario = bloqueadoPeloUsuario,
                Tentativas = tentativas
            };
    }

    /// <summary>
    /// Seam único de transporte de mensagens entre o módulo de mensageria e adaptadores (Telegram API ou Log).
    /// </summary>
    public interface IMensageriaTransport
    {
        Task<MensageriaDeliveryResult> EnviarAsync(MensageriaEnvelope envelope, CancellationToken ct = default);
    }
}
