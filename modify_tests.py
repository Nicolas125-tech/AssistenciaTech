with open('tests/AssistenciaTech.Domain.Tests/EmAnaliseStateTests.cs', 'r') as f:
    lines = f.readlines()

new_test = """
    [Fact]
    public void Avancar_EstadoDireto_SemDiagnostico_DeveLancarExcecao()
    {
        // Arrange
        var os = new OrdemServico("OS-001", "123.456.789-00", "Modelo", "NS123", "Defeito");
        var estado = new EmAnaliseState();

        // Act
        Action act = () => estado.Avancar(os);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Não é possível aprovar ou finalizar uma OS sem o diagnóstico técnico.");
    }
"""

lines.insert(63, new_test)

with open('tests/AssistenciaTech.Domain.Tests/EmAnaliseStateTests.cs', 'w') as f:
    f.writelines(lines)
