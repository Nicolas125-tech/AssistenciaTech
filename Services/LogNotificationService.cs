using AssistenciaTech.Models;
using Microsoft.Extensions.Configuration;
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
        private readonly IConfiguration _configuration;

        public LogNotificationService(ILogger<LogNotificationService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public Task EnviarNotificacaoStatusAsync(Cliente cliente, OrdemServico os, string statusAnterior)
        {
            if (cliente == null)
            {
                _logger.LogWarning("[Notificação] Tentativa de notificação para OS #{OsId} sem cliente associado.", os.Id);
                return Task.CompletedTask;
            }

            var mensagem = NotificationMessageHelper.GerarMensagem(_configuration, cliente, os, statusAnterior);

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
    }
}
