import re

with open('./tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'r') as f:
    content = f.read()

# I need to mock DbConnection so it throws on Open() when the EF core tries to run ToList.
# Wait! In EF Core, if we use InMemory database, it doesn't use DbConnection. It uses an in-memory provider.
# My mock mockConnection.Setup(c => c.Open()).Throws(...) with UseSqlite should work but maybe I forgot to add options builder UseSqlite?
# Actually, the sqlite might just not open the connection if the query is deferred, but `ToList()` should execute it.
# Wait, why didn't it throw? Let's check PopulateClientesViewBag.
