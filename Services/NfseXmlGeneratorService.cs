using AssistenciaTech.Models;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace AssistenciaTech.Services
{
    public interface INfseXmlGeneratorService
    {
        byte[] GerarXml(Faturamento faturamento);
    }

    public class NfseXmlGeneratorService : INfseXmlGeneratorService
    {
        private static readonly XNamespace _ns = "http://www.abrasf.org.br/nfse.xsd";

        public byte[] GerarXml(Faturamento faturamento)
        {
            if (faturamento?.OrdemServico?.Cliente == null)
            {
                throw new System.ArgumentException("Dados incompletos para geração de NFS-e (Faturamento, OS ou Cliente nulos).");
            }

            var os = faturamento.OrdemServico;
            var cliente = os.Cliente;

            var xml = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(_ns + "GerarNfseEnvio",
                    new XElement(_ns + "Rps",
                        new XElement(_ns + "InfDeclaracaoPrestacaoServico",
                            new XElement(_ns + "Competencia", System.DateTime.UtcNow.ToString("yyyy-MM-dd")),
                            GerarServico(faturamento, os),
                            GerarPrestador(),
                            GerarTomador(cliente)
                        )
                    )
                )
            );

            // Gerar XML com formatação correta UTF-8
            using var memoryStream = new MemoryStream();
            // Necessário especificar "false" no OmitXmlDeclaration para garantir o <?xml version="1.0" encoding="utf-8"?>
            using var xmlWriter = System.Xml.XmlWriter.Create(memoryStream, new System.Xml.XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false), // sem BOM
                Indent = true,
                OmitXmlDeclaration = false
            });

            xml.Save(xmlWriter);
            xmlWriter.Flush();
            return memoryStream.ToArray();
        }

        private XElement GerarServico(Faturamento faturamento, OrdemServico os)
        {
            return new XElement(_ns + "Servico",
                new XElement(_ns + "Valores",
                    new XElement(_ns + "ValorServicos", os.CustoMaoDeObra.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)),
                    new XElement(_ns + "ValorDeducoes", "0.00"),
                    new XElement(_ns + "ValorPis", "0.00"),
                    new XElement(_ns + "ValorCofins", "0.00"),
                    new XElement(_ns + "ValorInss", "0.00"),
                    new XElement(_ns + "ValorIr", "0.00"),
                    new XElement(_ns + "ValorCsll", "0.00"),
                    new XElement(_ns + "IssRetido", "2"), // 2 = Não
                    new XElement(_ns + "ValorIss", faturamento.ValorISS.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)),
                    new XElement(_ns + "BaseCalculo", faturamento.BaseCalculoISS.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)),
                    new XElement(_ns + "Aliquota", (faturamento.AliquotaISS * 100).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
                ),
                new XElement(_ns + "Discriminacao", $"Serviço referente à OS #{os.Id} - Equipamento: {os.Equipamento}")
            );
        }

        private XElement GerarPrestador()
        {
            return new XElement(_ns + "Prestador",
                new XElement(_ns + "Cnpj", "12345678000199"), // Exemplo Fictício da Assistência
                new XElement(_ns + "InscricaoMunicipal", "123456")
            );
        }

        private XElement GerarTomador(Cliente cliente)
        {
            return new XElement(_ns + "Tomador",
                new XElement(_ns + "IdentificacaoTomador",
                    new XElement(_ns + "CpfCnpj",
                        new XElement(_ns + "Cpf", cliente.Cpf?.Replace(".", "").Replace("-", "") ?? "00000000000")
                    )
                ),
                new XElement(_ns + "RazaoSocial", cliente.Nome),
                new XElement(_ns + "Contato",
                    new XElement(_ns + "Telefone", cliente.Telefone),
                    new XElement(_ns + "Email", cliente.Email)
                )
            );
        }
    }
}
