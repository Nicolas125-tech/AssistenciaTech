#!/bin/bash
# Re-running the targeted test to ensure my changes are covered

rm -f tests/AssistenciaTech.Application.Tests/Services/PdfGeneratorServiceTests.cs tests/AssistenciaTech.Application.Tests/Controllers/FaturamentosControllerTests.cs

dotnet test AssistenciaTech.sln --filter FaturamentosControllerTests
