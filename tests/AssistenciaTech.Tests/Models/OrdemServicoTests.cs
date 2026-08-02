using System;
using Xunit;
using AssistenciaTech.Models;

namespace AssistenciaTech.Tests.Models
{
    public class OrdemServicoTests
    {
        [Fact]
        public void GarantiaAtiva_SemDataEntrega_ReturnsFalse()
        {
            var os = new OrdemServico();
            Assert.False(os.IsGarantiaAtiva(DateTime.Now));
        }

        [Fact]
        public void GarantiaAtiva_DentroDaGarantia_ReturnsTrue()
        {
            var os = new OrdemServico
            {
                DataEntregaCliente = new DateTime(2023, 1, 1)
            };

            // Garantia is 90 days, so until 2023-04-01
            Assert.True(os.IsGarantiaAtiva(new DateTime(2023, 3, 31)));
        }

        [Fact]
        public void GarantiaAtiva_ExatamenteNoVencimento_ReturnsTrue()
        {
            var os = new OrdemServico
            {
                DataEntregaCliente = new DateTime(2023, 1, 1)
            };

            // Vencimento is exactly 90 days
            var vencimento = os.DataEntregaCliente.Value.AddDays(90);

            Assert.True(os.IsGarantiaAtiva(vencimento));
        }

        [Fact]
        public void GarantiaAtiva_ForaDaGarantia_ReturnsFalse()
        {
            var os = new OrdemServico
            {
                DataEntregaCliente = new DateTime(2023, 1, 1)
            };

            // Garantia is 90 days, so until 2023-04-01
            Assert.False(os.IsGarantiaAtiva(new DateTime(2023, 4, 2)));
        }

        [Fact]
        public void AparelhoAbandonado_NaoConcluido_ReturnsFalse()
        {
            var os = new OrdemServico
            {
                Status = "Em andamento",
                DataConclusao = new DateTime(2023, 1, 1)
            };

            Assert.False(os.IsAparelhoAbandonado(new DateTime(2023, 5, 1)));
        }

        [Fact]
        public void AparelhoAbandonado_MenosDe90Dias_ReturnsFalse()
        {
            var os = new OrdemServico
            {
                Status = "Concluído",
                DataConclusao = new DateTime(2023, 1, 1)
            };

            Assert.False(os.IsAparelhoAbandonado(new DateTime(2023, 3, 1)));
        }

        [Fact]
        public void AparelhoAbandonado_MaisDe90DiasMasEntregue_ReturnsFalse()
        {
            var os = new OrdemServico
            {
                Status = "Concluído",
                DataConclusao = new DateTime(2023, 1, 1),
                DataEntregaCliente = new DateTime(2023, 1, 2)
            };

            Assert.False(os.IsAparelhoAbandonado(new DateTime(2023, 5, 1)));
        }

        [Fact]
        public void AparelhoAbandonado_MaisDe90DiasNaoEntregue_ReturnsTrue()
        {
            var os = new OrdemServico
            {
                Status = "Concluído",
                DataConclusao = new DateTime(2023, 1, 1)
            };

            Assert.True(os.IsAparelhoAbandonado(new DateTime(2023, 5, 1)));
        }
    }
}
