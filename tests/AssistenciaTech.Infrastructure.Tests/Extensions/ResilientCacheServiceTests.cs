using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AssistenciaTech.Extensions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace AssistenciaTech.Infrastructure.Tests.Extensions
{
    // Important: The ResilientCacheService uses static variables for circuit breaking.
    // To avoid test pollution, we need to reset the static state before each test using Reflection.
    [Collection("Sequential")]
    public class ResilientCacheServiceTests : IDisposable
    {
        private readonly Mock<IDistributedCache> _cacheMock;
        private readonly Mock<ILogger<ResilientCacheService>> _loggerMock;
        private readonly ResilientCacheService _service;

        public ResilientCacheServiceTests()
        {
            _cacheMock = new Mock<IDistributedCache>();
            _loggerMock = new Mock<ILogger<ResilientCacheService>>();
            _service = new ResilientCacheService(_cacheMock.Object, _loggerMock.Object);
            ResetCircuitBreaker();
        }

        public void Dispose()
        {
            ResetCircuitBreaker();
        }

        private void ResetCircuitBreaker()
        {
            var fieldInfo = typeof(ResilientCacheService).GetField("_circuitOpenUntil",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (fieldInfo != null)
            {
                fieldInfo.SetValue(null, DateTime.MinValue);
            }
        }

        [Fact]
        public void IsAvailable_Initially_ShouldBeTrue()
        {
            // Assert
            _service.IsAvailable.Should().BeTrue();
        }

        [Fact]
        public async Task GetAsync_WhenAvailableAndKeyExists_ShouldReturnDeserializedData()
        {
            // Arrange
            var key = "test-key";
            var expectedData = new TestModel { Id = 1, Name = "Test" };
            var jsonData = JsonSerializer.Serialize(expectedData);

            _cacheMock.Setup(c => c.GetAsync(key, It.IsAny<CancellationToken>()))
                .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes(jsonData));

            // Act
            var result = await _service.GetAsync<TestModel>(key);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(expectedData.Id);
            result.Name.Should().Be(expectedData.Name);
        }

        [Fact]
        public async Task GetAsync_WhenAvailableAndKeyDoesNotExist_ShouldReturnDefault()
        {
            // Arrange
            var key = "non-existent-key";
            _cacheMock.Setup(c => c.GetAsync(key, It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[])null!);

            // Act
            var result = await _service.GetAsync<TestModel>(key);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetAsync_WhenExceptionThrown_ShouldOpenCircuitAndReturnDefault()
        {
            // Arrange
            var key = "test-key";
            var exception = new Exception("Redis connection failed");

            _cacheMock.Setup(c => c.GetAsync(key, It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);

            // Act
            var result = await _service.GetAsync<TestModel>(key);

            // Assert
            result.Should().BeNull();
            _service.IsAvailable.Should().BeFalse();

            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Circuit breaker ativado")),
                    exception,
                    It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
                Times.Once);
        }

        [Fact]
        public async Task GetAsync_WhenCircuitIsOpen_ShouldReturnDefaultImmediatelyWithoutCallingCache()
        {
            // Arrange
            // Force open circuit first
            _cacheMock.Setup(c => c.GetAsync("dummy", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Fail"));
            await _service.GetAsync<TestModel>("dummy");
            _cacheMock.Invocations.Clear();

            var key = "test-key";

            // Act
            var result = await _service.GetAsync<TestModel>(key);

            // Assert
            result.Should().BeNull();
            _cacheMock.Verify(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SetAsync_WhenAvailable_ShouldSerializeAndSetInCache()
        {
            // Arrange
            var key = "test-key";
            var data = new TestModel { Id = 1, Name = "Test" };
            var absoluteExpireTime = TimeSpan.FromMinutes(30);

            // Act
            await _service.SetAsync(key, data, absoluteExpireTime);

            // Assert
            _cacheMock.Verify(c => c.SetAsync(
                key,
                It.Is<byte[]>(b => JsonSerializer.Deserialize<TestModel>(System.Text.Encoding.UTF8.GetString(b), (JsonSerializerOptions?)null)!.Id == data.Id),
                It.Is<DistributedCacheEntryOptions>(o => o.AbsoluteExpirationRelativeToNow == absoluteExpireTime),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetAsync_WhenExceptionThrown_ShouldOpenCircuit()
        {
            // Arrange
            var key = "test-key";
            var data = new TestModel { Id = 1, Name = "Test" };

            _cacheMock.Setup(c => c.SetAsync(key, It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Redis connection failed"));

            // Act
            await _service.SetAsync(key, data);

            // Assert
            _service.IsAvailable.Should().BeFalse();
        }

        [Fact]
        public async Task SetAsync_WhenCircuitIsOpen_ShouldNotCallCache()
        {
            // Arrange
            // Force open circuit first
            _cacheMock.Setup(c => c.GetAsync("dummy", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Fail"));
            await _service.GetAsync<TestModel>("dummy");
            _cacheMock.Invocations.Clear();

            var key = "test-key";
            var data = new TestModel { Id = 1, Name = "Test" };

            // Act
            await _service.SetAsync(key, data);

            // Assert
            _cacheMock.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RemoveAsync_WhenAvailable_ShouldRemoveFromCache()
        {
            // Arrange
            var key = "test-key";

            // Act
            await _service.RemoveAsync(key);

            // Assert
            _cacheMock.Verify(c => c.RemoveAsync(key, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RemoveAsync_WhenExceptionThrown_ShouldOpenCircuit()
        {
            // Arrange
            var key = "test-key";

            _cacheMock.Setup(c => c.RemoveAsync(key, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Redis connection failed"));

            // Act
            await _service.RemoveAsync(key);

            // Assert
            _service.IsAvailable.Should().BeFalse();
        }

        [Fact]
        public async Task RemoveAsync_WhenCircuitIsOpen_ShouldNotCallCache()
        {
            // Arrange
            // Force open circuit first
            _cacheMock.Setup(c => c.GetAsync("dummy", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Fail"));
            await _service.GetAsync<TestModel>("dummy");
            _cacheMock.Invocations.Clear();

            var key = "test-key";

            // Act
            await _service.RemoveAsync(key);

            // Assert
            _cacheMock.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        private class TestModel
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }
    }
}
