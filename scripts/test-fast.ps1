[CmdletBinding()]
param(
    [switch]$All
)

$ErrorActionPreference = "Stop"

if ($All) {
    Write-Host "==> Executando suíte completa de testes (.NET 8)..." -ForegroundColor Cyan
    dotnet test AssistenciaTech.sln --nologo
} else {
    Write-Host "==> Executando testes unitários rápidos da aplicação..." -ForegroundColor Cyan
    dotnet test tests/AssistenciaTech.Application.Tests/AssistenciaTech.Application.Tests.csproj --nologo
}
