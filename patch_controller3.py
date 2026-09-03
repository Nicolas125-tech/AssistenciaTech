with open("Controllers/TelegramWebhookController.cs", "r") as f:
    content = f.read()

content = content.replace("            }\n}", "        }\n    }\n}")

with open("Controllers/TelegramWebhookController.cs", "w") as f:
    f.write(content)
