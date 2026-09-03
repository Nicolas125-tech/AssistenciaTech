import re

with open("Controllers/TelegramWebhookController.cs", "r") as f:
    content = f.read()

# Add missing using statement if needed
if "using AssistenciaTech.Services.TelegramCommands;" not in content:
    content = content.replace("using System.Threading.Tasks;", "using AssistenciaTech.Services.TelegramCommands;\nusing System.Threading.Tasks;")

# Replace fields
content = re.sub(
    r"private readonly AppDbContext _context;\s*private readonly ILogger<TelegramWebhookController> _logger;\s*private readonly HttpClient _httpClient;\s*private readonly IConfiguration _configuration;",
    r"private readonly ILogger<TelegramWebhookController> _logger;\n        private readonly ITelegramCommandHandler _commandHandler;",
    content,
    flags=re.MULTILINE
)

# Replace constructor
content = re.sub(
    r"public TelegramWebhookController\(AppDbContext context, ILogger<TelegramWebhookController> logger, HttpClient httpClient, IConfiguration configuration\)\s*\{\s*_context = context;\s*_logger = logger;\s*_httpClient = httpClient;\s*_configuration = configuration;\s*\}",
    r"public TelegramWebhookController(ILogger<TelegramWebhookController> logger, ITelegramCommandHandler commandHandler)\n        {\n            _logger = logger;\n            _commandHandler = commandHandler;\n        }",
    content,
    flags=re.MULTILINE
)

# Replace Webhook method body inside the try block
webhook_body_pattern = r"string text = textElement\.GetString\(\) \?\? string\.Empty;\s*string chatId = chatIdElement\.GetInt64\(\)\.ToString\(\);\s*// O comando mágico.*?await EnviarMensagemConfirmacao\(chatId, \"Olá! Para receber notificações, você precisa clicar no link de ativação enviado pela Assistência\.\"\);\s*\}"

replacement = r"string text = textElement.GetString() ?? string.Empty;\n                    string chatId = chatIdElement.GetInt64().ToString();\n\n                    await _commandHandler.HandleCommandAsync(text, chatId);"

content = re.sub(webhook_body_pattern, replacement, content, flags=re.DOTALL)

# Remove EnviarMensagemConfirmacao method
enviar_method_pattern = r"\s*private async Task EnviarMensagemConfirmacao\(string chatId, string texto\)\s*\{.*?\}"
content = re.sub(enviar_method_pattern, "", content, flags=re.DOTALL)


with open("Controllers/TelegramWebhookController.cs", "w") as f:
    f.write(content)
