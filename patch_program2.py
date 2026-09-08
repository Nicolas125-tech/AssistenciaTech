import sys

with open('Program.cs', 'r') as f:
    content = f.read()

# Add UseRateLimiter
if 'app.UseRateLimiter();' not in content:
    content = content.replace('app.UseRouting();', 'app.UseRouting();\napp.UseRateLimiter();')

with open('Program.cs', 'w') as f:
    f.write(content)

print("Program.cs patched successfully 2.")
