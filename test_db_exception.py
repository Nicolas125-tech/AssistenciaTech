import re

with open('./tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'r') as f:
    content = f.read()

print(content.find("DB_CONNECTION_ERROR (Admin/Index)"))
print(content.find("Create_Post_ReturnsViewResult_WithModelStateError_WhenDatabaseFails"))
