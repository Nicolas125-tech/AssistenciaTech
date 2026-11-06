using System.Collections.Generic;
using AssistenciaTech.Models;

namespace AssistenciaTech.Services
{
    public class OrdemServicoResult
    {
        public bool Sucesso { get; set; }
        public string? Mensagem { get; set; }
        public List<string> Erros { get; set; } = new();
        public OrdemServico? Ordem { get; set; }

        public static OrdemServicoResult Ok(OrdemServico ordem, string? mensagem = null) =>
            new()
            {
                Sucesso = true,
                Ordem = ordem,
                Mensagem = mensagem
            };

        public static OrdemServicoResult Falha(string erro, params string[] outrosErros)
        {
            var res = new OrdemServicoResult
            {
                Sucesso = false,
                Mensagem = erro
            };
            res.Erros.Add(erro);
            if (outrosErros != null && outrosErros.Length > 0)
            {
                res.Erros.AddRange(outrosErros);
            }
            return res;
        }
    }
}
