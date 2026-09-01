import re

with open("tests/AssistenciaTech.Application.Tests/Controllers/FaturamentosControllerTests.cs", "r") as f:
    content = f.read()

target1 = "_controller = new FaturamentosController(_context, _mockConfiguration.Object);"
replace1 = "_controller = new FaturamentosController(_context, _mockConfiguration.Object, new Mock<AssistenciaTech.Services.ITributacaoService>().Object, new Mock<AssistenciaTech.Services.INfseXmlGeneratorService>().Object);"

target2 = "var controller = new FaturamentosController(mockContext, mockConfig.Object);"
replace2 = "var controller = new FaturamentosController(mockContext, mockConfig.Object, new Mock<AssistenciaTech.Services.ITributacaoService>().Object, new Mock<AssistenciaTech.Services.INfseXmlGeneratorService>().Object);"

content = content.replace(target1, replace1)
content = content.replace(target2, replace2)

with open("tests/AssistenciaTech.Application.Tests/Controllers/FaturamentosControllerTests.cs", "w") as f:
    f.write(content)
