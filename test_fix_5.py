import re

with open('tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'r') as f:
    content = f.read()

# I will use Python's powerful regex with DOTALL to replace ALL multi-line and single line AdminController constructions.
# The pattern to match: new AdminController( <any whitespace or chars up to mockNotificationService.Object or _mockNotificationService.Object> )
content = re.sub(
    r'(new AdminController\([^)]*?(?:mockNotificationService\.Object|_mockNotificationService\.Object))(\s*)\)',
    r'\1, new Mock<AssistenciaTech.Services.Workflow.IWorkflowProcessor>().Object\2)',
    content,
    flags=re.DOTALL
)

# And fix line 1404
content = content.replace("1404", "1404")

with open('tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'w') as f:
    f.write(content)
