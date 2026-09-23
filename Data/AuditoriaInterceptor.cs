using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using AssistenciaTech.Models;
using System.Security.Claims;

namespace AssistenciaTech.Data
{
    public class AuditoriaInterceptor : SaveChangesInterceptor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditoriaInterceptor(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            Audit(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Audit(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void Audit(DbContext? context)
        {
            if (context == null) return;

            List<EntityEntry<OrdemServico>>? entries = null;
            foreach (var e in context.ChangeTracker.Entries<OrdemServico>())
            {
                if (e.State == EntityState.Modified || e.State == EntityState.Added)
                {
                    entries ??= new List<EntityEntry<OrdemServico>>();
                    entries.Add(e);
                }
            }

            if (entries == null) return;

            // Pega o usuário logado (Admin, Tecnico)
            string usuario = "Sistema/Desconhecido";
            if (_httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true)
            {
                usuario = _httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.Name)?.Value ?? "Admin";
            }

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Modified)
                {
                    AuditModifiedEntry(context, entry, usuario);
                }
                else if (entry.State == EntityState.Added)
                {
                    AuditAddedEntry(context, entry, usuario);
                }
            }
        }

        private void AuditModifiedEntry(DbContext context, EntityEntry<OrdemServico> entry, string usuario)
        {
            var alteracoes = new Dictionary<string, AuditChange>();

            foreach (var prop in entry.Properties)
            {
                if (!prop.IsModified) continue;

                var nomeCampo = prop.Metadata.Name;

                // Ignorar campos de controle interno se houver (ex: DataEntrada se não for intencional, etc.)
                if (nomeCampo == "DataAtualizacao") continue;

                var oldValue = prop.OriginalValue?.ToString();
                var newValue = prop.CurrentValue?.ToString();

                // Evita logar se o valor for nulo nas duas pontas ou idêntico
                if (oldValue == newValue) continue;

                alteracoes.Add(nomeCampo, new AuditChange
                {
                    De = oldValue ?? "N/A",
                    Para = newValue ?? "N/A"
                });
            }

            if (alteracoes.Count > 0)
            {
                var jsonDiff = System.Text.Json.JsonSerializer.Serialize(alteracoes, AuditoriaJsonContext.Default.DictionaryStringAuditChange);

                var auditoria = new AuditoriaOS
                {
                    Usuario = usuario,
                    DataAlteracao = DateTime.UtcNow,
                    CampoAlterado = "MÚLTIPLOS", // Mantido para compatibilidade, ou poderia ser nulo
                    ValorAntigo = null,
                    ValorNovo = null,
                    DetalhesAlteracao = jsonDiff
                };
                auditoria.OrdemServico = entry.Entity;
                context.Add(auditoria);
            }
        }

        private void AuditAddedEntry(DbContext context, EntityEntry<OrdemServico> entry, string usuario)
        {
            var jsonDiff = System.Text.Json.JsonSerializer.Serialize(new AuditCreate
            {
                Acao = "Criação de Ordem de Serviço",
                StatusInicial = entry.Entity.Status
            }, AuditoriaJsonContext.Default.AuditCreate);

            var auditoria = new AuditoriaOS
            {
                Usuario = usuario,
                DataAlteracao = DateTime.UtcNow,
                CampoAlterado = "CRIACAO_OS",
                ValorAntigo = "",
                ValorNovo = "OS Criada",
                DetalhesAlteracao = jsonDiff
            };
            auditoria.OrdemServico = entry.Entity;
            context.Add(auditoria);
        }
    }

    public struct AuditChange
    {
        public string De { get; set; }
        public string Para { get; set; }
    }

    public struct AuditCreate
    {
        public string Acao { get; set; }
        public string StatusInicial { get; set; }
    }

    [System.Text.Json.Serialization.JsonSerializable(typeof(Dictionary<string, AuditChange>))]
    [System.Text.Json.Serialization.JsonSerializable(typeof(AuditCreate))]
    public partial class AuditoriaJsonContext : System.Text.Json.Serialization.JsonSerializerContext
    {
    }
}
