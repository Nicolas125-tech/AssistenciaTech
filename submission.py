import json

with open("pr_data.json", "w") as f:
    json.dump({
        "title": "🧪 Add unit tests for TelegramNotificationService",
        "description": "🎯 **What:** The TelegramNotificationService lacked unit tests to verify its core notification logic and API interactions.\n  \n📊 **Coverage:** The following scenarios are now tested:\n- Aborting notification early when the client is null.\n- Aborting notification when the Telegram Bot Token is missing in configuration.\n- Aborting notification when the client has an empty Chat ID.\n- Successfully sending a notification with valid parameters (mocking HTTP success).\n- Gracefully handling and logging non-success HTTP API responses without throwing exceptions.\n- Gracefully handling generic exceptions (e.g. network errors) without throwing.\n\n✨ **Result:** Enhanced code reliability by ensuring the notification service correctly interprets inputs, manages its external dependencies, and suppresses transient errors from surfacing to the main workflow."
    }, f)
