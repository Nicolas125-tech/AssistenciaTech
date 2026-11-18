using System;
using System.Text.Json;
using System.Threading.Tasks;
using AssistenciaTech.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AssistenciaTech.Controllers
{
    [ApiController]
    [Route("api/telegram")]
    public class TelegramWebhookController : ControllerBase
    {
        private readonly ILogger<TelegramWebhookController> _logger;
        private readonly IMensageriaEngine _mensageria;

        public TelegramWebhookController(ILogger<TelegramWebhookController> logger, IMensageriaEngine mensageria)
        {
            _logger = logger;
            _mensageria = mensageria;
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook(
            [FromBody] JsonElement update,
            [FromHeader(Name = "X-Telegram-Bot-Api-Secret-Token")] string? secretToken = null)
        {
            try
            {
                var ct = HttpContext?.RequestAborted ?? default;
                var result = await _mensageria.ProcessarWebhookAsync(update, secretToken, ct);

                if (result.Status == WebhookProcessStatus.NaoAutorizado)
                {
                    return Unauthorized();
                }

                return Ok(); // Sempre retorna 200 OK para updates válidos/ignorados para evitar retries em loop do Telegram
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar Webhook do Telegram.");
                return Ok();
            }
        }
    }
}
