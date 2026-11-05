using System;
using System.Linq;
using System.Threading.Tasks;
using AssistenciaTech.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AssistenciaTech.Data
{
    /// <summary>
    /// Inicializador formal do banco de dados (EF Core Migrations + Seed de usuários).
    /// Substitui blocos de SQL inline do Program.cs mantendo compatibilidade com bancos existentes.
    /// </summary>
    public static class DbInitializer
    {
        public static async Task InitializeAsync(IServiceProvider rootProvider)
        {
            using var scope = rootProvider.CreateScope();
            var services = scope.ServiceProvider;
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");

            try
            {
                var context = services.GetRequiredService<AppDbContext>();
                var config = services.GetRequiredService<IConfiguration>();

                if (context.Database.IsRelational())
                {
                    var migrations = context.Database.GetMigrations().ToList();
                    if (migrations.Count > 0)
                    {
                        try
                        {
                            await context.Database.MigrateAsync();
                        }
                        catch (Exception migrateEx) when (migrateEx.Message.Contains("42P07") || migrateEx.Message.Contains("already exists"))
                        {
                            logger.LogWarning("Banco legado detectado (criado anteriormente via EnsureCreated). Sincronizando histórico de migrations.");
                            await EnsureMigrationHistoryBaselineAsync(context, migrations.First());
                            await context.Database.MigrateAsync();
                        }
                    }
                    else
                    {
                        await context.Database.EnsureCreatedAsync();
                    }
                }
                else
                {
                    await context.Database.EnsureCreatedAsync();
                }

                await SeedUsersAsync(context, config, logger);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ocorreu um erro ao inicializar ou aplicar migrations no banco de dados.");
            }
        }

        private static async Task EnsureMigrationHistoryBaselineAsync(AppDbContext context, string initialMigrationId)
        {
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (
                    ""MigrationId"" character varying(150) NOT NULL,
                    ""ProductVersion"" character varying(32) NOT NULL,
                    CONSTRAINT ""PK___EFMigrationsHistory"" PRIMARY KEY (""MigrationId"")
                );");

            await context.Database.ExecuteSqlInterpolatedAsync($@"
                INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
                VALUES ({initialMigrationId}, {"8.0.0"})
                ON CONFLICT (""MigrationId"") DO NOTHING;");
        }

        private static async Task SeedUsersAsync(AppDbContext context, IConfiguration config, ILogger logger)
        {
            var adminUser = config["AdminCredentials:Username"] ?? "admin";
            var adminHash = config["AdminCredentials:PasswordHash"];

            if (string.IsNullOrEmpty(adminHash))
            {
                logger.LogCritical("CRITICAL SECURITY ERROR: The administrator password hash is not configured. Provide 'AdminCredentials:PasswordHash'.");
                throw new InvalidOperationException("Missing required configuration: AdminCredentials:PasswordHash is not set.");
            }

            if (!await context.Usuarios.AnyAsync(u => u.Username == adminUser))
            {
                context.Usuarios.Add(new Usuario
                {
                    Username = adminUser,
                    PasswordHash = adminHash,
                    Role = "Administrador"
                });
                await context.SaveChangesAsync();
                logger.LogInformation("Usuário administrador padrão criado com sucesso.");
            }

            var demoUsername = config["DemoCredentials:Username"] ?? "demo@assistenciatech.com";
            var demoPasswordHash = config["DemoCredentials:PasswordHash"];

            if (!string.IsNullOrEmpty(demoPasswordHash) && !await context.Usuarios.AnyAsync(u => u.Username == demoUsername))
            {
                context.Usuarios.Add(new Usuario
                {
                    Username = demoUsername,
                    PasswordHash = demoPasswordHash,
                    Role = "Administrador"
                });
                await context.SaveChangesAsync();
                logger.LogInformation("Usuário de demonstração criado com sucesso.");
            }
        }
    }
}
