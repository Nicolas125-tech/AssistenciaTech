using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace AssistenciaTech.Services
{
    public enum MensageriaTransportMode
    {
        Auto,
        Telegram,
        Log
    }

    /// <summary>
    /// Configurações tipadas para notificações e integração com o Bot do Telegram.
    /// </summary>
    public class MensageriaOptions
    {
        public string? BotToken { get; set; }
        public string? WebhookSecretToken { get; set; }
        public MensageriaTransportMode TransportMode { get; set; } = MensageriaTransportMode.Auto;
        public int MaxRetries { get; set; } = 2;
        public int RetryBaseDelayMs { get; set; } = 250;
        public Dictionary<string, string> Templates { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public bool DeveUsarTelegramApi() =>
            TransportMode == MensageriaTransportMode.Telegram ||
            (TransportMode == MensageriaTransportMode.Auto && !string.IsNullOrWhiteSpace(BotToken));

        public void BindFromConfiguration(IConfiguration configuration)
        {
            configuration.GetSection("Telegram").Bind(this);
            if (string.IsNullOrWhiteSpace(BotToken))
            {
                BotToken = configuration["TelegramBotToken"];
            }

            foreach (var child in configuration.GetSection("NotificationTemplates").GetChildren())
            {
                if (!string.IsNullOrEmpty(child.Value))
                {
                    Templates[child.Key] = child.Value;
                }
            }
        }
    }
}
