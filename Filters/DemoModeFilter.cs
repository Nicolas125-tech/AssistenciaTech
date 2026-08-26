using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace AssistenciaTech.Filters
{
    public class DemoModeFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var user = context.HttpContext.User;

            // Verifica se está autenticado e se é o usuário demo
            if (user.Identity != null && user.Identity.IsAuthenticated && user.Identity.Name == "demo@assistenciatech.com")
            {
                var method = context.HttpContext.Request.Method.ToUpper();

                // Intercepta qualquer método que modifique dados
                if (method == "POST" || method == "PUT" || method == "PATCH" || method == "DELETE")
                {
                    // Ação bloqueada
                    var controller = context.Controller as Controller;
                    if (controller != null)
                    {
                        controller.TempData["Error"] = "Ação não permitida: Você está logado em uma conta de Demonstração. Nenhuma alteração foi salva no banco de dados.";
                        
                        // Retorna para a mesma página (se for POST de um formulário)
                        // Para não dar erro 500 ou quebrar a experiência, faremos um redirecionamento simples
                        // Pegamos o Referer para saber de onde a pessoa veio
                        string referer = context.HttpContext.Request.Headers["Referer"].ToString();
                        
                        if (!string.IsNullOrEmpty(referer))
                        {
                            context.Result = new RedirectResult(referer);
                        }
                        else
                        {
                            // Se não houver referer, manda pro dashboard
                            context.Result = new RedirectToActionResult("Index", "Admin", null);
                        }
                    }
                    else
                    {
                        // Caso seja um ControllerBase (API REST)
                        context.Result = new JsonResult(new { success = false, message = "Ação bloqueada no modo Demonstração." })
                        {
                            StatusCode = 403
                        };
                    }
                }
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            // Não precisamos fazer nada após a execução
        }
    }
}
