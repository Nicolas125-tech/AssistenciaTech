using AssistenciaTech.Models;

namespace AssistenciaTech.Services
{
    public class TributacaoResultadoDto
    {
        public decimal BaseCalculoISS { get; set; }
        public decimal AliquotaISS { get; set; }
        public decimal ValorISS { get; set; }
        public decimal BaseCalculoICMS { get; set; }
        public decimal AliquotaICMS { get; set; }
        public decimal ValorICMS { get; set; }
    }

    public interface ITributacaoService
    {
        TributacaoResultadoDto CalcularTributos(OrdemServico ordemServico);
    }

    public class TributacaoService : ITributacaoService
    {
        // Alíquotas padrão (poderiam vir de configuração ou banco de dados)
        private const decimal ALIQUOTA_ISS = 0.05m; // 5%
        private const decimal ALIQUOTA_ICMS = 0.18m; // 18%

        public TributacaoResultadoDto CalcularTributos(OrdemServico ordemServico)
        {
            if (ordemServico == null)
            {
                throw new System.ArgumentNullException(nameof(ordemServico));
            }

            // O ISS é calculado sobre a mão de obra prestada
            decimal baseIss = ordemServico.CustoMaoDeObra;
            decimal valorIss = baseIss * ALIQUOTA_ISS;

            // O ICMS é calculado sobre o custo/venda das peças
            decimal baseIcms = ordemServico.CustoPecas;
            decimal valorIcms = baseIcms * ALIQUOTA_ICMS;

            return new TributacaoResultadoDto
            {
                BaseCalculoISS = baseIss,
                AliquotaISS = ALIQUOTA_ISS,
                ValorISS = Math.Round(valorIss, 2),
                
                BaseCalculoICMS = baseIcms,
                AliquotaICMS = ALIQUOTA_ICMS,
                ValorICMS = Math.Round(valorIcms, 2)
            };
        }
    }
}
