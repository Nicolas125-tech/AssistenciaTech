#!/bin/bash

# Create appsettings.Development.json with default admin credentials
cat << 'JSON_EOF' > appsettings.Development.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AdminCredentials": {
    "Username": "admin",
    "PasswordHash": "AQAAAAIAAYagAAAAEP8jnf/36JUclmSKVmjO9NTodPkHD3jqBqbLkjxMqKL7iqkqsAV91XKIsH7BDFFZMw=="
  }
}
JSON_EOF

echo "✅ Created appsettings.Development.json with local admin credentials."
echo "Username: admin"
echo "Password: Admin@123"
