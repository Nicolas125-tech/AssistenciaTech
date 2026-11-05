using System;
using System.Threading;
using System.Threading.Tasks;
using AssistenciaTech.Data;
using AssistenciaTech.Extensions;
using AssistenciaTech.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using Xunit;

namespace AssistenciaTech.Application.Tests.Services
{
    public class TechOsHealthCheckTests
    {
        [Fact]
        public async Task CheckHealthAsync_WhenDatabaseAndCacheHealthy_ShouldReturnHealthy()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            await using var context = new AppDbContext(options);
            var cacheMock = new Mock<IResilientCacheService>();
            cacheMock.Setup(c => c.GetAsync<string>("__healthcheck_probe__"))
                .ReturnsAsync((string?)null);

            var healthCheck = new TechOsHealthCheck(context, cacheMock.Object);

            var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

            result.Status.Should().Be(HealthStatus.Healthy);
            result.Data.Should().ContainKey("database").WhoseValue.Should().Be("up");
            result.Data.Should().ContainKey("cache").WhoseValue.Should().Be("up");
        }

        [Fact]
        public async Task CheckHealthAsync_WhenCacheThrows_ShouldReturnDegraded()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            await using var context = new AppDbContext(options);
            var cacheMock = new Mock<IResilientCacheService>();
            cacheMock.Setup(c => c.GetAsync<string>("__healthcheck_probe__"))
                .ThrowsAsync(new InvalidOperationException("Redis timeout"));

            var healthCheck = new TechOsHealthCheck(context, cacheMock.Object);

            var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

            result.Status.Should().Be(HealthStatus.Degraded);
            result.Data.Should().ContainKey("cache").WhoseValue.Should().Be("degraded");
        }
    }
}
