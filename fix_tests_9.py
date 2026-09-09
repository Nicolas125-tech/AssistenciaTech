import re

with open('tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'r') as f:
    content = f.read()

# CS7036 means we still haven't fixed the constructor correctly for some lines!
# Remaining lines from output: 544, 588, 637, 956, 1276

def ensure_arg(line_no, content):
    lines = content.split('\n')
    idx = line_no - 1
    # Try replacing mockNotificationService.Object
    for i in range(idx, min(idx + 30, len(lines))):
        if "mockNotificationService.Object" in lines[i] or "_mockNotificationService.Object" in lines[i]:
            if "mockWorkflowProcessor" not in lines[i] and "Mock<" not in lines[i]:
                if "_mockNotificationService" in lines[i]:
                    lines[i] = lines[i].replace("_mockNotificationService.Object", "_mockNotificationService.Object, _mockWorkflowProcessor.Object")
                else:
                    lines[i] = lines[i].replace("mockNotificationService.Object", "mockNotificationService.Object, new Mock<AssistenciaTech.Services.Workflow.IWorkflowProcessor>().Object")
            break
    return "\n".join(lines)

content = ensure_arg(544, content)
content = ensure_arg(588, content)
content = ensure_arg(637, content)
content = ensure_arg(956, content)
content = ensure_arg(1276, content)

with open('tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'w') as f:
    f.write(content)
