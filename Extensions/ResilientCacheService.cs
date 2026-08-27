using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace AssistenciaTech.Extensions
{
    /// <summary>
    /// Wrapper resiliente sobre IDistributedCache com circuit breaker embutido.
    /// Se o Redis falhar, desativa o cache por 60 segundos para evitar timeouts repetidos.
    /// </summary>
    public interface IResilientCacheService
    {
        Task<T?> GetAsync<T>(string key);
        Task SetAsync<T>(string key, T data, TimeSpan? absoluteExpireTime = null);
        Task RemoveAsync(string key);
        bool IsAvailable { get; }
    }

    public class ResilientCacheService : IResilientCacheService
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger<ResilientCacheService> _logger;

        // Circuit breaker: se falhar, desativa por 60 segundos
        private static DateTime _circuitOpenUntil = DateTime.MinValue;
        private static readonly TimeSpan CircuitBreakDuration = TimeSpan.FromSeconds(60);

        public bool IsAvailable => DateTime.UtcNow >= _circuitOpenUntil;

        public ResilientCacheService(IDistributedCache cache, ILogger<ResilientCacheService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            if (!IsAvailable) return default;

            try
            {
                var jsonData = await _cache.GetStringAsync(key);
                if (jsonData is null) return default;
                return JsonSerializer.Deserialize<T>(jsonData);
            }
            catch (Exception ex)
            {
                OpenCircuit(ex);
                return default;
            }
        }

        public async Task SetAsync<T>(string key, T data, TimeSpan? absoluteExpireTime = null)
        {
            if (!IsAvailable) return;

            try
            {
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = absoluteExpireTime ?? TimeSpan.FromMinutes(60)
                };
                var jsonData = JsonSerializer.Serialize(data);
                await _cache.SetStringAsync(key, jsonData, options);
            }
            catch (Exception ex)
            {
                OpenCircuit(ex);
            }
        }

        public async Task RemoveAsync(string key)
        {
            if (!IsAvailable) return;

            try
            {
                await _cache.RemoveAsync(key);
            }
            catch (Exception ex)
            {
                OpenCircuit(ex);
            }
        }

        private void OpenCircuit(Exception ex)
        {
            _circuitOpenUntil = DateTime.UtcNow.Add(CircuitBreakDuration);
            _logger.LogWarning(ex, "[Cache] Redis indisponível. Circuit breaker ativado por {Seconds}s. Usando banco direto.", CircuitBreakDuration.TotalSeconds);
        }
    }
}
