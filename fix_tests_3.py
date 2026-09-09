import re

with open('tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'r') as f:
    content = f.read()

# Make sure all AdminController constructors have the correct 10 arguments instead of 9.
# It seems some of them are multiline.
def replacer(match):
    m = match.group(0)
    if "IWorkflowProcessor" not in m and "mockWorkflowProcessor" not in m:
        if "mockNotificationService.Object" in m:
            return m.replace("mockNotificationService.Object", "mockNotificationService.Object, new Mock<AssistenciaTech.Services.Workflow.IWorkflowProcessor>().Object")
    return m

# Using regex to find multiline new AdminController( ... );
content = re.sub(r'new AdminController\([^;]*?\);', replacer, content, flags=re.DOTALL)

with open('tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'w') as f:
    f.write(content)
