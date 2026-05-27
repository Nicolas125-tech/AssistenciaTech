using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using AssistenciaTech.Data;

using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Registrando o contexto do banco de dados PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Força a resolução do Host para IPv4 para evitar falhas de IPv6 no Render
if (!string.IsNullOrEmpty(connectionString))
{
    try
    {
        var connBuilder = new NpgsqlConnectionStringBuilder(connectionString);
        if (!string.IsNullOrEmpty(connBuilder.Host) && !System.Net.IPAddress.TryParse(connBuilder.Host, out _))
        {
            var addresses = System.Net.Dns.GetHostAddresses(connBuilder.Host);
            var ipv4 = addresses.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            if (ipv4 != null)
            {
                Console.WriteLine($"[DNS] Resolvendo {connBuilder.Host} para IPv4: {ipv4}");
                connBuilder.Host = ipv4.ToString();
                connectionString = connBuilder.ConnectionString;
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DNS] Erro ao resolver host para IPv4: {ex.Message}");
    }
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

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
