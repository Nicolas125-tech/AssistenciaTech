using System.Collections.Generic;

namespace AssistenciaTech.Services
{
    public class MobileAuthResult
    {
        public bool Sucesso { get; set; }
        public string? Token { get; set; }
        public int TecnicoId { get; set; }
        public string NomeTecnico { get; set; } = string.Empty;
        public string? Mensagem { get; set; }

        public static MobileAuthResult Ok(string token, int tecnicoId, string nomeTecnico)
        {
            return new MobileAuthResult
            {
                Sucesso = true,
                Token = token,
                TecnicoId = tecnicoId,
                NomeTecnico = nomeTecnico
            };
        }

        public static MobileAuthResult Falha(string mensagem)
        {
            return new MobileAuthResult
            {
                Sucesso = false,
                Mensagem = mensagem
            };
        }
    }

    public class VisitaResult
    {
        public bool Sucesso { get; set; }
        public int? VisitaId { get; set; }
        public string Mensagem { get; set; } = string.Empty;
        public int StatusCode { get; set; } = 200;
        public List<string> Erros { get; set; } = new();

        public static VisitaResult Ok(string mensagem, int? visitaId = null)
        {
            return new VisitaResult
            {
                Sucesso = true,
                VisitaId = visitaId,
                Mensagem = mensagem,
                StatusCode = 200
            };
        }

        public static VisitaResult NaoEncontrado(string mensagem)
        {
            return new VisitaResult
            {
                Sucesso = false,
                Mensagem = mensagem,
                StatusCode = 404
            };
        }

        public static VisitaResult Proibido(string mensagem = "Acesso proibido.")
        {
            return new VisitaResult
            {
                Sucesso = false,
                Mensagem = mensagem,
                StatusCode = 403
            };
        }

        public static VisitaResult Invalido(string mensagem, IEnumerable<string>? erros = null)
        {
            var res = new VisitaResult
            {
                Sucesso = false,
                Mensagem = mensagem,
                StatusCode = 400
            };
            if (erros != null) res.Erros.AddRange(erros);
            return res;
        }
    }

    public class VisitaSyncResult
    {
        public bool Sucesso { get; set; }
        public string Mensagem { get; set; } = string.Empty;
        public int TotalProcessadas { get; set; }
        public int TotalInseridas { get; set; }
        public int TotalDuplicadas { get; set; }
        public List<string> Erros { get; set; } = new();

        public static VisitaSyncResult Ok(string mensagem, int totalProcessadas, int totalInseridas, int totalDuplicadas)
        {
            return new VisitaSyncResult
            {
                Sucesso = true,
                Mensagem = mensagem,
                TotalProcessadas = totalProcessadas,
                TotalInseridas = totalInseridas,
                TotalDuplicadas = totalDuplicadas
            };
        }

        public static VisitaSyncResult Falha(string mensagem, IEnumerable<string>? erros = null)
        {
            var res = new VisitaSyncResult
            {
                Sucesso = false,
                Mensagem = mensagem
            };
            if (erros != null) res.Erros.AddRange(erros);
            return res;
        }
    }
}
