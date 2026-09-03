import re

with open("Program.cs", "r") as f:
    content = f.read()

if "builder.Services.AddScoped<ITelegramCommandHandler, TelegramCommandHandler>();" not in content and "ITelegramCommandHandler" not in content:
    content = content.replace(
        "builder.Services.AddHttpClient<INotificationService, TelegramNotificationService>();",
        "builder.Services.AddHttpClient<INotificationService, TelegramNotificationService>();\nbuilder.Services.AddHttpClient();\nbuilder.Services.AddScoped<AssistenciaTech.Services.TelegramCommands.ITelegramCommandHandler, AssistenciaTech.Services.TelegramCommands.TelegramCommandHandler>();"
    )

with open("Program.cs", "w") as f:
    f.write(content)
