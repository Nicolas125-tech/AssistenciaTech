import sys

with open('Controllers/AccountController.cs', 'r') as f:
    content = f.read()

# Add using
if 'using Microsoft.AspNetCore.RateLimiting;' not in content:
    content = content.replace('using System.Text;', 'using System.Text;\nusing Microsoft.AspNetCore.RateLimiting;')

# Add attribute
target = """        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]"""

replacement = """        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("LoginRateLimit")]"""

if '[EnableRateLimiting("LoginRateLimit")]' not in content:
    content = content.replace(target, replacement)

with open('Controllers/AccountController.cs', 'w') as f:
    f.write(content)

print("Controllers/AccountController.cs patched successfully.")
