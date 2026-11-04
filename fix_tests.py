import re

with open('tests/AssistenciaTech.Application.Tests/Controllers/PecasControllerTests.cs', 'r') as f:
    content = f.read()

content = content.replace(
    '.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())',
    '.UseSqlite("DataSource=:memory:;Foreign Keys=False")'
)

with open('tests/AssistenciaTech.Application.Tests/Controllers/PecasControllerTests.cs', 'w') as f:
    f.write(content)
