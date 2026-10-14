with open('Views/Admin/Index.cshtml', 'r') as f:
    content = f.read()

# Replace the @model directive
content = content.replace('@model IEnumerable<AssistenciaTech.Models.OrdemServico>', '@model AssistenciaTech.ViewModels.AdminDashboardViewModel')

# Replace ViewBag.ErroBanco
content = content.replace('ViewBag.ErroBanco', 'Model.ErroBanco')

# Replace ViewBag.SearchString
content = content.replace('ViewBag.SearchString', 'Model.SearchString')

# Replace ViewBag.StatusFilter
content = content.replace('ViewBag.StatusFilter', 'Model.StatusFilter')

# Replace ViewBag.ChartLabels
content = content.replace('ViewBag.ChartLabels', 'Model.DashboardData.ChartLabels')

# Replace ViewBag.ChartData
content = content.replace('ViewBag.ChartData', 'Model.DashboardData.ChartData')

# Replace ViewBag.TotalAbertas
content = content.replace('ViewBag.TotalAbertas', 'Model.DashboardData.TotalAbertas')

# Replace ViewBag.EquipamentosProntos
content = content.replace('ViewBag.EquipamentosProntos', 'Model.DashboardData.EquipamentosProntos')

# Replace ViewBag.FaturamentoPrevisto
content = content.replace('ViewBag.FaturamentoPrevisto', 'Model.DashboardData.FaturamentoPrevisto')

# Replace ViewBag.AlertasEstoque
content = content.replace('ViewBag.AlertasEstoque', 'Model.DashboardData.AlertasEstoque')

# Replace Model.Any() and foreach (var item in Model) with Model.DashboardData.Ordens
content = content.replace('@if (!Model.Any())', '@if (!Model.DashboardData.Ordens.Any())')
content = content.replace('@foreach (var item in Model)', '@foreach (var item in Model.DashboardData.Ordens)')

with open('Views/Admin/Index.cshtml', 'w') as f:
    f.write(content)
