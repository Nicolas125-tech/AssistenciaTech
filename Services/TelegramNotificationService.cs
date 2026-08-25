using AssistenciaTech.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AssistenciaTech.Services
{
    public class TelegramNotificationService : INotificationService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TelegramNotificationService> _logger;

        public TelegramNotificationService(HttpClient httpClient, IConfiguration configuration, ILogger<TelegramNotificationService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task EnviarNotificacaoStatusAsync(Cliente cliente, OrdemServico os, string statusAnterior)
        {
            if (cliente == null)
            {
                _logger.LogWarning("[Notificação] Tentativa de notificação para OS #{OsId} sem cliente associado.", os.Id);
                return;
            }

            var token = _configuration["TelegramBotToken"];
            
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(cliente.TelegramChatId))
            {
                _logger.LogWarning("[Telegram] Token do bot não configurado ou cliente {ClienteNome} (ID: {ClienteId}) não possui TelegramChatId.", cliente.Nome, cliente.Id);
                return;
            }

            var mensagem = GerarMensagem(cliente, os, statusAnterior);

            var payload = new
            {
                chat_id = cliente.TelegramChatId,
                text = mensagem
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync($"https://api.telegram.org/bot{token}/sendMessage", content);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("[Telegram] Notificação enviada para {ClienteNome} (ChatId: {ChatId}): {Mensagem}", cliente.Nome, cliente.TelegramChatId, mensagem);
                }
                else
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError("[Telegram] Falha ao enviar notificação para {ClienteNome}. Status: {StatusCode}, Resposta: {Response}", cliente.Nome, response.StatusCode, responseBody);
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "[Telegram] Erro ao enviar notificação para {ClienteNome}.", cliente.Nome);
            }
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
