using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AssistenciaTech.Application.Tests.Controllers
{
    public class PerformanceBenchmark
    {
        [Fact]
        public async Task MeasureDeletePerformance()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite("DataSource=:memory:;Foreign Keys=False")
                .Options;

            using var context = new AppDbContext(options);
            await context.Database.OpenConnectionAsync();
            await context.Database.EnsureCreatedAsync();

            for (int i = 0; i < 1000; i++)
            {
                context.Pecas.Add(new Peca { Nome = $"Peca {i}", QuantidadeEstoque = 1, ValorUnitario = 10 });
            }
            await context.SaveChangesAsync();

            var sw = Stopwatch.StartNew();

            for(int i = 1; i <= 500; i++)
            {
                var peca = await context.Pecas.FindAsync(i);
                if (peca != null)
                {
                    context.Pecas.Remove(peca);
                    await context.SaveChangesAsync();
                }
            }

            sw.Stop();
            var baseline = sw.ElapsedMilliseconds;

            sw.Restart();
            for(int i = 501; i <= 1000; i++)
            {
                await context.Pecas.Where(p => p.Id == i).ExecuteDeleteAsync();
            }
            sw.Stop();
            var optimized = sw.ElapsedMilliseconds;

            Console.WriteLine($"Baseline Delete: {baseline} ms");
            Console.WriteLine($"ExecuteDeleteAsync: {optimized} ms");
        }
    }
}
