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
using AssistenciaTech.Services.TelegramCommands;
using System.Text.Json;
using System.Threading.Tasks;

namespace AssistenciaTech.Controllers
{
    [ApiController]
    [Route("api/telegram")]
    public class TelegramWebhookController : ControllerBase
    {
        private readonly ILogger<TelegramWebhookController> _logger;
        private readonly ITelegramCommandHandler _commandHandler;

        public TelegramWebhookController(ILogger<TelegramWebhookController> logger, ITelegramCommandHandler commandHandler)
        {
            _logger = logger;
            _commandHandler = commandHandler;
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
