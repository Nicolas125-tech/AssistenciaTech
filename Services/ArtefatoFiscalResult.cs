namespace AssistenciaTech.Services
{
    public class ArtefatoFiscalResult
    {
        public bool Sucesso { get; set; }
        public byte[]? ConteudoBytes { get; set; }
        public string? NomeArquivo { get; set; }
        public string? ContentType { get; set; }
        public string? MensagemErro { get; set; }
        public int StatusCode { get; set; } = 200;

        public static ArtefatoFiscalResult Ok(byte[] conteudo, string nomeArquivo, string contentType)
        {
            return new ArtefatoFiscalResult
            {
                Sucesso = true,
                ConteudoBytes = conteudo,
                NomeArquivo = nomeArquivo,
                ContentType = contentType,
                StatusCode = 200
            };
        }

        public static ArtefatoFiscalResult Falha(string mensagem, int statusCode = 400)
        {
            return new ArtefatoFiscalResult
            {
                Sucesso = false,
                MensagemErro = mensagem,
                StatusCode = statusCode
            };
        }
    }
}
