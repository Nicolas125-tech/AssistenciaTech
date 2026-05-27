using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AssistenciaTech.Models
{
    /// <summary>
    /// Modelo que representa a Ordem de Serviço (OS) para os equipamentos em manutenção.
    /// </summary>
    public class OrdemServico
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "O ID do Cliente é obrigatório.")]
        [ForeignKey("Cliente")]
        public int ClienteId { get; set; }
        public Cliente? Cliente { get; set; }

        [Required(ErrorMessage = "O nome do equipamento é obrigatório.")]
        [Display(Name = "Equipamento")]
        public string Equipamento { get; set; } = string.Empty; // Ex: Notebook Dell Inspiron, PC Gamer

        [Display(Name = "Número de Série")]
        public string? NumeroSerie { get; set; } // Para histórico de garantia

        [Required(ErrorMessage = "A descrição do problema é obrigatória.")]
        [Display(Name = "Problema Relatado")]
        public string ProblemaRelatado { get; set; } = string.Empty; // Ex: Não liga, precisa de formatação

        // Relacionamentos para Funcionalidades Empresariais
        public ICollection<OrdemServicoPeca> PecasUtilizadas { get; set; } = new List<OrdemServicoPeca>();
        public ICollection<Evidencia> Evidencias { get; set; } = new List<Evidencia>();

        [Display(Name = "Laudo Técnico (Diagnóstico)")]
        public string? LaudoTecnico { get; set; } // O diagnóstico real dado pelo técnico

        // === Funcionalidades Enterprise Nível 2 ===
        
        [Display(Name = "Equipamento de Backup")]
        public int? EquipamentoBackupId { get; set; }
        public EquipamentoBackup? EquipamentoBackup { get; set; }

        [Display(Name = "Contrato / SLA")]
        public int? ContratoId { get; set; }
        public Contrato? Contrato { get; set; }

        [Display(Name = "Contador de Páginas Inicial")]
        public int? ContadorPaginasInicial { get; set; }

        [Display(Name = "Contador de Páginas Final")]
        public int? ContadorPaginasFinal { get; set; }

        [Display(Name = "Técnico Responsável")]
        public int? TecnicoId { get; set; }
        public Tecnico? TecnicoResponsavel { get; set; }

        // RMA / Terceirizados
        [Display(Name = "Enviado para Terceirizado?")]
        public bool EnviadoParaTerceiro { get; set; } = false;

        [Display(Name = "Nome do Parceiro/Laboratório")]
        public string? NomeParceiro { get; set; }

        [Display(Name = "Custo Terceirizado")]
        public decimal CustoTerceirizado { get; set; } = 0;

        [Display(Name = "Previsão de Retorno (RMA)")]
        public DateTime? PrevisaoRetornoParceiro { get; set; }

        // Propriedade Computada para Comissão
        [NotMapped]
        public decimal ValorComissao 
        {
            get 
            {
                if (TecnicoResponsavel != null)
                {
                    return CustoMaoDeObra * (TecnicoResponsavel.PercentualComissao / 100);
                }
                return 0m;
            }
        }

        // ==========================================

        [Display(Name = "Avarias Pré-Existentes")]
        public string? AvariasPreExistentes { get; set; } // Checklist visual

        [Required]
        public string Status { get; set; } = "Recebido"; // Fluxo de Trabalho Restrito

        [DataType(DataType.Currency)]
        [Display(Name = "Custo de Peças")]
        public decimal CustoPecas { get; set; }

        [DataType(DataType.Currency)]
        [Display(Name = "Mão de Obra")]
        public decimal CustoMaoDeObra { get; set; }

        [DataType(DataType.Currency)]
        [Display(Name = "Desconto")]
        public decimal DescontoAplicado { get; set; }

        [DataType(DataType.Currency)]
        [Display(Name = "Valor do Orçamento")]
        public decimal ValorOrcamento { get; set; } // Valor final armazenado. Pode ser calculado.

        [Display(Name = "Data de Entrada")]
        public DateTime DataEntrada { get; set; } = DateTime.Now;

        [Display(Name = "Data de Conclusão")]
        public DateTime? DataConclusao { get; set; } // Quando o reparo termina

        [Display(Name = "Data de Entrega")]
        public DateTime? DataEntregaCliente { get; set; } // Quando o cliente retira

        // === Propriedades Calculadas (Regras de Negócio em Tempo Real) ===

        [NotMapped]
        public decimal ValorTotalCalculado => (CustoPecas + CustoMaoDeObra) - DescontoAplicado;

        [NotMapped]
        public DateTime? DataVencimentoGarantia => DataEntregaCliente?.AddDays(90);

        [NotMapped]
        public bool GarantiaAtiva => DataVencimentoGarantia.HasValue && DateTime.Now.Date <= DataVencimentoGarantia.Value.Date;

        [NotMapped]
        public bool AparelhoAbandonado => Status == "Concluído" && DataConclusao.HasValue && !DataEntregaCliente.HasValue && (DateTime.Now - DataConclusao.Value).TotalDays > 90;
    }
}
