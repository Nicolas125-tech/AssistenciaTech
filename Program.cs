using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using AssistenciaTech.Data;
using AssistenciaTech.Services;

using Npgsql;

// Workaround para erro de timezone no PostgreSQL ("Cannot write DateTime with Kind=Local")
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<IEstoqueService, EstoqueService>();
builder.Services.AddScoped<IPdfGeneratorService, PdfGeneratorService>();

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
            Password = userInfo.Length > 1 ? userInfo[1] : "",
            Database = uri.LocalPath.TrimStart('/')
        };
        
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

// Configuração de Autenticação baseada em Cookies
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
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
                ""CampoAlterado"" text NOT NULL,
                ""ValorAntigo"" text NULL,
                ""ValorNovo"" text NULL
            );
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
