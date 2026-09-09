import re

with open('./tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'r') as f:
    content = f.read()

# I will revert the broken Create_Get test, and check what else is missing!
pattern = r'\[Fact\]\s+public void Create_Get_ReturnsRedirectToIndex_WhenDatabaseFails\(\).*?localController\.Dispose\(\);\s+\}'
content = re.sub(pattern, "", content, flags=re.DOTALL)

with open('./tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'w') as f:
    f.write(content)
