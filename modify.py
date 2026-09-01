import re

with open("Controllers/FaturamentosController.cs", "r") as f:
    content = f.read()

target = """                if (faturamentosToUpdate.Count > 0)
                {
                    foreach (var faturamento in faturamentosToUpdate)
                    {
                        faturamento.StatusPagamento = PagamentoStatus.Pago_Total;
                    }

                    _context.UpdateRange(faturamentosToUpdate);
                    await _context.SaveChangesAsync();
                }"""

replacement = """                if (faturamentosToUpdate.Count > 0)
                {
                    for (int i = 0; i < faturamentosToUpdate.Count; i++)
                    {
                        faturamentosToUpdate[i].StatusPagamento = PagamentoStatus.Pago_Total;
                    }

                    // Entities are already tracked, no need for _context.UpdateRange which forces all fields to modified
                    await _context.SaveChangesAsync();
                }"""

if target in content:
    content = content.replace(target, replacement)
    with open("Controllers/FaturamentosController.cs", "w") as f:
        f.write(content)
    print("Success")
else:
    print("Failed to find target text")
