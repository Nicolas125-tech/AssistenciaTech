import re

with open('tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'r') as f:
    content = f.read()

# CS7036 means we still haven't fixed the constructor correctly for some lines!
# lines: 544, 588, 637, 956, 1276
# CS0103 means we still haven't fixed the missing variable correctly on line 1404!

def fix_line(content, line_num):
    lines = content.split('\n')
    idx = line_num - 1

    # Try to find the closing parenthesis of the constructor and add the missing parameter
    for i in range(idx, min(idx + 30, len(lines))):
        if "mockNotificationService.Object" in lines[i] or "_mockNotificationService.Object" in lines[i]:
            if "mockWorkflowProcessor" not in lines[i] and "Mock<" not in lines[i]:
                if "_mockNotificationService" in lines[i]:
                    lines[i] = lines[i].replace("_mockNotificationService.Object", "_mockNotificationService.Object, _mockWorkflowProcessor.Object")
                else:
                    lines[i] = lines[i].replace("mockNotificationService.Object", "mockNotificationService.Object, new Mock<AssistenciaTech.Services.Workflow.IWorkflowProcessor>().Object")
                break
    return "\n".join(lines)

content = fix_line(content, 544)
content = fix_line(content, 588)
content = fix_line(content, 637)
content = fix_line(content, 956)
content = fix_line(content, 1276)
content = fix_line(content, 1404)

with open('tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'w') as f:
    f.write(content)
