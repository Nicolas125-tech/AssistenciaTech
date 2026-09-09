import os

os.makedirs('Services/Workflow', exist_ok=True)

iworkflowrule_content = """using System.Threading.Tasks;
using AssistenciaTech.Models;
using AssistenciaTech.Data;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AssistenciaTech.Services.Workflow
{
    public interface IWorkflowRule
    {
        bool CanProcess(OrdemServico ordemExistente, string statusAnterior, OrdemServico ordemNova);
        Task<(bool Success, string? ErrorMessage)> ProcessAsync(OrdemServico ordemExistente, string statusAnterior, OrdemServico ordemNova, AppDbContext context);
    }
}
"""

iworkflowprocessor_content = """using System.Threading.Tasks;
using AssistenciaTech.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AssistenciaTech.Services.Workflow
{
    public interface IWorkflowProcessor
    {
        Task<bool> ProcessAllAsync(OrdemServico ordemExistente, string statusAnterior, OrdemServico ordemNova, ModelStateDictionary modelState);
    }
}
"""

with open('Services/Workflow/IWorkflowRule.cs', 'w') as f:
    f.write(iworkflowrule_content)

with open('Services/Workflow/IWorkflowProcessor.cs', 'w') as f:
    f.write(iworkflowprocessor_content)
