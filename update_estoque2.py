import sys

def modify_file(filepath):
    with open(filepath, 'r') as f:
        lines = f.readlines()

    for i in range(len(lines)):
        if "var pecaIds = agrupado.Select(a => a.PecaId).ToList();" in lines[i]:
            if "var pecas = await _context.Pecas.Where(p => pecaIds.Contains(p.Id)).ToListAsync();" in lines[i+1]:
                lines[i] = "            // Get distinct parts directly via join to avoid large IN clauses\n"
                lines[i+1] = "            var pecas = await _context.OrdemServicoPecas.Where(op => op.OrdemServicoId == ordemServicoId).Select(op => op.Peca!).Distinct().ToListAsync();\n"

    with open(filepath, 'w') as f:
        f.writelines(lines)

modify_file('Services/EstoqueService.cs')
