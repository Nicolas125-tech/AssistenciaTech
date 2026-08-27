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
        public byte[] GerarXml(Faturamento faturamento)
        {
            if (faturamento?.OrdemServico?.Cliente == null)
            {
                throw new System.ArgumentException("Dados incompletos para geração de NFS-e (Faturamento, OS ou Cliente nulos).");
            }

            var os = faturamento.OrdemServico;
            var cliente = os.Cliente;

            // Estrutura simplificada no padrão ABRASF
            XNamespace ns = "http://www.abrasf.org.br/nfse.xsd";

            var xml = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(ns + "GerarNfseEnvio",
                    new XElement(ns + "Rps",
                        new XElement(ns + "InfDeclaracaoPrestacaoServico",
                            new XElement(ns + "Competencia", System.DateTime.UtcNow.ToString("yyyy-MM-dd")),
                            new XElement(ns + "Servico",
                                new XElement(ns + "Valores",
                                    new XElement(ns + "ValorServicos", os.CustoMaoDeObra.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)),
                                    new XElement(ns + "ValorDeducoes", "0.00"),
                                    new XElement(ns + "ValorPis", "0.00"),
                                    new XElement(ns + "ValorCofins", "0.00"),
                                    new XElement(ns + "ValorInss", "0.00"),
                                    new XElement(ns + "ValorIr", "0.00"),
                                    new XElement(ns + "ValorCsll", "0.00"),
                                    new XElement(ns + "IssRetido", "2"), // 2 = Não
                                    new XElement(ns + "ValorIss", faturamento.ValorISS.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)),
                                    new XElement(ns + "BaseCalculo", faturamento.BaseCalculoISS.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)),
                                    new XElement(ns + "Aliquota", (faturamento.AliquotaISS * 100).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
                                ),
                                new XElement(ns + "Discriminacao", $"Serviço referente à OS #{os.Id} - Equipamento: {os.Equipamento}")
                            ),
                            new XElement(ns + "Prestador",
                                new XElement(ns + "Cnpj", "12345678000199"), // Exemplo Fictício da Assistência
                                new XElement(ns + "InscricaoMunicipal", "123456")
                            ),
                            new XElement(ns + "Tomador",
                                new XElement(ns + "IdentificacaoTomador",
                                    new XElement(ns + "CpfCnpj",
                                        new XElement(ns + "Cpf", cliente.Cpf?.Replace(".", "").Replace("-", "") ?? "00000000000")
                                    )
                                ),
                                new XElement(ns + "RazaoSocial", cliente.Nome),
                                new XElement(ns + "Contato",
                                    new XElement(ns + "Telefone", cliente.Telefone),
                                    new XElement(ns + "Email", cliente.Email)
                                )
                            )
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
    }
}
