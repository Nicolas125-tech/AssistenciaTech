import re

with open('Program.cs', 'r') as f:
    content = f.read()

services_registration = """
builder.Services.AddScoped<AssistenciaTech.Services.Workflow.IWorkflowRule, AssistenciaTech.Services.Workflow.ConcluidoRule>();
builder.Services.AddScoped<AssistenciaTech.Services.Workflow.IWorkflowRule, AssistenciaTech.Services.Workflow.UndoConcluidoRule>();
builder.Services.AddScoped<AssistenciaTech.Services.Workflow.IWorkflowRule, AssistenciaTech.Services.Workflow.EntregueRule>();
builder.Services.AddScoped<AssistenciaTech.Services.Workflow.IWorkflowRule, AssistenciaTech.Services.Workflow.UndoEntregueRule>();
builder.Services.AddScoped<AssistenciaTech.Services.Workflow.IWorkflowProcessor, AssistenciaTech.Services.Workflow.WorkflowProcessor>();
"""

content = content.replace("builder.Services.AddScoped<IEstoqueService, EstoqueService>();",
                          "builder.Services.AddScoped<IEstoqueService, EstoqueService>();" + services_registration)

with open('Program.cs', 'w') as f:
    f.write(content)
