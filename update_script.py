import sys

def update_admin_facade():
    with open("Services/IAdminFacade.cs", "r") as f:
        content = f.read()

    if "IResilientCacheService Cache" not in content:
        content = content.replace("IClienteService Cliente { get; }", "IClienteService Cliente { get; }\n        AssistenciaTech.Extensions.IResilientCacheService Cache { get; }")
        with open("Services/IAdminFacade.cs", "w") as f:
            f.write(content)

    with open("Services/AdminFacade.cs", "r") as f:
        content = f.read()

    if "IResilientCacheService Cache" not in content:
        content = content.replace("IClienteService Cliente { get; }", "IClienteService Cliente { get; }\n        public AssistenciaTech.Extensions.IResilientCacheService Cache { get; }")
        content = content.replace("IClienteService clienteService)", "IClienteService clienteService,\n            AssistenciaTech.Extensions.IResilientCacheService cache)")
        content = content.replace("Cliente = clienteService;", "Cliente = clienteService;\n            Cache = cache;")
        with open("Services/AdminFacade.cs", "w") as f:
            f.write(content)

update_admin_facade()
