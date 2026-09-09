import sys

file_path = "./tests/AssistenciaTech.Application.Tests/Services/LogNotificationServiceTests.cs"

with open(file_path, "r") as f:
    content = f.read()

new_test = """
        [Theory]
        [InlineData(WorkflowStatus.EmAnalise, "está sendo analisado pelo nosso técnico")]
        [InlineData(WorkflowStatus.AguardandoAprovacao, "está pronto:")]
        [InlineData(WorkflowStatus.AguardandoPecas, "estamos aguardando a chegada de peças para o reparo")]
        [InlineData(WorkflowStatus.EmReparo, "está em reparo")]
        [InlineData(WorkflowStatus.Concluido, "foi concluído! Valor:")]
        [InlineData(WorkflowStatus.Entregue, "confirmamos a entrega do seu equipamento")]
        public async Task EnviarNotificacaoStatusAsync_ShouldGenerateCorrectMessage_ForAllKnownStatuses(string status, string expectedMessageFragment)
        {
            // Arrange
            var cliente = new Cliente { Id = 1, Nome = "Test", Telefone = "123" };
            var os = new OrdemServico { Id = 1, Equipamento = "PC", Status = status, ValorOrcamento = 100 };

            // Act
            await _sut.EnviarNotificacaoStatusAsync(cliente, os, "Anterior");

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessageFragment)),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
"""

# Find the last closing brace for the class
index = content.rfind("}")
index2 = content.rfind("}", 0, index)

if index2 != -1:
    new_content = content[:index2] + new_test + content[index2:]
    with open(file_path, "w") as f:
        f.write(new_content)
    print("Modified successfully.")
else:
    print("Could not find insertion point.")
