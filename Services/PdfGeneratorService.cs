using System;
using System.Linq;
using AssistenciaTech.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Microsoft.AspNetCore.Http;
using QRCoder;

namespace AssistenciaTech.Services
{
    public interface IPdfGeneratorService
    {
        byte[] GenerateOsPdf(OrdemServico os);
    }

    public class PdfGeneratorService : IPdfGeneratorService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public PdfGeneratorService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public byte[] GenerateOsPdf(OrdemServico os)
        {
            if (os == null) throw new ArgumentNullException(nameof(os));

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Header().Element(c => ComposeHeader(c, os));
                    page.Content().Element(c => ComposeContent(c, os));
                    page.Footer().Element(c => ComposeFooter(c, os));
                });
            }).GeneratePdf();
        }

        private void ComposeHeader(IContainer headerContainer, OrdemServico os)
        {
            byte[] qrCodeImage = GenerateQrCode(os.Id);

            headerContainer.PaddingBottom(10).BorderBottom(2).BorderColor(Colors.Blue.Darken2).Row(row =>
            {
                // Dados da Empresa
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("ASSISTÊNCIA TECH LTDA").FontSize(20).Black().FontColor(Colors.Blue.Darken2);
                    column.Item().Text("CNPJ: 00.000.000/0001-00");
                    column.Item().Text("Av. das Tecnologias, 1000 - Centro, São Paulo/SP");
                    column.Item().Text("Telefone: (11) 99999-9999 | E-mail: contato@assistenciatech.com");
                });

                // QR Code
                if (qrCodeImage != null)
                {
                    row.ConstantItem(70).Height(70).Image(qrCodeImage);
                }

                // Info da OS
                row.ConstantItem(150).AlignRight().Column(col =>
                {
                    col.Item().Text($"ORDEM DE SERVIÇO").FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
                    col.Item().Text($"Nº {os.Id:D5}").FontSize(18).Bold().FontColor(Colors.Red.Medium);
                    col.Item().Text($"Data: {os.DataEntrada:dd/MM/yyyy}");
                });
            });
        }

        private byte[] GenerateQrCode(int osId)
        {
            var request = _httpContextAccessor.HttpContext?.Request;
            if (request == null) return Array.Empty<byte>();

            var baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";
            var linkAcompanhamento = $"{baseUrl}/Consulta?numeroOS={osId}";

            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(linkAcompanhamento, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            
            return qrCode.GetGraphic(20);
        }

        private void ComposeContent(IContainer contentContainer, OrdemServico os)
        {
            contentContainer.PaddingVertical(10).Column(column =>
            {
                column.Spacing(15);

                // Bloco do Cliente
                column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                {
                    c.Item().Text("DADOS DO CLIENTE").SemiBold().FontSize(12).FontColor(Colors.Grey.Darken3);
                    c.Item().PaddingBottom(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    c.Item().Row(r => 
                    {
                        r.RelativeItem().Text($"Nome: {os.Cliente?.Nome}");
                        r.RelativeItem().Text($"CPF/CNPJ: {os.Cliente?.Cpf}");
                    });
                    c.Item().Row(r => 
                    {
                        r.RelativeItem().Text($"Telefone: {os.Cliente?.Telefone}");
                        r.RelativeItem().Text($"E-mail: {os.Cliente?.Email}");
                    });
                });

                // Bloco do Equipamento
                column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                {
                    c.Item().Text("DADOS DO EQUIPAMENTO").SemiBold().FontSize(12).FontColor(Colors.Grey.Darken3);
                    c.Item().PaddingBottom(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    c.Item().Row(r => 
                    {
                        r.RelativeItem(2).Text($"Equipamento: {os.Equipamento}");
                        r.RelativeItem(1).Text($"Nº Série: {(string.IsNullOrEmpty(os.NumeroSerie) ? "N/A" : os.NumeroSerie)}");
                    });
                    c.Item().Text($"Defeito Relatado: {os.ProblemaRelatado}");
                    c.Item().PaddingTop(5).Text($"Laudo Técnico: {(string.IsNullOrEmpty(os.LaudoTecnico) ? "Aguardando análise" : os.LaudoTecnico)}");
                    if (!string.IsNullOrEmpty(os.AvariasPreExistentes))
                        c.Item().Text($"Avarias Existentes: {os.AvariasPreExistentes}").FontColor(Colors.Red.Darken2);
                });

                // Tabela de Custos e Peças
                if (os.PecasUtilizadas != null && os.PecasUtilizadas.Any() || os.CustoMaoDeObra > 0)
                {
                    column.Item().Text("CUSTOS E SERVIÇOS").SemiBold().FontSize(12).FontColor(Colors.Grey.Darken3);
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3); // Descrição
                            columns.ConstantColumn(70); // Qtd
                            columns.RelativeColumn(1); // V. Unit
                            columns.RelativeColumn(1); // Total
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(2).Text("Descrição").SemiBold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(2).AlignCenter().Text("Qtd").SemiBold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(2).AlignRight().Text("V. Unitário").SemiBold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(2).AlignRight().Text("V. Total").SemiBold();
                        });

                        // Peças
                        if (os.PecasUtilizadas != null)
                        {
                            foreach (var item in os.PecasUtilizadas)
                            {
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten4).Padding(2).Text(item.Peca?.Nome ?? "Peça");
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten4).Padding(2).AlignCenter().Text(item.Quantidade.ToString());
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten4).Padding(2).AlignRight().Text(item.ValorVenda.ToString("C"));
                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten4).Padding(2).AlignRight().Text((item.Quantidade * item.ValorVenda).ToString("C"));
                            }
                        }

                        // Mão de Obra
                        if (os.CustoMaoDeObra > 0)
                        {
                            table.Cell().Padding(2).Text("Mão de Obra / Serviços Técnicos");
                            table.Cell().Padding(2).AlignCenter().Text("1");
                            table.Cell().Padding(2).AlignRight().Text(os.CustoMaoDeObra.ToString("C"));
                            table.Cell().Padding(2).AlignRight().Text(os.CustoMaoDeObra.ToString("C"));
                        }
                    });
                }

                // Resumo Financeiro
                column.Item().AlignRight().Column(c =>
                {
                    c.Item().Text($"Subtotal: {os.CustoPecas + os.CustoMaoDeObra:C}");
                    if (os.DescontoAplicado > 0)
                    {
                        c.Item().Text($"Desconto: -{os.DescontoAplicado:C}").FontColor(Colors.Red.Medium);
                    }
                    c.Item().PaddingTop(5).Text($"TOTAL FINAL: {os.ValorTotalCalculado:C}").FontSize(16).Bold().FontColor(Colors.Blue.Darken2);
                });

                // Termos Legais
                column.Item().PaddingTop(20).Background(Colors.Grey.Lighten4).Padding(10).Text(t =>
                {
                    t.Span("TERMO DE GARANTIA E ACEITE: ").Bold().FontSize(9);
                    t.Span("Declaro ter testado e retirado o equipamento em perfeitas condições de funcionamento. " +
                           "Garantia de 90 dias para os serviços prestados e peças substituídas, de acordo com o Código de Defesa do Consumidor, " +
                           "contados a partir da data de entrega. A garantia não cobre mau uso, quedas, líquidos ou rompimento do selo de garantia. " +
                           "Equipamentos não retirados após 90 dias da conclusão serão considerados abandonados.").FontSize(9);
                });
            });
        }

        private void ComposeFooter(IContainer footerContainer, OrdemServico os)
        {
            footerContainer.Column(column =>
            {
                // Assinaturas
                column.Item().PaddingTop(30).Row(row =>
                {
                    row.RelativeItem().AlignCenter().Column(c =>
                    {
                        c.Item().LineHorizontal(1);
                        c.Item().PaddingTop(5).Text("Assinatura do Cliente").FontSize(10);
                        c.Item().Text(os.Cliente?.Nome).FontSize(9).FontColor(Colors.Grey.Darken1);
                    });

                    row.ConstantItem(50); // Espaçamento

                    row.RelativeItem().AlignCenter().Column(c =>
                    {
                        c.Item().LineHorizontal(1);
                        c.Item().PaddingTop(5).Text("Técnico Responsável").FontSize(10);
                        c.Item().Text(os.TecnicoResponsavel?.Nome ?? "Assistência Tech").FontSize(9).FontColor(Colors.Grey.Darken1);
                    });
                });

                column.Item().PaddingTop(15).AlignCenter().Text(x =>
                {
                    x.Span("Gerado pelo sistema em: ").FontSize(8).FontColor(Colors.Grey.Medium);
                    x.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(8).FontColor(Colors.Grey.Medium);
                });
            });
        }
    }
}
