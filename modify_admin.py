import sys

def modify_admin_controller():
    with open("Controllers/AdminController.cs", "r") as f:
        lines = f.readlines()

    cache_logic = """
                var cacheKeyEq = "EquipamentosBackup_All";
                var allEquipamentos = await _facade.Cache.GetAsync<System.Collections.Generic.List<EquipamentoBackup>>(cacheKeyEq);
                if (allEquipamentos == null)
                {
                    allEquipamentos = await _context.EquipamentosBackup.AsNoTracking().ToListAsync();
                    await _facade.Cache.SetAsync(cacheKeyEq, allEquipamentos, TimeSpan.FromMinutes(60));
                }
"""

    # We need to replace line 211
    # 211: ViewBag.EquipamentosBackup = new SelectList(await _context.EquipamentosBackup.AsNoTracking().Where(e => e.Disponivel || e.Id == ordemServico.EquipamentoBackupId).Select(e => new { e.Id, e.Descricao }).ToListAsync(), "Id", "Descricao");
    line211_index = 210
    lines[line211_index] = cache_logic + """                ViewBag.EquipamentosBackup = new SelectList(allEquipamentos.Where(e => e.Disponivel || e.Id == ordemServico.EquipamentoBackupId).Select(e => new { e.Id, e.Descricao }).ToList(), "Id", "Descricao");\n"""

    # Let's write to file to check it out
    with open("Controllers/AdminController.cs", "w") as f:
        f.writelines(lines)

    # Reload lines because line count changed
    with open("Controllers/AdminController.cs", "r") as f:
        lines = f.readlines()

    # Now find the line matching "var equipamentos = await _context.EquipamentosBackup" in PopulateViewBagsForEditAsync
    for i, line in enumerate(lines):
        if "var equipamentos = await _context.EquipamentosBackup.AsNoTracking().Where(e => e.Disponivel || e.Id == ordemServico.EquipamentoBackupId).ToListAsync();" in line:
            indent = line[:len(line) - len(line.lstrip())]
            replacement = """
{indent}var cacheKeyEq = "EquipamentosBackup_All";
{indent}var allEquipamentos = await _facade.Cache.GetAsync<System.Collections.Generic.List<EquipamentoBackup>>(cacheKeyEq);
{indent}if (allEquipamentos == null)
{indent}{{
{indent}    allEquipamentos = await _context.EquipamentosBackup.AsNoTracking().ToListAsync();
{indent}    await _facade.Cache.SetAsync(cacheKeyEq, allEquipamentos, TimeSpan.FromMinutes(60));
{indent}}}
{indent}var equipamentos = allEquipamentos.Where(e => e.Disponivel || e.Id == ordemServico.EquipamentoBackupId).ToList();
""".format(indent=indent)
            lines[i] = replacement[1:] # strip leading newline
            break

    with open("Controllers/AdminController.cs", "w") as f:
        f.writelines(lines)

modify_admin_controller()
