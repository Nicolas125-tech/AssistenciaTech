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
        private readonly List<(AuditoriaOS Audit, OrdemServico OS)> _pendingAudits = new();

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


        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            var baseResult = base.SavedChanges(eventData, result);
            ProcessPendingAudits(eventData.Context);
            return baseResult;
        }

        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            var baseResult = await base.SavedChangesAsync(eventData, result, cancellationToken);
            await ProcessPendingAuditsAsync(eventData.Context, cancellationToken);
            return baseResult;
        }

        public override void SaveChangesFailed(DbContextErrorEventData eventData)
        {
            _pendingAudits.Clear();
            base.SaveChangesFailed(eventData);
        }

        public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            _pendingAudits.Clear();
            return base.SaveChangesFailedAsync(eventData, cancellationToken);
        }

        private List<AuditoriaOS>? PreparePendingAudits(DbContext? context)
        {
            if (context == null || !_pendingAudits.Any()) return null;

            var audits = new List<AuditoriaOS>(_pendingAudits.Count);
            foreach (var pending in _pendingAudits)
            {
                pending.Audit.OrdemServicoId = pending.OS.Id;
                audits.Add(pending.Audit);
            }
            context.AddRange(audits);

            _pendingAudits.Clear();
            return audits;
        }

        private void ProcessPendingAudits(DbContext? context)
        {
            if (context == null || PreparePendingAudits(context) == null) return;
            context.SaveChanges();
        }

        private async Task ProcessPendingAuditsAsync(DbContext? context, CancellationToken cancellationToken)
        {
            if (context == null || PreparePendingAudits(context) == null) return;
            await context.SaveChangesAsync(cancellationToken);
        }

        private void Audit(DbContext? context)
        {
            if (context == null) return;

            var entries = context.ChangeTracker.Entries<OrdemServico>()
                .Where(e => e.State == EntityState.Modified || e.State == EntityState.Added)
                .ToList();

            if (!entries.Any()) return;

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
                    AuditAddedEntry(entry, usuario);
                }
            }
        }

        private void AuditModifiedEntry(DbContext context, EntityEntry<OrdemServico> entry, string usuario)
        {
            foreach (var prop in entry.Properties.Where(p => p.IsModified))
            {
                var oldValue = prop.OriginalValue?.ToString();
                var newValue = prop.CurrentValue?.ToString();

                // Evita logar se o valor for nulo nas duas pontas ou idêntico
                if (oldValue == newValue) continue;

                var auditoria = new AuditoriaOS
                {
                    OrdemServicoId = entry.Entity.Id,
                    Usuario = usuario,
                    DataAlteracao = DateTime.UtcNow,
                    CampoAlterado = prop.Metadata.Name,
                    ValorAntigo = oldValue,
                    ValorNovo = newValue
                };
                context.Add(auditoria);
            }
        }

        private void AuditAddedEntry(EntityEntry<OrdemServico> entry, string usuario)
        {
            var auditoria = new AuditoriaOS
            {
                Usuario = usuario,
                DataAlteracao = DateTime.UtcNow,
                CampoAlterado = "CRIACAO_OS",
                ValorAntigo = "",
                ValorNovo = "OS Criada"
            };
            _pendingAudits.Add((auditoria, entry.Entity));
        }
    }
}
