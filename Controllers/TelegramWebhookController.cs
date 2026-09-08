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
using AssistenciaTech.Services.TelegramCommands;
using System.Threading.Tasks;

namespace AssistenciaTech.Controllers
{
    [ApiController]
    [Route("api/telegram")]
    public class TelegramWebhookController : ControllerBase
    {
        private readonly ILogger<TelegramWebhookController> _logger;
        private readonly ITelegramCommandHandler _commandHandler;
        private readonly IConfiguration _configuration;

        public TelegramWebhookController(ILogger<TelegramWebhookController> logger, ITelegramCommandHandler commandHandler, IConfiguration configuration)
        {
            _logger = logger;
            _commandHandler = commandHandler;
            _configuration = configuration;
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook([FromBody] JsonElement update, [FromHeader(Name = "X-Telegram-Bot-Api-Secret-Token")] string? secretToken = null)
        {
            try
            {
                var configuredToken = _configuration["Telegram:WebhookSecretToken"];
                if (!string.IsNullOrEmpty(configuredToken) && secretToken != configuredToken)
                {
                    _logger.LogWarning("Unauthorized webhook request. Token mismatch.");
                    return Unauthorized();
                }
                // Verifica se há uma mensagem de texto no update
                if (update.TryGetProperty("message", out JsonElement message) &&
                    message.TryGetProperty("text", out JsonElement textElement) &&
                    message.TryGetProperty("chat", out JsonElement chat) &&
                    chat.TryGetProperty("id", out JsonElement chatIdElement))
                {
                    string text = textElement.GetString() ?? string.Empty;
                    string chatId = chatIdElement.GetInt64().ToString();

                    await _commandHandler.HandleCommandAsync(text, chatId);
                }

                return Ok(); // Sempre retorne 200 OK para o Telegram, senão ele fica repetindo o envio
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar Webhook do Telegram.");
                return Ok(); 
            }
        }
    }
}
