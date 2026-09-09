import re

file_path = 'tests/AssistenciaTech.Application.Tests/Controllers/MobileApiControllerTests.cs'
with open(file_path, 'r') as f:
    content = f.read()

tests_to_add = """
        [Fact]
        public async Task SyncVisitas_ReturnsUnauthorized_WhenUserIsNotAuthenticated()
        {
            // Arrange
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };

            var dtoList = new System.Collections.Generic.List<MobileApiController.VisitaCampoSyncDto>();

            // Act
            var result = await _controller.SyncVisitas(dtoList);

            // Assert
            var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorizedResult.Value.Should().BeEquivalentTo(new { error = "Técnico não autenticado." });
        }

        [Fact]
        public async Task SyncVisitas_ReturnsBadRequest_WhenDtoListIsNull()
        {
            // Arrange
            SetUserContext(10);

            // Act
            var result = await _controller.SyncVisitas(null!);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().BeEquivalentTo(new { error = "Nenhum dado para sincronizar." });
        }

        [Fact]
        public async Task SyncVisitas_ReturnsBadRequest_WhenDtoListIsEmpty()
        {
            // Arrange
            SetUserContext(10);
            var dtoList = new System.Collections.Generic.List<MobileApiController.VisitaCampoSyncDto>();

            // Act
            var result = await _controller.SyncVisitas(dtoList);

            // Assert
            var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
            badRequestResult.Value.Should().BeEquivalentTo(new { error = "Nenhum dado para sincronizar." });
        }

        [Fact]
        public async Task SyncVisitas_ReturnsOkAndSavesData_WhenProvidedValidData()
        {
            // Arrange
            SetUserContext(10);
            var dtoList = new System.Collections.Generic.List<MobileApiController.VisitaCampoSyncDto>
            {
                new MobileApiController.VisitaCampoSyncDto
                {
                    OrdemServicoId = 100,
                    CheckIn = new DateTime(2023, 10, 1, 10, 0, 0, DateTimeKind.Utc),
                    CheckOut = new DateTime(2023, 10, 1, 12, 0, 0, DateTimeKind.Utc),
                    Latitude = -23.5505m,
                    Longitude = -46.6333m,
                    AssinaturaClienteBase64 = "base64string1"
                },
                new MobileApiController.VisitaCampoSyncDto
                {
                    OrdemServicoId = 101,
                    CheckIn = new DateTime(2023, 10, 2, 14, 0, 0, DateTimeKind.Utc),
                    Latitude = -22.9068m,
                    Longitude = -43.1729m
                }
            };

            // Act
            var result = await _controller.SyncVisitas(dtoList);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            okResult.Value.Should().BeEquivalentTo(new { status = "success", message = "2 visitas sincronizadas com sucesso." });

            var visitas = await _context.VisitasCampo.ToListAsync();
            visitas.Should().HaveCount(2);

            var visita1 = visitas.First(v => v.OrdemServicoId == 100);
            visita1.TecnicoId.Should().Be(10);
            visita1.CheckIn.Should().Be(new DateTime(2023, 10, 1, 10, 0, 0, DateTimeKind.Utc));
            visita1.CheckOut.Should().Be(new DateTime(2023, 10, 1, 12, 0, 0, DateTimeKind.Utc));
            visita1.Latitude.Should().Be(-23.5505m);
            visita1.Longitude.Should().Be(-46.6333m);
            visita1.AssinaturaClienteBase64.Should().Be("base64string1");

            var visita2 = visitas.First(v => v.OrdemServicoId == 101);
            visita2.TecnicoId.Should().Be(10);
            visita2.CheckIn.Should().Be(new DateTime(2023, 10, 2, 14, 0, 0, DateTimeKind.Utc));
            visita2.CheckOut.Should().BeNull();
            visita2.Latitude.Should().Be(-22.9068m);
            visita2.Longitude.Should().Be(-43.1729m);
            visita2.AssinaturaClienteBase64.Should().BeNull();
        }
"""

content = re.sub(r'(\s+public void Dispose\(\))', tests_to_add + r'\1', content)

with open(file_path, 'w') as f:
    f.write(content)

print("Tests added.")
