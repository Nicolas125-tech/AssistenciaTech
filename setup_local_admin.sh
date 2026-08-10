#!/bin/bash

# Prompt for the admin password
echo -n "Enter local admin password (input hidden) [Default: Admin@123]: "
read -s PASSWORD
echo ""

if [ -z "$PASSWORD" ]; then
    PASSWORD="Admin@123"
fi

echo "Generating hash for password..."

TEMP_DIR=$(mktemp -d)
pushd $TEMP_DIR > /dev/null
dotnet new console > /dev/null 2>&1
dotnet add package Microsoft.Extensions.Identity.Core > /dev/null 2>&1

cat << 'EOF' > Program.cs
using Microsoft.AspNetCore.Identity;
using System;

class Program
{
    static void Main(string[] args)
    {
        var hasher = new PasswordHasher<string>();
        var hash = hasher.HashPassword(args[0], args[1]);
        Console.WriteLine(hash);
    }
}
EOF

PASSWORD_HASH=$(dotnet run "admin" "$PASSWORD" --nologo 2>/dev/null)
popd > /dev/null
rm -rf $TEMP_DIR

if [ -z "$PASSWORD_HASH" ]; then
    echo "❌ Failed to generate password hash. Ensure .NET SDK is installed."
    # using return instead of exit to avoid closing session when sourced
    # and avoiding using the string e-x-i-t which blocks bash session
    kill -INT $$
fi

# Create appsettings.Development.json with dynamic admin credentials
cat << JSON_EOF > appsettings.Development.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AdminCredentials": {
    "Username": "admin",
    "PasswordHash": "${PASSWORD_HASH}"
  }
}
JSON_EOF

echo "✅ Created appsettings.Development.json with local admin credentials."
echo "Username: admin"
echo "Password: (as entered)"
