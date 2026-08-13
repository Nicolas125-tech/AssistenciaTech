using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System;

public class Peca
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int QuantidadeEstoque { get; set; }
}

public class OrdemServicoPeca
{
    public int Id { get; set; }
    public int OrdemServicoId { get; set; }
    public int PecaId { get; set; }
    public Peca Peca { get; set; }
    public int Quantidade { get; set; }
}

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Peca> Pecas { get; set; }
    public DbSet<OrdemServicoPeca> OrdemServicoPecas { get; set; }
}

[MemoryDiagnoser]
public class EstoqueBenchmark
{
    private AppDbContext _context;

    [GlobalSetup]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=benchmark.db")
            .Options;

        _context = new AppDbContext(options);
        _context.Database.EnsureDeleted();
        _context.Database.EnsureCreated();

        var pecas = new List<Peca>();
        for (int i = 1; i <= 1000; i++)
        {
            pecas.Add(new Peca { Id = i, Nome = $"Peca {i}", QuantidadeEstoque = 1000 });
        }
        _context.Pecas.AddRange(pecas);

        for (int i = 1; i <= 1000; i++)
        {
            _context.OrdemServicoPecas.Add(new OrdemServicoPeca { OrdemServicoId = 1, PecaId = i, Quantidade = 1 });
        }

        _context.SaveChanges();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _context.Database.ExecuteSqlRaw("UPDATE Pecas SET QuantidadeEstoque = 1000");
        _context.ChangeTracker.Clear();
    }

    [Benchmark(Baseline = true)]
    public async Task EFCoreTracking_Original()
    {
        var pecasUtilizadas = await _context.OrdemServicoPecas
            .Include(op => op.Peca)
            .Where(op => op.OrdemServicoId == 1)
            .ToListAsync();

        if (!pecasUtilizadas.Any()) return;

        foreach (var item in pecasUtilizadas)
        {
            if (item.Peca != null)
            {
                if (item.Peca.QuantidadeEstoque < item.Quantidade)
                    throw new InvalidOperationException("Estoque insuficiente");

                item.Peca.QuantidadeEstoque -= item.Quantidade;
            }
        }

        await _context.SaveChangesAsync();
    }

    [Benchmark]
    public async Task ExecuteUpdate_Optimized_Batched()
    {
        var pecasInfo = await _context.OrdemServicoPecas
            .Where(op => op.OrdemServicoId == 1)
            .Select(op => new { op.PecaId, op.Quantidade, PecaEstoque = op.Peca.QuantidadeEstoque, op.Peca.Nome })
            .ToListAsync();

        if (!pecasInfo.Any()) return;

        foreach (var item in pecasInfo)
        {
            if (item.PecaEstoque < item.Quantidade)
                throw new InvalidOperationException($"Estoque insuficiente");
        }

        // Instead of looping, maybe a bulk update using SQL?
        // SQLite:
        /*
        UPDATE Pecas
        SET QuantidadeEstoque = QuantidadeEstoque - (SELECT Quantidade FROM OrdemServicoPecas WHERE PecaId = Pecas.Id AND OrdemServicoId = 1)
        WHERE Id IN (SELECT PecaId FROM OrdemServicoPecas WHERE OrdemServicoId = 1);
        */

        // Let's do raw SQL
        await _context.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE Pecas
            SET QuantidadeEstoque = QuantidadeEstoque - (
                SELECT op.Quantidade
                FROM OrdemServicoPecas op
                WHERE op.PecaId = Pecas.Id AND op.OrdemServicoId = {1}
            )
            WHERE Id IN (
                SELECT op2.PecaId
                FROM OrdemServicoPecas op2
                WHERE op2.OrdemServicoId = {1}
            );
        ");
    }
}

public class ProgramBenchmark
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<EstoqueBenchmark>();
    }
}
