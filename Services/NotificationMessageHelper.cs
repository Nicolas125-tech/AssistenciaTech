using AssistenciaTech.Models;
using Microsoft.Extensions.Configuration;

namespace AssistenciaTech.Services
{
    public static class NotificationMessageHelper
    {
        public static string GerarMensagem(IConfiguration configuration, Cliente cliente, OrdemServico os, string statusAnterior)
        {
            var novoStatus = os.Status;

            // Tenta obter o template específico para o status atual
            var template = configuration[$"NotificationTemplates:{novoStatus}"];

            if (string.IsNullOrEmpty(template))
            {
                // Fallback para o default se não encontrar
                template = configuration["NotificationTemplates:Default"];

                if (string.IsNullOrEmpty(template))
                {
                     // Fallback hardcoded de última linha de segurança
                     return $"Olá {cliente.Nome}, o status da sua OS #{os.Id} ({os.Equipamento}) foi atualizado de '{statusAnterior}' para '{novoStatus}'.";
                }
            }

            // Os templates usarão:
            // {0} = Nome do cliente
            // {1} = Equipamento
            // {2} = ID da OS
            // {3} = Valor do Orçamento (formatado como moeda:C)
            // {4} = Status Anterior
            // {5} = Novo Status

            return string.Format(template,
                cliente.Nome,
                os.Equipamento,
                os.Id,
                os.ValorOrcamento.ToString("C"),
                statusAnterior,
                novoStatus);
        }
    }
}
