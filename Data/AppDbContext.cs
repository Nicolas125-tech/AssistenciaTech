using AssistenciaTech.Models;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AssistenciaTech.Data
{
    /// <summary>
    /// Contexto do banco de dados do Entity Framework Core.
    /// Gerencia a conexão com o banco e o mapeamento das entidades.
    /// </summary>
    public class AppDbContext : DbContext, IDataProtectionKeyContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Representação das tabelas no banco de dados
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<OrdemServico> OrdensServico { get; set; }
        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

        public DbSet<Peca> Pecas { get; set; }
        public DbSet<Evidencia> Evidencias { get; set; }
        public DbSet<OrdemServicoPeca> OrdemServicoPecas { get; set; }

        public DbSet<Tecnico> Tecnicos { get; set; }
        public DbSet<EquipamentoBackup> EquipamentosBackup { get; set; }

        // Nivel 3 Enterprise
        public DbSet<Contrato> Contratos { get; set; }
        public DbSet<Faturamento> Faturamentos { get; set; }
        public DbSet<VisitaCampo> VisitasCampo { get; set; }
        public DbSet<AuditoriaOS> AuditoriaOS { get; set; }

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
