using AssistenciaTech.Models;
using System.Collections.Generic;

namespace AssistenciaTech.Services
{
    public class FaturamentoResult
    {
        public bool Sucesso { get; set; }
        public string? Mensagem { get; set; }
        public IReadOnlyList<string> Erros { get; set; } = new List<string>();
        public Faturamento? Faturamento { get; set; }
        public int? FaturamentoId => Faturamento?.Id;

        public static FaturamentoResult Ok(Faturamento faturamento, string? mensagem = null)
        {
            return new FaturamentoResult
            {
                Sucesso = true,
                Faturamento = faturamento,
                Mensagem = mensagem
            };
        }

        public static FaturamentoResult Falha(string mensagem, IEnumerable<string>? erros = null)
        {
            var erroList = erros != null ? new List<string>(erros) : new List<string> { mensagem };
            return new FaturamentoResult
            {
                Sucesso = false,
                Mensagem = mensagem,
                Erros = erroList
            };
        }
    }
}
