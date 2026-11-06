using System.Threading.Tasks;

namespace AssistenciaTech.Services
{
    public interface IFaturamentoEngine
    {
        /// <summary>
        /// Emite fatura a partir de uma Ordem de Serviço, aplicando tributação de ISS e ICMS,
        /// gerando payload dinâmico de Pix (BR Code) e garantindo idempotência.
        /// </summary>
        Task<FaturamentoResult> FaturarOrdemAsync(int osId);

        /// <summary>
        /// Autentica e processa callback de Webhook Pix utilizando HMAC-SHA256,
        /// extraindo identificadores e reconciliando pagamentos atômicos no banco de dados.
        /// </summary>
        Task<WebhookReconciliationResult> ProcessarWebhookPixAsync(string payload, string? signatureHeader);

        /// <summary>
        /// Realiza a baixa manual de faturamento para quitação na bancada.
        /// </summary>
        Task<FaturamentoResult> LiquidarFaturaManualmenteAsync(int faturamentoId);

        /// <summary>
        /// Emite documento XML de NFS-e no padrão ABRASF correspondente ao faturamento.
        /// </summary>
        Task<ArtefatoFiscalResult> GerarXmlNfseAsync(int faturamentoId);

        /// <summary>
        /// Emite documento PDF de Recibo de Pagamento para faturamento quitado.
        /// </summary>
        Task<ArtefatoFiscalResult> GerarReciboPagamentoPdfAsync(int faturamentoId);
    }
}
