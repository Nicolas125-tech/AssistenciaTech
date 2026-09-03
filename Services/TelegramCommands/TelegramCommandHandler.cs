using AssistenciaTech.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AssistenciaTech.Services.TelegramCommands
{
    public class TelegramCommandHandler : ITelegramCommandHandler
    {
        private readonly AppDbContext _context;
        private readonly ILogger<TelegramCommandHandler> _logger;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public TelegramCommandHandler(
            AppDbContext context,
            ILogger<TelegramCommandHandler> logger,
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task HandleCommandAsync(string text, string chatId)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

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
