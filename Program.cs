using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
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
builder.Services.AddScoped<IEstoqueService, EstoqueService>();
builder.Services.AddScoped<IPdfGeneratorService, PdfGeneratorService>();
builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();
builder.Services.AddScoped<IEquipamentoBackupService, EquipamentoBackupService>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<INotificationService, TelegramNotificationService>();

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
            connBuilder.TrustServerCertificate = true;
        }

        connectionString = connBuilder.ConnectionString;
        Console.WriteLine($"[Neon/Postgres] Converted URI to connection string for Host: {connBuilder.Host}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Neon/Postgres] Error parsing URI connection string: {ex.Message}");
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

// Configuração do Data Protection (Persistindo chaves no banco de dados para suportar reinícios do contêiner)
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>();

// Configuração de Autenticação baseada em Cookies e JWT
var jwtKey = builder.Configuration["Jwt:Key"] ?? "UmaChaveSuperSecretaMuitoLongaParaOJWT12345!";
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

app.UseForwardedHeaders(); // Movido para o topo do pipeline

// === ADICIONADO: Auto-Migration para produção/Docker ===
// Garante que o banco de dados seja criado e atualizado automaticamente ao iniciar a aplicação.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        // Cria as tabelas do banco automaticamente sem precisar da pasta Migrations
        context.Database.EnsureCreated();

        // Migração manual: Adiciona novas colunas caso o banco já tenha sido criado antes (pois o EnsureCreated não altera tabelas existentes)
        context.Database.ExecuteSqlRaw(@"ALTER TABLE ""Clientes"" ADD COLUMN IF NOT EXISTS ""TelegramChatId"" text NULL;");

        // --- SEED ADMIN USER ---
        var config = services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        var adminUser = config["AdminCredentials:Username"] ?? "admin";
        var adminHash = config["AdminCredentials:PasswordHash"];

        if (string.IsNullOrEmpty(adminHash))
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogCritical("CRITICAL SECURITY ERROR: The administrator password hash is not configured. You must provide a secure password hash via the 'AdminCredentials:PasswordHash' configuration or environment variable (AdminCredentials__PasswordHash).");
            throw new InvalidOperationException("Missing required configuration: AdminCredentials:PasswordHash is not set.");
        }

        if (!context.Usuarios.Any(u => u.Username == adminUser))
        {
            context.Usuarios.Add(new AssistenciaTech.Models.Usuario
            {
                Username = adminUser,
                PasswordHash = adminHash,
                Role = "Administrador"
            });
            context.SaveChanges();
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Usuário administrador padrão criado com sucesso.");
        }
        // -----------------------

        context.Database.ExecuteSqlRaw(@"
            ALTER TABLE ""OrdensServico"" ADD COLUMN IF NOT EXISTS ""CustoPecas"" numeric NOT NULL DEFAULT 0;
            ALTER TABLE ""OrdensServico"" ADD COLUMN IF NOT EXISTS ""CustoMaoDeObra"" numeric NOT NULL DEFAULT 0;
            ALTER TABLE ""OrdensServico"" ADD COLUMN IF NOT EXISTS ""DescontoAplicado"" numeric NOT NULL DEFAULT 0;
            ALTER TABLE ""OrdensServico"" ADD COLUMN IF NOT EXISTS ""DataConclusao"" timestamp without time zone NULL;
            ALTER TABLE ""OrdensServico"" ADD COLUMN IF NOT EXISTS ""DataEntregaCliente"" timestamp without time zone NULL;
            ALTER TABLE ""OrdensServico"" ADD COLUMN IF NOT EXISTS ""AvariasPreExistentes"" text NULL;
            ALTER TABLE ""OrdensServico"" ADD COLUMN IF NOT EXISTS ""NumeroSerie"" text NULL;
            ALTER TABLE ""OrdensServico"" ADD COLUMN IF NOT EXISTS ""AnotacoesInternas"" text NULL;
            ALTER TABLE ""OrdensServico"" ADD COLUMN IF NOT EXISTS ""Prioridade"" integer NOT NULL DEFAULT 0;

            CREATE TABLE IF NOT EXISTS ""Pecas"" (
                ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""Nome"" text NOT NULL,
                ""QuantidadeEstoque"" integer NOT NULL,
                ""ValorUnitario"" numeric NOT NULL
            );
            ALTER TABLE ""Pecas"" ADD COLUMN IF NOT EXISTS ""QuantidadeMinima"" integer NOT NULL DEFAULT 0;

            CREATE TABLE IF NOT EXISTS ""OrdemServicoPecas"" (
                ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""OrdemServicoId"" integer NOT NULL REFERENCES ""OrdensServico"" (""Id"") ON DELETE CASCADE,
                ""PecaId"" integer NOT NULL REFERENCES ""Pecas"" (""Id"") ON DELETE CASCADE,
                ""Quantidade"" integer NOT NULL,
                ""ValorVenda"" numeric NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ""Evidencias"" (
                ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""OrdemServicoId"" integer NOT NULL REFERENCES ""OrdensServico"" (""Id"") ON DELETE CASCADE,
                ""CaminhoArquivo"" text NOT NULL,
                ""DataUpload"" timestamp without time zone NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS ""EquipamentosBackup"" (
                ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""Descricao"" text NOT NULL,
                ""NumeroSerie"" text NULL,
                ""Disponivel"" boolean NOT NULL DEFAULT TRUE
            );

            CREATE TABLE IF NOT EXISTS ""Tecnicos"" (
                ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""Nome"" text NOT NULL,
                ""PercentualComissao"" numeric NOT NULL DEFAULT 0,
                ""Ativo"" boolean NOT NULL DEFAULT TRUE
            );

            ALTER TABLE ""OrdensServico"" ADD COLUMN IF NOT EXISTS ""LaudoTecnico"" text NULL;
            ALTER TABLE ""OrdensServico"" ADD COLUMN IF NOT EXISTS ""EquipamentoBackupId"" integer NULL REFERENCES ""EquipamentosBackup"" (""Id"") ON DELETE SET NULL;
            ALTER TABLE ""OrdensServico"" ADD COLUMN IF NOT EXISTS ""TecnicoId"" integer NULL REFERENCES ""Tecnicos"" (""Id"") ON DELETE SET NULL;
            ALTER TABLE ""OrdensServico"" ADD COLUMN IF NOT EXISTS ""EnviadoParaTerceiro"" boolean NOT NULL DEFAULT FALSE;
            ALTER TABLE ""OrdensServico"" ADD COLUMN IF NOT EXISTS ""NomeParceiro"" text NULL;
            ALTER TABLE ""OrdensServico"" ADD COLUMN IF NOT EXISTS ""CustoTerceirizado"" numeric NOT NULL DEFAULT 0;
            ALTER TABLE ""OrdensServico"" ADD COLUMN IF NOT EXISTS ""PrevisaoRetornoParceiro"" timestamp without time zone NULL;


            -- Enterprise Nivel 3
            CREATE TABLE IF NOT EXISTS ""Contratos"" (
                ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""ClienteId"" integer NOT NULL REFERENCES ""Clientes"" (""Id"") ON DELETE CASCADE,
                ""DataInicio"" timestamp without time zone NOT NULL,
                ""DataFim"" timestamp without time zone NULL,
                ""HorasSLA"" integer NOT NULL DEFAULT 4,
                ""FranquiaPaginas"" integer NULL,
                ""ValorMensal"" numeric NOT NULL
            );

            ALTER TABLE ""OrdensServico"" ADD COLUMN IF NOT EXISTS ""ContratoId"" integer NULL REFERENCES ""Contratos"" (""Id"") ON DELETE SET NULL;
            ALTER TABLE ""OrdensServico"" ADD COLUMN IF NOT EXISTS ""ContadorPaginasInicial"" integer NULL;
            ALTER TABLE ""OrdensServico"" ADD COLUMN IF NOT EXISTS ""ContadorPaginasFinal"" integer NULL;

            CREATE TABLE IF NOT EXISTS ""Faturamentos"" (
                ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""OrdemServicoId"" integer NOT NULL REFERENCES ""OrdensServico"" (""Id"") ON DELETE CASCADE,
                ""ValorTotal"" numeric NOT NULL,
                ""DataVencimento"" timestamp without time zone NOT NULL,
                ""StatusPagamento"" integer NOT NULL DEFAULT 0,
                ""TxIdPix"" text NULL,
                ""QrCodePayload"" text NULL
            );
            ALTER TABLE ""Faturamentos"" ADD COLUMN IF NOT EXISTS ""BaseCalculoISS"" numeric NOT NULL DEFAULT 0;
            ALTER TABLE ""Faturamentos"" ADD COLUMN IF NOT EXISTS ""AliquotaISS"" numeric NOT NULL DEFAULT 0;
            ALTER TABLE ""Faturamentos"" ADD COLUMN IF NOT EXISTS ""ValorISS"" numeric NOT NULL DEFAULT 0;
            ALTER TABLE ""Faturamentos"" ADD COLUMN IF NOT EXISTS ""BaseCalculoICMS"" numeric NOT NULL DEFAULT 0;
            ALTER TABLE ""Faturamentos"" ADD COLUMN IF NOT EXISTS ""AliquotaICMS"" numeric NOT NULL DEFAULT 0;
            ALTER TABLE ""Faturamentos"" ADD COLUMN IF NOT EXISTS ""ValorICMS"" numeric NOT NULL DEFAULT 0;

            CREATE TABLE IF NOT EXISTS ""VisitasCampo"" (
                ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""OrdemServicoId"" integer NOT NULL REFERENCES ""OrdensServico"" (""Id"") ON DELETE CASCADE,
                ""TecnicoId"" integer NOT NULL REFERENCES ""Tecnicos"" (""Id"") ON DELETE CASCADE,
                ""CheckIn"" timestamp without time zone NOT NULL,
                ""CheckOut"" timestamp without time zone NULL,
                ""Latitude"" numeric NULL,
                ""Longitude"" numeric NULL,
                ""AssinaturaClienteBase64"" text NULL
            );

            CREATE TABLE IF NOT EXISTS ""AuditoriaOS"" (
                ""Id"" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                ""OrdemServicoId"" integer NOT NULL,
                ""Usuario"" text NOT NULL,
                ""DataAlteracao"" timestamp without time zone NOT NULL,
                ""CampoAlterado"" text NULL,
                ""ValorAntigo"" text NULL,
                ""ValorNovo"" text NULL
            );
            ALTER TABLE ""AuditoriaOS"" ADD COLUMN IF NOT EXISTS ""DetalhesAlteracao"" text NULL;
            ALTER TABLE ""AuditoriaOS"" ALTER COLUMN ""CampoAlterado"" DROP NOT NULL;
        ");

        // Garante que a tabela de chaves de proteção exista, pois o EnsureCreated ignora se o banco já existir
        context.Database.ExecuteSqlRaw(@"
            CREATE TABLE IF NOT EXISTS ""DataProtectionKeys"" (
                ""Id"" integer GENERATED BY DEFAULT AS IDENTITY,
                ""FriendlyName"" text NULL,
                ""Xml"" text NULL,
                CONSTRAINT ""PK_DataProtectionKeys"" PRIMARY KEY (""Id"")
            );
        ");

        // --- Seed do Usuário Demo ---
        var demoUsername = "demo@assistenciatech.com";
        var demoUser = context.Usuarios.FirstOrDefault(u => u.Username == demoUsername);
        if (demoUser == null)
        {
            var newUser = new AssistenciaTech.Models.Usuario
            {
                Username = demoUsername,
                Role = "Administrador"
            };
            
            var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<AssistenciaTech.Models.Usuario>();
            newUser.PasswordHash = hasher.HashPassword(newUser, "Demo@1234");
            
            context.Usuarios.Add(newUser);
            context.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Ocorreu um erro ao aplicar as migrations do banco de dados.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}


app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Middlewares de Segurança
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
