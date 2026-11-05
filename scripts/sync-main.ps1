[CmdletBinding()]
param(
    [string]$CommitMessage = "",
    [switch]$SkipTests,
    [string]$OneDrivePath = "C:\Users\nicolas.bissoqui\OneDrive - Agili Software Brasil Ltda\AssistenciaTech"
)

$ErrorActionPreference = "Stop"

Write-Host "==> [TechOS] Iniciando sincronização segura..." -ForegroundColor Cyan

# 1. Executa testes unitários rápidos se não ignorados
if (-not $SkipTests) {
    Write-Host "==> Executando testes unitários rápidos da aplicação..." -ForegroundColor Yellow
    dotnet test tests/AssistenciaTech.Application.Tests/AssistenciaTech.Application.Tests.csproj --nologo
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Testes falharam. Abortando sincronização para proteger a estabilidade."
        exit $LASTEXITCODE
    }
}

# 2. Se houver mensagem de commit e modificações, faz commit
$status = git status --porcelain
if ($status -and $CommitMessage) {
    Write-Host "==> Registrando alterações no Git..." -ForegroundColor Yellow
    git add -A
    git commit -m $CommitMessage
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Falha ao criar commit."
        exit $LASTEXITCODE
    }
}

# 3. Push para origin/main
Write-Host "==> Enviando alterações para origin/main..." -ForegroundColor Yellow
git push origin HEAD:main
if ($LASTEXITCODE -ne 0) {
    Write-Error "Falha ao enviar commit para origin/main."
    exit $LASTEXITCODE
}

# 4. Sincroniza cópia local do OneDrive se o caminho existir
if (Test-Path $OneDrivePath) {
    Write-Host "==> Sincronizando repositório primário no OneDrive ($OneDrivePath)..." -ForegroundColor Yellow
    git -C $OneDrivePath pull origin main --ff-only
    if ($LASTEXITCODE -ne 0) {
        Write-Host "==> Tentando restaurar arquivos com alteração de quebra de linha (CRLF)..." -ForegroundColor Yellow
        git -C $OneDrivePath restore .
        git -C $OneDrivePath pull origin main --ff-only
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Não foi possível atualizar a pasta OneDrive via fast-forward. Verifique se há alterações locais lá."
        } else {
            Write-Host "==> Repositório do OneDrive sincronizado com sucesso após restaurar quebras de linha." -ForegroundColor Green
        }
    } else {
        Write-Host "==> Repositório do OneDrive sincronizado com sucesso." -ForegroundColor Green
    }
}

Write-Host "==> [TechOS] Sincronização concluída com sucesso." -ForegroundColor Green
