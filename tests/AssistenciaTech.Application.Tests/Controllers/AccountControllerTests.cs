using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using AssistenciaTech.Controllers;
using AssistenciaTech.Data;
using AssistenciaTech.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace AssistenciaTech.Application.Tests.Controllers
{
    public class AccountControllerTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly AccountController _controller;
        private readonly Mock<IAuthenticationService> _mockAuthService;

        public AccountControllerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);
            _mockConfig = new Mock<IConfiguration>();

            var mockHttpContext = new Mock<HttpContext>();
            _mockAuthService = new Mock<IAuthenticationService>();

            _mockAuthService
                .Setup(x => x.SignInAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()))
                .Returns(Task.CompletedTask);

            _mockAuthService
                .Setup(x => x.SignOutAsync(It.IsAny<HttpContext>(), It.IsAny<string>(), It.IsAny<AuthenticationProperties>()))
                .Returns(Task.CompletedTask);

            mockHttpContext.Setup(c => c.RequestServices.GetService(typeof(IAuthenticationService)))
                .Returns(_mockAuthService.Object);


            var mockUrlHelper = new Mock<IUrlHelper>();
            mockUrlHelper.Setup(x => x.IsLocalUrl(It.IsAny<string>())).Returns(false);

            _controller = new AccountController(_mockConfig.Object, _context)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = mockHttpContext.Object
                },
                Url = mockUrlHelper.Object,
                TempData = new TempDataDictionary(mockHttpContext.Object, new Mock<ITempDataProvider>().Object)
            };
        }

        [Fact]
        public async Task Login_ReturnsRedirectToActionResult_WhenCredentialsAreValid()
        {
            // Arrange
            var hasher = new PasswordHasher<Usuario>();
            var user = new Usuario
            {
                Username = "admin",
                Role = "Administrador"
            };
            user.PasswordHash = hasher.HashPassword(user, "senha123");

            _context.Usuarios.Add(user);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            // Act
            var result = await _controller.Login("admin", "senha123");

            // Assert
            var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectResult.ActionName.Should().Be("Index");
            redirectResult.ControllerName.Should().Be("Admin");
        }

        [Fact]
        public async Task Login_ReturnsViewResultWithError_WhenCredentialsAreInvalid()
        {
            // Arrange
            var hasher = new PasswordHasher<Usuario>();
            var user = new Usuario
            {
                Username = "admin",
                Role = "Administrador"
            };
            user.PasswordHash = hasher.HashPassword(user, "senha123");

            _context.Usuarios.Add(user);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            // Act
            var result = await _controller.Login("admin", "senhaErrada");

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            ((string)_controller.ViewBag.Error).Should().Be("Usuário ou senha incorretos. Acesso negado.");
        }

        [Fact]
        public async Task Logout_CallsSignOutAsyncAndRedirectsToHomeIndex()
        {
            // Act
            var result = await _controller.Logout();

            // Assert
            _mockAuthService.Verify(x => x.SignOutAsync(It.IsAny<HttpContext>(), CookieAuthenticationDefaults.AuthenticationScheme, It.IsAny<AuthenticationProperties>()), Times.Once);
            var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectResult.ActionName.Should().Be("Index");
            redirectResult.ControllerName.Should().Be("Home");
        }

        [Fact]
        public async Task Login_ReturnsRedirectResult_WhenCredentialsAreValidAndReturnUrlIsLocal()
        {
            // Arrange
            var hasher = new PasswordHasher<Usuario>();
            var user = new Usuario
            {
                Username = "admin",
                Role = "Administrador"
            };
            user.PasswordHash = hasher.HashPassword(user, "senha123");

            _context.Usuarios.Add(user);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            var returnUrl = "/Admin/Create";
            var mockUrlHelper = Mock.Get(_controller.Url);
            mockUrlHelper.Setup(x => x.IsLocalUrl(returnUrl)).Returns(true);

            // Act
            var result = await _controller.Login("admin", "senha123", returnUrl);

            // Assert
            var redirectResult = result.Should().BeOfType<RedirectResult>().Subject;
            redirectResult.Url.Should().Be(returnUrl);
        }

        [Fact]
        public async Task Login_ReturnsRedirectToActionResult_WhenCredentialsAreValidAndReturnUrlIsNotLocal()
        {
            // Arrange
            var hasher = new PasswordHasher<Usuario>();
            var user = new Usuario
            {
                Username = "admin",
                Role = "Administrador"
            };
            user.PasswordHash = hasher.HashPassword(user, "senha123");

            _context.Usuarios.Add(user);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            var returnUrl = "http://malicious-site.com";
            var mockUrlHelper = Mock.Get(_controller.Url);
            mockUrlHelper.Setup(x => x.IsLocalUrl(returnUrl)).Returns(false);

            // Act
            var result = await _controller.Login("admin", "senha123", returnUrl);

            // Assert
            var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectResult.ActionName.Should().Be("Index");
            redirectResult.ControllerName.Should().Be("Admin");
        }


        [Fact]
        public async Task SetupAdmin_ReturnsRedirectToActionResult_WhenCpfIsValid()
        {
            // Act
            var result = await _controller.SetupAdmin("12161408984", "newadmin", "securepass");

            // Assert
            var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
            redirectResult.ActionName.Should().Be("Login");

            // Verify DB changes
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Username == "newadmin");
            user.Should().NotBeNull();
            user.Role.Should().Be("Administrador");

            var tecnico = await _context.Tecnicos.FirstOrDefaultAsync(t => t.Nome == "newadmin");
            tecnico.Should().NotBeNull();
        }

        [Fact]
        public async Task SetupAdmin_ReturnsViewResultWithError_WhenCpfIsInvalid()
        {
            // Act
            var result = await _controller.SetupAdmin("00000000000", "badadmin", "securepass");

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            ((string)_controller.ViewBag.Error).Should().Be("CPF inválido ou não autorizado para criação de administrador.");

            // Verify no DB changes
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Username == "badadmin");
            user.Should().BeNull();
        }

        [Fact]
        public async Task SetupAdmin_ReturnsViewResultWithError_WhenUsernameAlreadyExists()
        {
            // Arrange
            var hasher = new PasswordHasher<Usuario>();
            var user = new Usuario
            {
                Username = "newadmin",
                Role = "Administrador"
            };
            user.PasswordHash = hasher.HashPassword(user, "senha123");

            _context.Usuarios.Add(user);
            await _context.SaveChangesAsync();
            _context.ChangeTracker.Clear();

            // Act
            var result = await _controller.SetupAdmin("12161408984", "newadmin", "securepass");

            // Assert
            var viewResult = result.Should().BeOfType<ViewResult>().Subject;
            ((string)_controller.ViewBag.Error).Should().Be("Nome de usuário já existe.");
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _controller.Dispose();
        }
    }
}
