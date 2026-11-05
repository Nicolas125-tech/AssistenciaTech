using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AssistenciaTech.Data;
using AssistenciaTech.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AssistenciaTech.Services
{
    /// <summary>
    /// Health Check de prontidão operacional (PostgreSQL + Cache Resiliente).
    /// </summary>
    public class TechOsHealthCheck : IHealthCheck
    {
        private readonly AppDbContext _context;
        private readonly IResilientCacheService _cache;

        public TechOsHealthCheck(AppDbContext context, IResilientCacheService cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var data = new Dictionary<string, object>
            {
                ["timestampUtc"] = DateTime.UtcNow
            };

            try
            {
                bool dbConnected = await _context.Database.CanConnectAsync(cancellationToken);
                data["database"] = dbConnected ? "up" : "down";

                if (!dbConnected)
                {
                    return HealthCheckResult.Unhealthy("Não foi possível conectar ao banco de dados principal.", data: data);
                }

                try
                {
                    await _cache.GetAsync<string>("__healthcheck_probe__");
                    data["cache"] = "up";
                }
                catch (Exception cacheEx)
                {
                    data["cache"] = "degraded";
                    data["cacheError"] = cacheEx.Message;
                    return HealthCheckResult.Degraded("Banco de dados operacional, mas camada de cache degradada.", cacheEx, data);
                }

                return HealthCheckResult.Healthy("Todos os subsistemas do TechOS estão operacionais.", data);
            }
            catch (Exception ex)
            {
                data["database"] = "down";
                return HealthCheckResult.Unhealthy("Falha ao verificar conectividade com o banco de dados.", ex, data);
            }
        }
    }
}
