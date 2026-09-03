import re

with open("Controllers/TelegramWebhookController.cs", "r") as f:
    content = f.read()

# Fix the trailing garbage
content = re.sub(r"};\s*var content = new StringContent\(.*?\}\s*\}\s*\}", r"    }\n}", content, flags=re.DOTALL)

with open("Controllers/TelegramWebhookController.cs", "w") as f:
    f.write(content)
