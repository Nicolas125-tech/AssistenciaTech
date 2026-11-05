# Observabilidade, Health Checks e Deploy (TechOS)

Este documento descreve como diagnosticar o status da aplicação em tempo de execução e verificar a integridade dos deploys no Render, Docker ou ambiente local.

---

## 1. Endpoints de Verificação

A aplicação disponibiliza três rotas de monitoramento:

- **`/health` (JSON detalhado)**:
  - Rota principal para agentes e operadores verificarem a saúde e a versão da aplicação.
  - Formato de resposta:
    ```json
    {
      "status": "Healthy",
      "version": "1.0.0.0",
      "commit": "908fef7",
      "fullCommit": "908fef794c0fdf6840f4d3bccbc9767a7474cba8",
      "timestampUtc": "2026-11-05T20:20:00Z",
      "durationMs": 4.2,
      "checks": [
        {
          "name": "techos_readiness",
          "status": "Healthy",
          "description": "Todos os subsistemas do TechOS estão operacionais.",
          "data": {
            "database": "up",
            "cache": "up",
            "timestampUtc": "2026-11-05T20:20:00Z"
          }
        }
      ]
    }
    ```
- **`/healthz` (Liveness)**:
  - Resposta textual rápida (`Healthy`) utilizada pelo orquestrador de contêineres para saber se o processo está vivo.
- **`/ready` (Readiness)**:
  - Resposta textual que valida se a conexão com o PostgreSQL está apta a receber tráfego.

---

## 2. Rastreabilidade de Versão e Diagnóstico de Erros

- **Como conferir se o deploy remoto está atualizado**:
  - Em incidentes onde uma rota retorna `404 Not Found` (como na tela de acesso por e-mail), acesse a rota `/health` ou verifique o link de commit no rodapé da página.
  - Se o hash do commit for diferente do `HEAD` da branch `main`, o contêiner ainda está executando a imagem antiga ou a compilação do deploy ainda está em andamento no provedor.

- **Variáveis de Ambiente Reconhecidas**:
  - `RENDER_GIT_COMMIT`: Injetada automaticamente pelo Render com o hash de 40 caracteres do commit implantado.
  - `GIT_COMMIT`: Variável de fallback para outros provedores de CI/CD.
