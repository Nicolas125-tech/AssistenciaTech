import sys

with open('Program.cs', 'r') as f:
    content = f.read()

# Add using
if 'using Microsoft.AspNetCore.RateLimiting;' not in content:
    content = content.replace('using System.Globalization;', 'using System.Globalization;\nusing Microsoft.AspNetCore.RateLimiting;\nusing System.Threading.RateLimiting;')

# Add AddRateLimiter
rate_limiter_code = """
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("LoginRateLimit", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        await context.HttpContext.Response.WriteAsync("Muitas tentativas de login. Tente novamente em 1 minuto.", cancellationToken: token);
    };
});
"""

if 'builder.Services.AddRateLimiter' not in content:
    content = content.replace('var builder = WebApplication.CreateBuilder(args);', f'var builder = WebApplication.CreateBuilder(args);\n{rate_limiter_code}')


with open('Program.cs', 'w') as f:
    f.write(content)

print("Program.cs patched successfully.")
