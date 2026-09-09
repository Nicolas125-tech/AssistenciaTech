import re

with open('tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'r') as f:
    content = f.read()

# CS7036 means we still haven't fixed the constructor correctly for some lines!
# Remaining lines from output: 544, 588, 637, 956, 1276

def add_mock(content, start_idx):
    lines = content.split('\n')
    idx = start_idx - 1
    # Find the closing parenthesis of the `new AdminController` call starting near `start_idx`
    for i in range(idx, min(idx + 30, len(lines))):
        if "new Mock<INotificationService>().Object" in lines[i]:
            lines[i] = lines[i].replace("new Mock<INotificationService>().Object", "new Mock<INotificationService>().Object, new Mock<AssistenciaTech.Services.Workflow.IWorkflowProcessor>().Object")
            break
        elif "_mockNotificationService.Object" in lines[i] and "_mockWorkflowProcessor.Object" not in lines[i]:
            lines[i] = lines[i].replace("_mockNotificationService.Object", "_mockNotificationService.Object, _mockWorkflowProcessor.Object")
            break
        elif "mockNotificationService.Object" in lines[i] and "mockWorkflowProcessor.Object" not in lines[i]:
            lines[i] = lines[i].replace("mockNotificationService.Object", "mockNotificationService.Object, new Mock<AssistenciaTech.Services.Workflow.IWorkflowProcessor>().Object")
            break
    return "\n".join(lines)

content = add_mock(content, 544)
content = add_mock(content, 588)
content = add_mock(content, 637)
content = add_mock(content, 956)
content = add_mock(content, 1276)

with open('tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'w') as f:
    f.write(content)
