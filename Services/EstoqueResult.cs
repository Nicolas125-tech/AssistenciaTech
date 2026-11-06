using System.Collections.Generic;
using AssistenciaTech.Models;

namespace AssistenciaTech.Services
{
    public class EstoqueResult
    {
        public bool Sucesso { get; set; }
        public string Mensagem { get; set; } = string.Empty;
        public Peca? Peca { get; set; }
        public List<string> Erros { get; set; } = new();

        public static EstoqueResult Ok(Peca? peca = null, string mensagem = "")
        {
            return new EstoqueResult
            {
                Sucesso = true,
                Mensagem = mensagem,
                Peca = peca
            };
        }

        public static EstoqueResult Falha(string mensagem, IEnumerable<string>? erros = null)
        {
            var result = new EstoqueResult
            {
                Sucesso = false,
                Mensagem = mensagem
            };

            if (erros != null)
            {
                result.Erros.AddRange(erros);
            }

            return result;
        }
    }
}
