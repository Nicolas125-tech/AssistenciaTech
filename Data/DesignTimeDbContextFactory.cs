using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AssistenciaTech.Data
{
    /// <summary>
    /// Fábrica em tempo de design para geração de EF Core Migrations sem depender do host completo.
    /// </summary>
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseNpgsql("Host=localhost;Database=assistenciatech_design;Username=postgres;Password=postgres");
            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
