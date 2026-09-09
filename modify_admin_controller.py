import re

with open('Controllers/AdminController.cs', 'r') as f:
    content = f.read()

# 1. Add namespace
content = content.replace("using AssistenciaTech.Services;", "using AssistenciaTech.Services;\nusing AssistenciaTech.Services.Workflow;")

# 2. Add processor variable
content = content.replace("private readonly INotificationService _notificationService;", "private readonly INotificationService _notificationService;\n        private readonly IWorkflowProcessor _workflowProcessor;")

# 3. Modify constructor
old_constructor = "public AdminController(AppDbContext context, IEstoqueService estoqueService, IWebHostEnvironment env, IPdfGeneratorService pdfGeneratorService, IAdminDashboardService dashboardService, IEquipamentoBackupService equipamentoBackupService, ILogger<AdminController> logger, IServiceScopeFactory scopeFactory, INotificationService notificationService)"
new_constructor = "public AdminController(AppDbContext context, IEstoqueService estoqueService, IWebHostEnvironment env, IPdfGeneratorService pdfGeneratorService, IAdminDashboardService dashboardService, IEquipamentoBackupService equipamentoBackupService, ILogger<AdminController> logger, IServiceScopeFactory scopeFactory, INotificationService notificationService, IWorkflowProcessor workflowProcessor)"

content = content.replace(old_constructor, new_constructor)
content = content.replace("_notificationService = notificationService;", "_notificationService = notificationService;\n            _workflowProcessor = workflowProcessor;")

# 4. Modify ProcessWorkflowRulesAsync
old_method = """        private async Task<bool> ProcessWorkflowRulesAsync(OrdemServico ordemExistente, string statusAnterior, OrdemServico ordemServico)
        {
            if (ordemExistente.Status == WorkflowStatus.Concluido && ordemExistente.DataConclusao == null)
            {
                ordemExistente.DataConclusao = DateTime.UtcNow;
                await _estoqueService.DeduzirEstoque(ordemExistente.Id);
            }
            else if (statusAnterior == WorkflowStatus.Concluido && ordemExistente.Status != WorkflowStatus.Concluido && ordemExistente.Status != WorkflowStatus.Entregue)
            {
                ordemExistente.DataConclusao = null;
                await _estoqueService.RestaurarEstoque(ordemExistente.Id);
            }

            if (ordemExistente.Status == WorkflowStatus.Entregue && ordemExistente.DataEntregaCliente == null)
            {
                if (ordemExistente.EquipamentoBackupId.HasValue)
                {
                    var backup = await _context.EquipamentosBackup.FindAsync(ordemExistente.EquipamentoBackupId);
                    if (backup != null && backup.Disponivel == false)
                    {
                        ModelState.AddModelError(string.Empty, $"O status não pode ser 'Entregue' até que o equipamento '{backup.Descricao}' seja devolvido no sistema.");
                        return false;
                    }
                }

                ordemExistente.DataEntregaCliente = DateTime.UtcNow;

                if (ordemExistente.DataConclusao == null)
                {
                    ordemExistente.DataConclusao = DateTime.UtcNow;
                    await _estoqueService.DeduzirEstoque(ordemExistente.Id);
                }
            }
            else if (ordemExistente.Status != WorkflowStatus.Entregue)
            {
                ordemExistente.DataEntregaCliente = null;
            }
            return true;
        }"""

new_method = """        private async Task<bool> ProcessWorkflowRulesAsync(OrdemServico ordemExistente, string statusAnterior, OrdemServico ordemServico)
        {
            return await _workflowProcessor.ProcessAllAsync(ordemExistente, statusAnterior, ordemServico, ModelState);
        }"""

content = content.replace(old_method, new_method)

with open('Controllers/AdminController.cs', 'w') as f:
    f.write(content)
