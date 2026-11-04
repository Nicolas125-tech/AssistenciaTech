import re

with open('tests/AssistenciaTech.Application.Tests/Controllers/PecasControllerTests.cs', 'r') as f:
    content = f.read()

# We need to explicitly open the connection when using sqlite in memory
new_ctor = """        public PecasControllerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite("DataSource=:memory:;Foreign Keys=False")
                .Options;

            _context = new AppDbContext(options);
            _context.Database.OpenConnection();
            _context.Database.EnsureCreated();
            _controller = new PecasController(_context);
        }"""

content = re.sub(
    r'public PecasControllerTests\(\).*?_controller = new PecasController\(_context\);\s*\}',
    new_ctor,
    content,
    flags=re.DOTALL
)

with open('tests/AssistenciaTech.Application.Tests/Controllers/PecasControllerTests.cs', 'w') as f:
    f.write(content)
