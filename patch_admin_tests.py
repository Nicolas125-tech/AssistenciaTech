import re
import os

files_to_patch = [
    "tests/AssistenciaTech.Application.Tests/AdminControllerTests.cs",
    "tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs"
]

for file_path in files_to_patch:
    if not os.path.exists(file_path):
        continue

    with open(file_path, "r") as f:
        content = f.read()

    # Add mock for IServiceScopeFactory
    content = content.replace("private readonly Mock<ILogger<AdminController>> _mockLogger;", "private readonly Mock<ILogger<AdminController>> _mockLogger;\n        private readonly Mock<IServiceScopeFactory> _mockScopeFactory;")

    # Initialize mock in constructor/setup
    content = re.sub(r"(_mockLogger = new Mock<ILogger<AdminController>>\(\);)", r"\1\n            _mockScopeFactory = new Mock<IServiceScopeFactory>();\n\n            // Setup ScopeFactory to return a scope containing the DbContext\n            var mockScope = new Mock<IServiceScope>();\n            var mockServiceProvider = new Mock<IServiceProvider>();\n            mockServiceProvider.Setup(sp => sp.GetService(typeof(AppDbContext))).Returns(_context);\n            mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);\n            _mockScopeFactory.Setup(s => s.CreateScope()).Returns(mockScope.Object);", content)

    # Update AdminController instantiations
    content = re.sub(
        r"(new AdminController\([^,]+, [^,]+, [^,]+, [^,]+, [^,]+, [^,]+, _mockLogger\.Object)\)",
        r"\1, _mockScopeFactory.Object)",
        content
    )

    # For inline instantiations that might be missed
    content = re.sub(
        r"(= new AdminController\((.*?_logger\.Object.*?))\)",
        r"\1, _mockScopeFactory.Object)",
        content
    )
    # Check for context passing
    content = re.sub(
        r"(= new AdminController\(context,\s*mockEstoque\.Object,\s*mockEnv\.Object,\s*mockPdf\.Object,\s*mockDashboard\.Object,\s*mockBackup\.Object,\s*mockLogger\.Object\))",
        r"= new AdminController(context, mockEstoque.Object, mockEnv.Object, mockPdf.Object, mockDashboard.Object, mockBackup.Object, mockLogger.Object, _mockScopeFactory.Object)",
        content
    )

    with open(file_path, "w") as f:
        f.write(content)
