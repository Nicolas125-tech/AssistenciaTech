using AssistenciaTech.Models;
using Microsoft.EntityFrameworkCore;

namespace AssistenciaTech.Data
{
    /// <summary>
    /// Contexto do banco de dados do Entity Framework Core.
    /// Gerencia a conexão com o banco e o mapeamento das entidades.
    /// </summary>
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Representação das tabelas no banco de dados
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<OrdemServico> OrdensServico { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Mapeamento extra (Fluent API), se necessário
            modelBuilder.Entity<OrdemServico>()
                .Property(o => o.ValorOrcamento)
                .HasColumnType("decimal(18,2)");
        }
    }
}
