using System.Security.Claims;
using AssistenciaTech.Models;

namespace AssistenciaTech.Services
{
    public class ConsultaPublicaResult
    {
        public bool Sucesso { get; set; }
        public OrdemServico? Ordem { get; set; }
        public string Mensagem { get; set; } = string.Empty;
        public bool ParametrosInvalidos { get; set; }

        public static ConsultaPublicaResult Ok(OrdemServico ordem)
        {
            return new ConsultaPublicaResult
            {
                Sucesso = true,
                Ordem = ordem
            };
        }

        public static ConsultaPublicaResult Invalido(string mensagem)
        {
            return new ConsultaPublicaResult
            {
                Sucesso = false,
                Mensagem = mensagem,
                ParametrosInvalidos = true
            };
        }

        public static ConsultaPublicaResult NaoEncontrado(string mensagem)
        {
            return new ConsultaPublicaResult
            {
                Sucesso = false,
                Mensagem = mensagem,
                ParametrosInvalidos = false
            };
        }

        public static ConsultaPublicaResult Falha(string mensagem) => NaoEncontrado(mensagem);
    }

    public class VisualizacaoOSResult
    {
        public bool Sucesso { get; set; }
        public OrdemServico? Ordem { get; set; }
        public string Mensagem { get; set; } = string.Empty;
        public int StatusCode { get; set; } = 200;
        public bool NaoEncontradoOuNaoAutorizado => StatusCode == 404 || StatusCode == 403;

        public static VisualizacaoOSResult Ok(OrdemServico ordem)
        {
            return new VisualizacaoOSResult
            {
                Sucesso = true,
                Ordem = ordem,
                StatusCode = 200
            };
        }

        public static VisualizacaoOSResult NaoEncontrado(string mensagem = "Ordem de Serviço não encontrada ou acesso não autorizado.")
        {
            return new VisualizacaoOSResult
            {
                Sucesso = false,
                Mensagem = mensagem,
                StatusCode = 404
            };
        }

        public static VisualizacaoOSResult Proibido(string mensagem = "Acesso não autorizado.")
        {
            return new VisualizacaoOSResult
            {
                Sucesso = false,
                Mensagem = mensagem,
                StatusCode = 403
            };
        }

        public static VisualizacaoOSResult FalhaInterna(string mensagem)
        {
            return new VisualizacaoOSResult
            {
                Sucesso = false,
                Mensagem = mensagem,
                StatusCode = 500
            };
        }
    }

    public class ClienteAuthResult
    {
        public bool Sucesso { get; set; }
        public ClaimsPrincipal? Principal { get; set; }
        public Cliente? Cliente { get; set; }
        public string Mensagem { get; set; } = string.Empty;

        public static ClienteAuthResult Ok(Cliente cliente, ClaimsPrincipal principal)
        {
            return new ClienteAuthResult
            {
                Sucesso = true,
                Cliente = cliente,
                Principal = principal
            };
        }

        public static ClienteAuthResult Falha(string mensagem)
        {
            return new ClienteAuthResult
            {
                Sucesso = false,
                Mensagem = mensagem
            };
        }
    }
}
