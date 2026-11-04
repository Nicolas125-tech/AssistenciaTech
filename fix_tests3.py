import re

with open('tests/AssistenciaTech.Application.Tests/Controllers/PecasControllerTests.cs', 'r') as f:
    content = f.read()

# Also need to fix the other places that create DbContextOptionsBuilder
content = content.replace(
    """var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite("DataSource=:memory:;Foreign Keys=False")
                .Options;

            using var concurrencyContext = new ConcurrencyAppDbContext(options);""",
    """var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite("DataSource=:memory:;Foreign Keys=False")
                .Options;

            using var concurrencyContext = new ConcurrencyAppDbContext(options);
            concurrencyContext.Database.OpenConnection();
            concurrencyContext.Database.EnsureCreated();"""
)

with open('tests/AssistenciaTech.Application.Tests/Controllers/PecasControllerTests.cs', 'w') as f:
    f.write(content)
