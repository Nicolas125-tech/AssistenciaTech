using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AssistenciaTech.Models;

namespace AssistenciaTech.Services
{
    public enum WebhookProcessStatus
    {
        AutorizadoEProcessado,
        NaoAutorizado,
        Ignorado
    }

    public class WebhookProcessResult
    {
        public WebhookProcessStatus Status { get; init; }
        public string? Detalhe { get; init; }
        public MensageriaDeliveryResult? Entrega { get; init; }

        public static WebhookProcessResult NaoAutorizado(string? detalhe = null) =>
            new() { Status = WebhookProcessStatus.NaoAutorizado, Detalhe = detalhe };

        public static WebhookProcessResult Ignorado(string? detalhe = null) =>
            new() { Status = WebhookProcessStatus.Ignorado, Detalhe = detalhe };

        public static WebhookProcessResult Processado(MensageriaDeliveryResult? entrega, string? detalhe = null) =>
            new() { Status = WebhookProcessStatus.AutorizadoEProcessado, Entrega = entrega, Detalhe = detalhe };
    }

    /// <summary>
    /// Módulo profundo unificado de mensageria, templates e processamento de comandos do Bot do Telegram.
    /// </summary>
    public interface IMensageriaEngine : INotificationService
    {
        Task<WebhookProcessResult> ProcessarWebhookAsync(JsonElement update, string? secretToken, CancellationToken ct = default);

        Task<MensageriaDeliveryResult> EnviarNotificacaoStatusComResultadoAsync(
            Cliente? cliente,
            OrdemServico os,
            string statusAnterior,
            CancellationToken ct = default);
    }
}
