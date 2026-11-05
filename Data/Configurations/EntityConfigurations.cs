using AssistenciaTech.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssistenciaTech.Data.Configurations
{
    public class OrdemServicoConfiguration : IEntityTypeConfiguration<OrdemServico>
    {
        public void Configure(EntityTypeBuilder<OrdemServico> builder)
        {
            builder.HasKey(o => o.Id);

            builder.Property(o => o.Equipamento).IsRequired();
            builder.Property(o => o.ProblemaRelatado).IsRequired();
            builder.Property(o => o.Status).IsRequired().HasDefaultValue(WorkflowStatus.Recebido);

            builder.Property(o => o.CustoPecas).HasColumnType("decimal(18,2)");
            builder.Property(o => o.CustoMaoDeObra).HasColumnType("decimal(18,2)");
            builder.Property(o => o.DescontoAplicado).HasColumnType("decimal(18,2)");
            builder.Property(o => o.ValorOrcamento).HasColumnType("decimal(18,2)");
            builder.Property(o => o.CustoTerceirizado).HasColumnType("decimal(18,2)");

            builder.HasIndex(o => o.Status);
            builder.HasIndex(o => o.NumeroSerie);
            builder.HasIndex(o => o.DataEntrada);

            builder.HasOne(o => o.Cliente)
                .WithMany(c => c.OrdensServico)
                .HasForeignKey(o => o.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(o => o.TecnicoResponsavel)
                .WithMany()
                .HasForeignKey(o => o.TecnicoId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(o => o.EquipamentoBackup)
                .WithMany()
                .HasForeignKey(o => o.EquipamentoBackupId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(o => o.Contrato)
                .WithMany()
                .HasForeignKey(o => o.ContratoId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }

    public class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
    {
        public void Configure(EntityTypeBuilder<Cliente> builder)
        {
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Nome).IsRequired().HasMaxLength(100);
            builder.Property(c => c.Cpf).IsRequired().HasMaxLength(14);
            builder.Property(c => c.Telefone).IsRequired();

            builder.HasIndex(c => c.Cpf);
        }
    }

    public class PecaConfiguration : IEntityTypeConfiguration<Peca>
    {
        public void Configure(EntityTypeBuilder<Peca> builder)
        {
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Nome).IsRequired();
            builder.Property(p => p.ValorUnitario).HasColumnType("decimal(18,2)");
        }
    }

    public class OrdemServicoPecaConfiguration : IEntityTypeConfiguration<OrdemServicoPeca>
    {
        public void Configure(EntityTypeBuilder<OrdemServicoPeca> builder)
        {
            builder.HasKey(op => op.Id);
            builder.Property(op => op.ValorVenda).HasColumnType("decimal(18,2)");

            builder.HasOne(op => op.OrdemServico)
                .WithMany(o => o.PecasUtilizadas)
                .HasForeignKey(op => op.OrdemServicoId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(op => op.Peca)
                .WithMany()
                .HasForeignKey(op => op.PecaId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class TecnicoConfiguration : IEntityTypeConfiguration<Tecnico>
    {
        public void Configure(EntityTypeBuilder<Tecnico> builder)
        {
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Nome).IsRequired();
            builder.Property(t => t.PercentualComissao).HasColumnType("decimal(18,2)");
        }
    }

    public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
    {
        public void Configure(EntityTypeBuilder<Usuario> builder)
        {
            builder.HasKey(u => u.Id);
            builder.Property(u => u.Username).IsRequired().HasMaxLength(100);
            builder.Property(u => u.PasswordHash).IsRequired();
            builder.Property(u => u.Role).IsRequired().HasMaxLength(50);

            builder.HasIndex(u => u.Username).IsUnique();
        }
    }

    public class ContratoConfiguration : IEntityTypeConfiguration<Contrato>
    {
        public void Configure(EntityTypeBuilder<Contrato> builder)
        {
            builder.HasKey(c => c.Id);
            builder.Property(c => c.ValorMensal).HasColumnType("decimal(18,2)");

            builder.HasOne(c => c.Cliente)
                .WithMany()
                .HasForeignKey(c => c.ClienteId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class FaturamentoConfiguration : IEntityTypeConfiguration<Faturamento>
    {
        public void Configure(EntityTypeBuilder<Faturamento> builder)
        {
            builder.HasKey(f => f.Id);
            builder.Property(f => f.ValorTotal).HasColumnType("decimal(18,2)");
            builder.Property(f => f.BaseCalculoISS).HasColumnType("decimal(18,2)");
            builder.Property(f => f.AliquotaISS).HasColumnType("decimal(18,2)");
            builder.Property(f => f.ValorISS).HasColumnType("decimal(18,2)");
            builder.Property(f => f.BaseCalculoICMS).HasColumnType("decimal(18,2)");
            builder.Property(f => f.AliquotaICMS).HasColumnType("decimal(18,2)");
            builder.Property(f => f.ValorICMS).HasColumnType("decimal(18,2)");

            builder.HasOne(f => f.OrdemServico)
                .WithMany()
                .HasForeignKey(f => f.OrdemServicoId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class VisitaCampoConfiguration : IEntityTypeConfiguration<VisitaCampo>
    {
        public void Configure(EntityTypeBuilder<VisitaCampo> builder)
        {
            builder.HasKey(v => v.Id);
            builder.Property(v => v.Latitude).HasColumnType("decimal(18,6)");
            builder.Property(v => v.Longitude).HasColumnType("decimal(18,6)");

            builder.HasOne(v => v.OrdemServico)
                .WithMany()
                .HasForeignKey(v => v.OrdemServicoId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(v => v.Tecnico)
                .WithMany()
                .HasForeignKey(v => v.TecnicoId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class AuditoriaOSConfiguration : IEntityTypeConfiguration<AuditoriaOS>
    {
        public void Configure(EntityTypeBuilder<AuditoriaOS> builder)
        {
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Usuario).IsRequired();
            builder.HasIndex(a => a.OrdemServicoId);

            builder.HasOne(a => a.OrdemServico)
                .WithMany()
                .HasForeignKey(a => a.OrdemServicoId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
