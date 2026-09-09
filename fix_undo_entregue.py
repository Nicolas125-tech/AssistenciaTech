import os

with open('Services/Workflow/Rules.cs', 'r') as f:
    content = f.read()

# Fix the condition to match exactly the original code:
# else if (ordemExistente.Status != WorkflowStatus.Entregue) { ordemExistente.DataEntregaCliente = null; }
content = content.replace("return ordemExistente.Status != WorkflowStatus.Entregue && ordemExistente.DataEntregaCliente != null;", "return ordemExistente.Status != WorkflowStatus.Entregue;")

with open('Services/Workflow/Rules.cs', 'w') as f:
    f.write(content)
