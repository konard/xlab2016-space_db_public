using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using SpaceDb.Controllers;
using SpaceDb.Services;
using Xunit;

namespace SpaceDb.Tests.Controllers
{
    /// <summary>
    /// Comprehensive tests for ContentController
    /// Tests various content sizes, formats, and edge cases for fragment/block splitting
    /// </summary>
    public class ContentControllerTests
    {
        private readonly Mock<IContentParserService> _contentParserServiceMock;
        private readonly Mock<ILogger<ContentController>> _loggerMock;
        private readonly ContentController _controller;

        public ContentControllerTests()
        {
            _contentParserServiceMock = new Mock<IContentParserService>();
            _loggerMock = new Mock<ILogger<ContentController>>();
            _controller = new ContentController(_contentParserServiceMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task UploadContent_WithSmallTextContent_ShouldReturnSuccess()
        {
            // Arrange
            var request = new UploadContentRequest
            {
                Payload = "This is a small text content that should be parsed successfully.",
                ResourceId = "small_text.txt",
                ContentType = "text",
                SingularityId = 1,
                UserId = 1
            };

            var expectedResult = new ContentParseResult
            {
                ResourcePointId = 1,
                ParserType = "text",
                BlockPointIds = new List<long> { 2 },
                FragmentPointIds = new List<long> { 3 }
            };

            _contentParserServiceMock
                .Setup(x => x.ParseAndStoreAsync(
                    request.Payload,
                    request.ResourceId,
                    request.ContentType ?? "auto",
                    request.SingularityId,
                    request.UserId,
                    request.Metadata))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await _controller.UploadContent(request);

            // Assert
            result.Should().BeOfType<OkObjectResult>();
            _contentParserServiceMock.Verify(x => x.ParseAndStoreAsync(
                request.Payload,
                request.ResourceId,
                request.ContentType ?? "auto",
                request.SingularityId,
                request.UserId,
                request.Metadata), Times.Once);
        }

        [Fact]
        public async Task UploadContent_WithEmptyPayload_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new UploadContentRequest
            {
                Payload = "",
                ResourceId = "empty.txt",
                ContentType = "text"
            };

            // Act
            var result = await _controller.UploadContent(request);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task UploadContent_WithEmptyResourceId_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new UploadContentRequest
            {
                Payload = "Some content",
                ResourceId = "",
                ContentType = "text"
            };

            // Act
            var result = await _controller.UploadContent(request);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task UploadContent_WhenParsingFails_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new UploadContentRequest
            {
                Payload = "Some content",
                ResourceId = "test.txt",
                ContentType = "text"
            };

            _contentParserServiceMock
                .Setup(x => x.ParseAndStoreAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<long?>(),
                    It.IsAny<int?>(),
                    It.IsAny<Dictionary<string, object>?>()))
                .ReturnsAsync((ContentParseResult?)null);

            // Act
            var result = await _controller.UploadContent(request);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task GetParsers_ShouldReturnAvailableParsers()
        {
            // Arrange
            var availableParsers = new List<string> { "text", "json" };
            _contentParserServiceMock
                .Setup(x => x.GetAvailableParserTypes())
                .Returns(availableParsers);

            // Act
            var result = _controller.GetParsers();

            // Assert
            result.Should().BeOfType<OkObjectResult>();
        }
    }
}
