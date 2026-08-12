using System;
using System.Threading.Tasks;
using AssistenciaTech.Controllers;
using AssistenciaTech.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace AssistenciaTech.Application.Tests.Controllers
{
    public class FaturamentosControllerTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly FaturamentosController _controller;

        public FaturamentosControllerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);
            _mockConfiguration = new Mock<IConfiguration>();
            _controller = new FaturamentosController(_context, _mockConfiguration.Object);
        }

        private void SetHttpContext(string? tokenHeaderValue = null)
        {
            var httpContext = new DefaultHttpContext();
            if (tokenHeaderValue != null)
            {
                httpContext.Request.Headers["X-Webhook-Token"] = tokenHeaderValue;
            }

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        [Fact]
        public async Task WebhookPix_Returns500_WhenWebhookSecretIsNotConfigured()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["WebhookSecret"]).Returns((string)null);
            SetHttpContext();

            // Act
            var result = await _controller.WebhookPix(new { });

            // Assert
            var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(500);
            objectResult.Value.Should().Be("Internal server error.");
        }

        [Fact]
        public async Task WebhookPix_Returns401_WhenWebhookTokenHeaderIsMissing()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["WebhookSecret"]).Returns("my-secret");
            SetHttpContext(); // Default context has no custom headers

            // Act
            var result = await _controller.WebhookPix(new { });

            // Assert
            var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorizedResult.Value.Should().Be("Invalid or missing webhook token.");
        }

        [Fact]
        public async Task WebhookPix_Returns401_WhenWebhookTokenIsInvalid()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["WebhookSecret"]).Returns("my-secret");
            SetHttpContext("invalid-secret");

            // Act
            var result = await _controller.WebhookPix(new { });

            // Assert
            var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
            unauthorizedResult.Value.Should().Be("Invalid or missing webhook token.");
        }

        [Fact]
        public async Task WebhookPix_ReturnsOk_WhenWebhookTokenIsValid()
        {
            // Arrange
            _mockConfiguration.Setup(c => c["WebhookSecret"]).Returns("my-secret");
            SetHttpContext("my-secret");

            // Act
            var result = await _controller.WebhookPix(new { });

            // Assert
            result.Should().BeOfType<OkResult>();
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _controller.Dispose();
        }
    }
}
