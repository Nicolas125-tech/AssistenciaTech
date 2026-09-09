using System.Collections.Generic;
using System.Threading.Tasks;
using AssistenciaTech.Models;
using AssistenciaTech.Data;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AssistenciaTech.Services.Workflow
{
    public class WorkflowProcessor : IWorkflowProcessor
    {
        private readonly IEnumerable<IWorkflowRule> _rules;
        private readonly AppDbContext _context;

        public WorkflowProcessor(IEnumerable<IWorkflowRule> rules, AppDbContext context)
        {
            _rules = rules;
            _context = context;
        }

        public async Task<bool> ProcessAllAsync(OrdemServico ordemExistente, string statusAnterior, OrdemServico ordemNova, ModelStateDictionary modelState)
        {
            foreach (var rule in _rules)
            {
                if (rule.CanProcess(ordemExistente, statusAnterior, ordemNova))
                {
                    var result = await rule.ProcessAsync(ordemExistente, statusAnterior, ordemNova, _context);
                    if (!result.Success)
                    {
                        modelState.AddModelError(string.Empty, result.ErrorMessage ?? "Erro ao processar regra de workflow.");
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
