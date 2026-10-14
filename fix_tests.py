with open('tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'r') as f:
    lines = f.readlines()

content = "".join(lines)

# Fix Index_Get_ReturnsViewWithDashboardData_WhenSuccessful
content = content.replace(
"""            var result = await _controller.Index(searchString, statusFilter, page) as ViewResult;

            // Assert
            result.Should().NotBeNull();
            var model = result.Model.Should().BeAssignableTo<IEnumerable<OrdemServico>>().Subject;
            model.Should().BeEquivalentTo(ordens);

            var viewData = result.ViewData;
            viewData["ErroBanco"].Should().Be("Test error message");
            viewData["SearchString"].Should().Be(searchString);
            viewData["StatusFilter"].Should().Be(statusFilter);
            viewData["ChartLabels"].Should().BeEquivalentTo(dashboardData.ChartLabels);
            viewData["ChartData"].Should().BeEquivalentTo(dashboardData.ChartData);

            viewData["TotalAbertas"].Should().Be(5);
            viewData["EquipamentosProntos"].Should().Be(3);
            viewData["FaturamentoPrevisto"].Should().Be(1500.50m);
            viewData["CurrentPage"].Should().Be(2);
            viewData["TotalPages"].Should().Be(3);
            viewData["TotalOrdens"].Should().Be(25);""",
"""            var result = await _controller.Index(searchString, statusFilter, page) as ViewResult;

            // Assert
            result.Should().NotBeNull();
            var model = result.Model.Should().BeAssignableTo<AdminDashboardViewModel>().Subject;
            model.DashboardData.Ordens.Should().BeEquivalentTo(ordens);

            model.ErroBanco.Should().Be("Test error message");
            model.SearchString.Should().Be(searchString);
            model.StatusFilter.Should().Be(statusFilter);
            model.DashboardData.ChartLabels.Should().BeEquivalentTo(dashboardData.ChartLabels);
            model.DashboardData.ChartData.Should().BeEquivalentTo(dashboardData.ChartData);

            model.DashboardData.TotalAbertas.Should().Be(5);
            model.DashboardData.EquipamentosProntos.Should().Be(3);
            model.DashboardData.FaturamentoPrevisto.Should().Be(1500.50m);
            model.DashboardData.CurrentPage.Should().Be(2);
            model.DashboardData.TotalPages.Should().Be(3);
            model.DashboardData.TotalOrdens.Should().Be(25);"""
)

# Fix Index_Get_ReturnsEmptyList_WhenDatabaseFails
content = content.replace(
"""            var result = await _controller.Index(null, null, 1) as ViewResult;

            // Assert
            result.Should().NotBeNull();
            result.Model.Should().BeEquivalentTo(new List<OrdemServico>());""",
"""            var result = await _controller.Index(null, null, 1) as ViewResult;

            // Assert
            result.Should().NotBeNull();
            var model = result.Model.Should().BeAssignableTo<AdminDashboardViewModel>().Subject;
            model.DashboardData.Ordens.Should().BeEquivalentTo(new List<OrdemServico>());"""
)

content = content.replace(
"""            var viewData = result.ViewData;
            viewData["ErroBanco"].Should().Be("Erro ao conectar ao banco de dados. Por favor, tente novamente mais tarde.");
            viewData["TotalAbertas"].Should().Be(0);
            viewData["EquipamentosProntos"].Should().Be(0);
            viewData["FaturamentoPrevisto"].Should().Be(0m);""",
"""            model.ErroBanco.Should().Be("Erro ao conectar ao banco de dados. Por favor, tente novamente mais tarde.");
            model.DashboardData.TotalAbertas.Should().Be(0);
            model.DashboardData.EquipamentosProntos.Should().Be(0);
            model.DashboardData.FaturamentoPrevisto.Should().Be(0m);"""
)

with open('tests/AssistenciaTech.Application.Tests/Controllers/AdminControllerTests.cs', 'w') as f:
    f.write(content)
