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
