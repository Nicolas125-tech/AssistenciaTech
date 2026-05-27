using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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
                    foreach (var prop in entry.Properties)
                    {
                        if (prop.IsModified)
                        {
                            var oldValue = prop.OriginalValue?.ToString();
                            var newValue = prop.CurrentValue?.ToString();

                            // Evita logar se o valor for nulo nas duas pontas ou idêntico
                            if (oldValue == newValue) continue;

                            var auditoria = new AuditoriaOS
                            {
                                OrdemServicoId = entry.Entity.Id,
                                Usuario = usuario,
                                DataAlteracao = DateTime.Now,
                                CampoAlterado = prop.Metadata.Name,
                                ValorAntigo = oldValue,
                                ValorNovo = newValue
                            };

                            context.Add(auditoria);
                        }
                    }
                }
                else if (entry.State == EntityState.Added)
                {
                    var auditoria = new AuditoriaOS
                    {
                        OrdemServicoId = entry.Entity.Id,
                        Usuario = usuario,
                        DataAlteracao = DateTime.Now,
                        CampoAlterado = "CRIACAO_OS",
                        ValorAntigo = "",
                        ValorNovo = "OS Criada"
                    };
                    // Não podemos adicionar ainda pq o ID da OS pode ser 0 (se for Identity db-gerado),
                    // Mas para não complicar com SaveChanges duplo, aceitaremos 0 ou criaremos a lógica no Controller se necessário.
                    // Para o MVP: assumimos que o mais importante é auditar a MODIFICAÇÃO de status e valores.
                }
            }
        }
    }
}
