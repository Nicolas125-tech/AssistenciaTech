namespace AssistenciaTech.Models
{
    public static class WorkflowStatus
    {
        public const string Recebido = "Recebido";
        public const string EmAnalise = "Em Análise";
        public const string AguardandoAprovacao = "Aguardando Aprovação do Orçamento";
        public const string AguardandoPecas = "Aguardando Peças";
        public const string EmReparo = "Em Reparo";
        public const string Concluido = "Concluído";
        public const string Entregue = "Entregue ao Cliente";

        public static readonly string[] Todos = new[]
        {
            Recebido,
            EmAnalise,
            AguardandoAprovacao,
            AguardandoPecas,
            EmReparo,
            Concluido,
            Entregue
        };
    }
}
