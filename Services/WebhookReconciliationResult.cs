using System.Collections.Generic;

namespace AssistenciaTech.Services
{
    public class WebhookReconciliationResult
    {
        public bool Sucesso { get; set; }
        public int HttpStatusCode { get; set; } = 200;
        public string? MensagemErro { get; set; }
        public IReadOnlyList<string> TxIdsReconciliados { get; set; } = new List<string>();
        public int TotalFaturasLiquidadas { get; set; }

        public static WebhookReconciliationResult Ok(IReadOnlyList<string> txIds, int totalFaturasLiquidadas)
        {
            return new WebhookReconciliationResult
            {
                Sucesso = true,
                HttpStatusCode = 200,
                TxIdsReconciliados = txIds,
                TotalFaturasLiquidadas = totalFaturasLiquidadas
            };
        }

        public static WebhookReconciliationResult Falha(int statusCode, string mensagem)
        {
            return new WebhookReconciliationResult
            {
                Sucesso = false,
                HttpStatusCode = statusCode,
                MensagemErro = mensagem
            };
        }
    }
}
