using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using AssistenciaTech.Data;
using AssistenciaTech.Services;

using Npgsql;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<AssistenciaTech.Filters.DemoModeFilter>();
});
builder.Services.AddProblemDetails();
builder.Services.AddScoped<IEstoqueService, EstoqueService>();
builder.Services.AddScoped<IPdfGeneratorService, PdfGeneratorService>();
builder.Services.AddScoped<IClienteService, ClienteService>();
builder.Services.AddScoped<IAdminFacade, AdminFacade>();
builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();
builder.Services.AddScoped<IEquipamentoBackupService, EquipamentoBackupService>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<INotificationService, TelegramNotificationService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<AssistenciaTech.Services.TelegramCommands.ITelegramCommandHandler, AssistenciaTech.Services.TelegramCommands.TelegramCommandHandler>();

// Configurando o Redis (IDistributedCache)
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
// Se não achar, tenta ler da variável de ambiente do Render diretamente
if (string.IsNullOrEmpty(redisConnectionString))
{
    redisConnectionString = Environment.GetEnvironmentVariable("REDIS_URL");
}
if (!string.IsNullOrEmpty(redisConnectionString))
{
    // Garante timeouts curtos e abortConnect=false para não travar a app se o Redis estiver fora
    if (!redisConnectionString.Contains("abortConnect", StringComparison.OrdinalIgnoreCase))
    {
        redisConnectionString += ",abortConnect=false";
    }
    if (!redisConnectionString.Contains("connectTimeout", StringComparison.OrdinalIgnoreCase))
    {
        redisConnectionString += ",connectTimeout=2000,syncTimeout=2000";
    }

    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
        options.InstanceName = "AssistenciaTech_";
    });
    Console.WriteLine("[Cache] Redis configurado com sucesso.");
}
else
{
    // Fallback para memória se o Redis não estiver configurado
    builder.Services.AddDistributedMemoryCache();
    Console.WriteLine("[Cache] Redis não configurado. Usando cache em memória (fallback).");
}

// Registra o serviço de cache resiliente com circuit breaker
builder.Services.AddSingleton<AssistenciaTech.Extensions.IResilientCacheService, AssistenciaTech.Extensions.ResilientCacheService>();

builder.Services.AddScoped<AssistenciaTech.Services.ITributacaoService, AssistenciaTech.Services.TributacaoService>();
builder.Services.AddScoped<AssistenciaTech.Services.INfseXmlGeneratorService, AssistenciaTech.Services.NfseXmlGeneratorService>();


// Registrando o contexto do banco de dados PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (!string.IsNullOrEmpty(connectionString) && (connectionString.StartsWith("postgres://") || connectionString.StartsWith("postgresql://")))
{
    try
    {
        var uri = new Uri(connectionString);
        var userInfo = uri.UserInfo.Split(':');
        var connBuilder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Username = userInfo[0],
            Database = uri.LocalPath.TrimStart('/')
        };

        if (userInfo.Length > 1)
        {
            connBuilder.Password = userInfo[1];
        }

        if (connectionString.Contains("sslmode=require", StringComparison.OrdinalIgnoreCase))
        {
            connBuilder.SslMode = SslMode.Require;
#pragma warning disable CS0618
            connBuilder.TrustServerCertificate = true;
#pragma warning restore CS0618
        }

        connectionString = connBuilder.ConnectionString;
        Console.WriteLine($"[Neon/Postgres] Converted URI to connection string for Host: {connBuilder.Host}");
    }
    catch (Exception ex)
    {
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
        var logger = loggerFactory.CreateLogger<Program>();
        logger.LogError(ex, "[Neon/Postgres] Error parsing URI connection string");
    }
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AssistenciaTech.Data.AuditoriaInterceptor>();

builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    var interceptor = serviceProvider.GetRequiredService<AssistenciaTech.Data.AuditoriaInterceptor>();
    options.UseNpgsql(connectionString)
           .AddInterceptors(interceptor);
});

builder.Services.AddHealthChecks()
    .AddCheck<TechOsHealthCheck>("techos_readiness");

// Configuração do Data Protection (Persistindo chaves no banco de dados para suportar reinícios do contêiner)
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>();

// Configuração de Autenticação baseada em Cookies e JWT
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured. Please set the 'Jwt:Key' configuration value.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "AssistenciaTech";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "AssistenciaTechMobile";

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false; // Em produção, configure como true se usar HTTPS
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

// Configuração da Licença do QuestPDF
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;


// Configuração para proxy reverso (Render, Heroku, etc.)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

var supportedCultures = new[] { "pt-BR" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("pt-BR")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);

app.UseForwardedHeaders();

// Inicialização segura do banco de dados (EnsureCreated + Schema updates idempotentes + Seed)
await DbInitializer.InitializeAsync(app.Services);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseStatusCodePages();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Middlewares de Segurança
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/healthz");
app.MapHealthChecks("/ready");

app.MapGet("/health", async (HealthCheckService healthCheckService) =>
{
    var report = await healthCheckService.CheckHealthAsync();
    var rawCommit = Environment.GetEnvironmentVariable("RENDER_GIT_COMMIT") 
                    ?? Environment.GetEnvironmentVariable("GIT_COMMIT") 
                    ?? "dev";
    var shortCommit = rawCommit.Length > 7 ? rawCommit.Substring(0, 7) : rawCommit;

    var response = new
    {
        status = report.Status.ToString(),
        version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0",
        commit = shortCommit,
        fullCommit = rawCommit,
        timestampUtc = DateTime.UtcNow,
        durationMs = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.Select(e => new
        {
            name = e.Key,
            status = e.Value.Status.ToString(),
            description = e.Value.Description,
            data = e.Value.Data
        })
    };

    var statusCode = report.Status == HealthStatus.Unhealthy ? 503 : 200;
    return Results.Json(response, statusCode: statusCode);
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
