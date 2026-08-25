using AssistenciaTech.Data;
using AssistenciaTech.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AssistenciaTech.Controllers
{
    [ApiController]
    [Route("api/telegram")]
    public class TelegramWebhookController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<TelegramWebhookController> _logger;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public TelegramWebhookController(AppDbContext context, ILogger<TelegramWebhookController> logger, HttpClient httpClient, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _httpClient = httpClient;
            _configuration = configuration;
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook([FromBody] JsonElement update)
        {
            try
            {
                // Verifica se há uma mensagem de texto no update
                if (update.TryGetProperty("message", out JsonElement message) &&
                    message.TryGetProperty("text", out JsonElement textElement) &&
                    message.TryGetProperty("chat", out JsonElement chat) &&
                    chat.TryGetProperty("id", out JsonElement chatIdElement))
                {
                    string text = textElement.GetString() ?? string.Empty;
                    string chatId = chatIdElement.GetInt64().ToString();

                    // O comando mágico do Telegram é o /start seguido de um parâmetro oculto
                    // Exemplo: /start 15 (onde 15 é o ID do cliente)
                    if (text.StartsWith("/start "))
                    {
                        string clienteIdStr = text.Substring(7).Trim();
                        
                        if (int.TryParse(clienteIdStr, out int clienteId))
                        {
                            var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.Id == clienteId);

                            if (cliente != null)
                            {
                                // Atualiza o ID automaticamente no banco!
                                cliente.TelegramChatId = chatId;
                                await _context.SaveChangesAsync();

                                _logger.LogInformation("[Telegram Webhook] Chat ID {ChatId} vinculado com sucesso ao Cliente ID {ClienteId}", chatId, clienteId);

                                // Responde ao cliente confirmando
                                await EnviarMensagemConfirmacao(chatId, $"Olá {cliente.Nome}! ✅ Seu Telegram foi vinculado com sucesso. A partir de agora você receberá notificações das suas Ordens de Serviço por aqui.");
                            }
                            else
                            {
                                await EnviarMensagemConfirmacao(chatId, "❌ Cliente não encontrado. Por favor, solicite um novo link de vinculação.");
                            }
                        }
                    }
                    else if (text == "/start")
                    {
                        await EnviarMensagemConfirmacao(chatId, "Olá! Para receber notificações, você precisa clicar no link de ativação enviado pela Assistência.");
                    }
                }

                return Ok(); // Sempre retorne 200 OK para o Telegram, senão ele fica repetindo o envio
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar Webhook do Telegram.");
                return Ok(); 
            }
        }

        private async Task EnviarMensagemConfirmacao(string chatId, string texto)
        {
            var token = _configuration["TelegramBotToken"];
            if (string.IsNullOrEmpty(token)) return;

            var payload = new { chat_id = chatId, text = texto };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            
            await _httpClient.PostAsync($"https://api.telegram.org/bot{token}/sendMessage", content);
        }
    }
}
