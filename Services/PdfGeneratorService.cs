using System;
using AssistenciaTech.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AssistenciaTech.Services
{
    public interface IPdfGeneratorService
    {
        byte[] GenerateOsPdf(OrdemServico os);
    }

    public class PdfGeneratorService : IPdfGeneratorService
    {
        public byte[] GenerateOsPdf(OrdemServico os)
        {
            if (os == null)
            {
                throw new ArgumentNullException(nameof(os));
            }

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header().Element(c => ComposeHeader(c, os));
                    page.Content().Element(c => ComposeContent(c, os));
                    page.Footer().Element(c => ComposeFooter(c, os));
                });
            }).GeneratePdf();
        }

        private void ComposeHeader(IContainer headerContainer, OrdemServico os)
        {
            headerContainer.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("Assistência Tech").FontSize(24).SemiBold().FontColor(Colors.Blue.Darken2);
                    column.Item().Text("Soluções em Tecnologia");
                });

                row.ConstantItem(150).AlignRight().Text($"Ordem de Serviço Nº {os.Id}").FontSize(16).Bold();
            });
        }

        private void ComposeContent(IContainer contentContainer, OrdemServico os)
        {
            contentContainer.PaddingVertical(1, Unit.Centimetre).Column(column =>
            {
                column.Spacing(20);

                // Dados do Cliente
                column.Item().Background(Colors.Grey.Lighten4).Padding(10).Column(c =>
                {
                    c.Item().Text("Dados do Cliente").SemiBold().FontSize(14);
                    c.Item().Text($"Nome: {os.Cliente?.Nome}");
                    c.Item().Text($"CPF: {os.Cliente?.Cpf}");
                });

                // Dados do Equipamento e Serviço
                column.Item().Background(Colors.Grey.Lighten4).Padding(10).Column(c =>
                {
                    c.Item().Text("Detalhes do Serviço").SemiBold().FontSize(14);
                    c.Item().Text($"Equipamento: {os.Equipamento}");
                    if (!string.IsNullOrEmpty(os.NumeroSerie))
                        c.Item().Text($"Nº de Série: {os.NumeroSerie}");
                    c.Item().Text($"Defeito Relatado: {os.ProblemaRelatado}");
                    if (!string.IsNullOrEmpty(os.LaudoTecnico))
                        c.Item().Text($"Laudo Técnico: {os.LaudoTecnico}");
                    if (!string.IsNullOrEmpty(os.AvariasPreExistentes))
                        c.Item().Text($"Avarias Pré-Existentes: {os.AvariasPreExistentes}");
                    c.Item().Text($"Status: {os.Status}").FontColor(os.Status == WorkflowStatus.Concluido || os.Status == WorkflowStatus.Entregue ? Colors.Green.Darken2 : Colors.Orange.Darken2).SemiBold();
                    c.Item().Text($"Data de Entrada: {os.DataEntrada:dd/MM/yyyy HH:mm}");
                    if (os.DataEntregaCliente.HasValue)
                    {
                        c.Item().Text($"Data de Entrega: {os.DataEntregaCliente:dd/MM/yyyy HH:mm}");
                    }

                    if (os.DataEntregaCliente.HasValue && os.GarantiaAtiva)
                    {
                        c.Item().Text($"Garantia Válida até: {os.DataVencimentoGarantia:dd/MM/yyyy}");
                    }
                });
            });
        }

        private void ComposeFooter(IContainer footerContainer, OrdemServico os)
        {
            footerContainer.Column(column =>
            {
                // Orçamento
                column.Item().PaddingBottom(2, Unit.Centimetre).AlignRight()
                    .Text($"Valor do Orçamento: {os.ValorOrcamento:C}").FontSize(16).SemiBold();

                // Assinatura
                column.Item().AlignCenter().Text("___________________________________________________");
                column.Item().AlignCenter().Text("Assinatura do Cliente");
            });
        }
    }
}
