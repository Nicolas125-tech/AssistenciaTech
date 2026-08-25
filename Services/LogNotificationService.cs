using AssistenciaTech.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace AssistenciaTech.Services
{
    /// <summary>
    /// Implementação de notificações que simula o envio via Log.
    /// Em produção, substituir por WhatsAppNotificationService ou EmailNotificationService.
    /// </summary>
    public class LogNotificationService : INotificationService
    {
        private readonly ILogger<LogNotificationService> _logger;

        public LogNotificationService(ILogger<LogNotificationService> logger)
        {
            _logger = logger;
        }

        public Task EnviarNotificacaoStatusAsync(Cliente cliente, OrdemServico os, string statusAnterior)
        {
            if (cliente == null)
            {
                _logger.LogWarning("[Notificação] Tentativa de notificação para OS #{OsId} sem cliente associado.", os.Id);
                return Task.CompletedTask;
            }

            var mensagem = GerarMensagem(cliente, os, statusAnterior);

            // Simula envio via WhatsApp
            if (!string.IsNullOrEmpty(cliente.Telefone))
            {
                _logger.LogInformation(
                    "[WhatsApp] Notificação enviada para {ClienteNome} ({Telefone}): {Mensagem}",
                    cliente.Nome, cliente.Telefone, mensagem);
            }

            // Simula envio via E-mail
            if (!string.IsNullOrEmpty(cliente.Email))
            {
                _logger.LogInformation(
                    "[Email] Notificação enviada para {ClienteNome} ({Email}): {Mensagem}",
                    cliente.Nome, cliente.Email, mensagem);
            }

            if (string.IsNullOrEmpty(cliente.Telefone) && string.IsNullOrEmpty(cliente.Email))
            {
                _logger.LogWarning(
                    "[Notificação] Cliente {ClienteNome} (ID: {ClienteId}) não possui telefone nem e-mail cadastrado. Notificação não enviada para OS #{OsId}.",
                    cliente.Nome, cliente.Id, os.Id);
            }

            return Task.CompletedTask;
        }

        private static string GerarMensagem(Cliente cliente, OrdemServico os, string statusAnterior)
        {
            var novoStatus = os.Status;

            return novoStatus switch
            {
                WorkflowStatus.Recebido => $"Olá {cliente.Nome}, seu equipamento '{os.Equipamento}' foi recebido na assistência técnica. OS #{os.Id}.",
                WorkflowStatus.EmAnalise => $"Olá {cliente.Nome}, seu equipamento '{os.Equipamento}' (OS #{os.Id}) está sendo analisado pelo nosso técnico.",
                WorkflowStatus.AguardandoAprovacao => $"Olá {cliente.Nome}, o orçamento da OS #{os.Id} ({os.Equipamento}) está pronto: {os.ValorOrcamento:C}. Aguardamos sua aprovação.",
                WorkflowStatus.AguardandoPecas => $"Olá {cliente.Nome}, estamos aguardando a chegada de peças para o reparo do seu equipamento '{os.Equipamento}' (OS #{os.Id}).",
                WorkflowStatus.EmReparo => $"Olá {cliente.Nome}, seu equipamento '{os.Equipamento}' (OS #{os.Id}) está em reparo.",
                WorkflowStatus.Concluido => $"Olá {cliente.Nome}, o reparo do seu equipamento '{os.Equipamento}' (OS #{os.Id}) foi concluído! Valor: {os.ValorOrcamento:C}. Já está disponível para retirada.",
                WorkflowStatus.Entregue => $"Olá {cliente.Nome}, confirmamos a entrega do seu equipamento '{os.Equipamento}' (OS #{os.Id}). Obrigado pela preferência!",
                _ => $"Olá {cliente.Nome}, o status da sua OS #{os.Id} ({os.Equipamento}) foi atualizado de '{statusAnterior}' para '{novoStatus}'."
            };
        }
    }
}
