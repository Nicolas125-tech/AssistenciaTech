using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AssistenciaTech.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AssistenciaTech.Data.Tests
{
    public class DbInitializerTests
    {
        private static ServiceProvider BuildTestServiceProvider(Dictionary<string, string?> configValues)
        {
            var services = new ServiceCollection();
            var dbName = Guid.NewGuid().ToString();

            services.AddLogging();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build());

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(dbName));

            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task InitializeAsync_WithValidCredentials_ShouldSeedAdminAndDemoUsersIdempotently()
        {
            using var provider = BuildTestServiceProvider(new Dictionary<string, string?>
            {
                ["AdminCredentials:Username"] = "admin_test",
                ["AdminCredentials:PasswordHash"] = "hashed_admin_pw",
                ["DemoCredentials:Username"] = "demo_test",
                ["DemoCredentials:PasswordHash"] = "hashed_demo_pw"
            });

            await DbInitializer.InitializeAsync(provider);
            await DbInitializer.InitializeAsync(provider); // Segunda chamada deve ser idempotente

            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var usuarios = await context.Usuarios.ToListAsync();

            usuarios.Should().HaveCount(2);
            usuarios.Should().Contain(u => u.Username == "admin_test" && u.Role == "Administrador");
            usuarios.Should().Contain(u => u.Username == "demo_test" && u.Role == "Administrador");
        }

        [Fact]
        public async Task InitializeAsync_WhenAdminPasswordHashMissing_ShouldThrowInvalidOperationException()
        {
            using var provider = BuildTestServiceProvider(new Dictionary<string, string?>
            {
                ["AdminCredentials:Username"] = "admin_test"
            });

            Func<Task> act = async () => await DbInitializer.InitializeAsync(provider);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*AdminCredentials:PasswordHash*");
        }
    }
}
