# Glossário de Domínio · TechOS

Este documento define os termos de domínio e a linguagem ubíqua utilizada no TechOS.

---

### 1. Entidades e Conceitos Centrais

- **Ordem de Serviço (OS)**: Registro central de um equipamento recebido na oficina para diagnóstico ou reparo. Contém histórico de transições de status, laudos técnicos, custos de mão de obra e peças, checklist de entrada e fotos de evidências.
- **OrdemServicoLifecycle**: Módulo profundo responsável por governar todas as transições de estado da OS. Encapsula em uma única transação atômica a validação do State Pattern, a dedução e estorno de peças do estoque, a reserva e devolução de aparelhos de backup e o disparo de notificações ao cliente.
- **State Pattern (Estados da OS)**: Máquina de estados que define o fluxo de vida da OS:
  - `Recebido`: Equipamento deu entrada na oficina.
  - `Em Análise`: Técnico responsável está realizando testes e diagnósticos preliminares.
  - `Aguardando Aprovação`: Orçamento emitido aguardando confirmação do cliente.
  - `Aguardando Peças`: Peças necessárias solicitadas a fornecedores ou aguardando chegada.
  - `Em Reparo`: Reparo ou manutenção em andamento na bancada.
  - `Concluído`: Manutenção finalizada. Dispara dedução automática de peças no estoque e notificação ao cliente.
  - `Entregue ao Cliente`: Equipamento retirado pelo cliente. Exige a devolução prévia de eventual aparelho de backup emprestado.
  - `Cancelado`: Serviço recusado ou inviabilizado. Realiza o estorno de peças reservadas caso tenha havido baixa anterior.
- **Equipamento de Backup**: Dispositivo (notebook ou computador) cedido temporariamente ao cliente em regime de comodato/empréstimo enquanto o equipamento original é reparado. O status de disponibilidade do backup é gerenciado automaticamente pelo ciclo de vida da OS.
- **Laudo Técnico**: Parecer conclusivo emitido pelo técnico sobre o defeito encontrado, intervenções realizadas e peças substituídas.
- **Visita em Campo**: Atendimento presencial realizado por técnico externo via aplicativo mobile com registro de coordenadas GPS (check-in/check-out) e coleta de assinatura digital do cliente.
- **Faturamento & Pix**: Registro financeiro de cobrança vinculado a uma OS concluída, contendo cálculo de tributos (ISS e ICMS), chave Pix estática/dinâmica e controle de webhook de pagamento.
- **FaturamentoEngine**: Módulo profundo responsável por governar o cálculo tributário (ISS/ICMS), geração de payload NFS-e e persistência transacional de faturamentos de Ordens de Serviço.
- **EstoqueEngine**: Módulo profundo responsável por governar o ciclo de vida do inventário e catálogo de peças da oficina. Centraliza movimentações de saldo, deduções e estornos por OS, alertas de estoque crítico com invalidação atômica de cache, proteção de integridade referencial e controle de concorrência otimista.
- **VisitaCampoEngine**: Módulo profundo responsável por governar o atendimento de técnicos externos e a telemetria mobile em campo. Encapsula autenticação e emissão de tokens JWT, check-in e check-out com coordenadas GPS, captura de assinatura digital com avanço coordenado da OS e reconciliação transacional idempotente de lotes offline.
- **AreaClienteEngine**: Módulo profundo responsável por governar o autoatendimento e rastreamento de ordens de serviço pelo cliente. Centraliza a consulta pública com sanitização de documentos em alta performance (Span<char>), proteção contra DoS, correspondência flexível de CPF, listagem de equipamentos do cliente e emissão de credenciais para autenticação web.
