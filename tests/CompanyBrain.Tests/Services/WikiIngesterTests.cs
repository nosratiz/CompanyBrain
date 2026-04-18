using System.Net;
using CompanyBrain.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace CompanyBrain.Tests.Services;

public sealed class WikiIngesterTests
{
    private readonly WikiIngester _sut;
    private readonly MockHttpMessageHandler _mockHandler;
    private readonly HttpClient _httpClient;

    public WikiIngesterTests()
    {
        _mockHandler = new MockHttpMessageHandler();
        _httpClient = new HttpClient(_mockHandler);
        // Use NullLogger since logger type parameter includes internal WikiIngester type
        _sut = new WikiIngester(_httpClient, NullLogger<WikiIngester>.Instance);
    }

    #region IngestAsync Tests

    [Fact]
    public async Task IngestAsync_WithValidHtmlPage_ShouldReturnMarkdown()
    {
        // Arrange
        var html = """
            <html>
            <body>
                <main>
                    <h1>Test Title</h1>
                    <p>This is a test paragraph with enough content to be meaningful.</p>
                </main>
            </body>
            </html>
            """;
        _mockHandler.SetResponse(html);

        // Act
        var result = await _sut.IngestAsync(new Uri("https://example.com/page"), CancellationToken.None);

        // Assert
        result.Should().Contain("# Test Title");
        result.Should().Contain("test paragraph");
    }

    [Fact]
    public async Task IngestAsync_WithBoilerplate_ShouldRemoveScriptsAndStyles()
    {
        // Arrange
        var html = """
            <html>
            <head>
                <script>console.log('remove me');</script>
                <style>.remove { display: none; }</style>
            </head>
            <body>
                <nav>Navigation</nav>
                <header>Header</header>
                <main>
                    <h1>Real Content</h1>
                    <p>This paragraph contains the actual meaningful content of the page.</p>
                </main>
                <footer>Footer</footer>
            </body>
            </html>
            """;
        _mockHandler.SetResponse(html);

        // Act
        var result = await _sut.IngestAsync(new Uri("https://example.com"), CancellationToken.None);

        // Assert
        result.Should().NotContain("console.log");
        result.Should().NotContain(".remove");
        result.Should().Contain("Real Content");
    }

    [Fact]
    public async Task IngestAsync_WithEmptyMainContent_ShouldThrow()
    {
        // Arrange
        var html = """
            <html>
            <body>
                <main></main>
            </body>
            </html>
            """;
        _mockHandler.SetResponse(html);

        // Act
        var act = () => _sut.IngestAsync(new Uri("https://example.com"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No meaningful content*");
    }

    [Fact]
    public async Task IngestAsync_WithArticleElement_ShouldExtractContent()
    {
        // Arrange
        var html = """
            <html>
            <body>
                <article>
                    <h2>Article Heading</h2>
                    <p>Article content with enough text to be considered meaningful content.</p>
                </article>
            </body>
            </html>
            """;
        _mockHandler.SetResponse(html);

        // Act
        var result = await _sut.IngestAsync(new Uri("https://example.com"), CancellationToken.None);

        // Assert
        result.Should().Contain("## Article Heading");
        result.Should().Contain("Article content");
    }

    [Fact]
    public async Task IngestAsync_WithHttpError_ShouldThrow()
    {
        // Arrange
        _mockHandler.SetStatusCode(HttpStatusCode.NotFound);

        // Act
        var act = () => _sut.IngestAsync(new Uri("https://example.com/missing"), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task IngestAsync_WithLinks_ShouldConvertToMarkdown()
    {
        // Arrange
        var html = """
            <html>
            <body>
                <main>
                    <h1>Links Page</h1>
                    <p>Visit <a href="https://other.com/page">this link</a> for more info.</p>
                </main>
            </body>
            </html>
            """;
        _mockHandler.SetResponse(html);

        // Act
        var result = await _sut.IngestAsync(new Uri("https://example.com"), CancellationToken.None);

        // Assert
        result.Should().Contain("[this link](https://other.com/page)");
    }

    #endregion

    #region DiscoverLinksAsync Tests

    [Fact]
    public async Task DiscoverLinksAsync_WithValidLinks_ShouldReturnDiscoveredLinks()
    {
        // Arrange
        var html = """
            <html>
            <body>
                <a href="/page1">Page 1</a>
                <a href="/page2">Page 2</a>
                <a href="/page3">Page 3</a>
            </body>
            </html>
            """;
        _mockHandler.SetResponse(html);

        // Act
        var result = await _sut.DiscoverLinksAsync(
            new Uri("https://example.com"), 
            null, 
            CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);
        result.Select(l => l.Name).Should().Contain("Page 1");
        result.Select(l => l.Name).Should().Contain("Page 2");
    }

    [Fact]
    public async Task DiscoverLinksAsync_WithCustomSelector_ShouldUseSelector()
    {
        // Arrange
        var html = """
            <html>
            <body>
                <div class="sidebar">
                    <a href="/sidebar-link">Sidebar</a>
                </div>
                <div class="content">
                    <a href="/content-link">Content Link</a>
                </div>
            </body>
            </html>
            """;
        _mockHandler.SetResponse(html);

        // Act
        var result = await _sut.DiscoverLinksAsync(
            new Uri("https://example.com"), 
            "//div[@class='content']//a", 
            CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("Content Link");
    }

    [Fact]
    public async Task DiscoverLinksAsync_ShouldFilterNonContentLinks()
    {
        // Arrange
        var html = """
            <html>
            <body>
                <a href="/page.html">Valid Page</a>
                <a href="/style.css">Stylesheet</a>
                <a href="/script.js">Script</a>
                <a href="/image.png">Image</a>
                <a href="/login">Login</a>
            </body>
            </html>
            """;
        _mockHandler.SetResponse(html);

        // Act
        var result = await _sut.DiscoverLinksAsync(
            new Uri("https://example.com"), 
            null, 
            CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result.First().Url.Should().Contain("page.html");
    }

    [Fact]
    public async Task DiscoverLinksAsync_ShouldDeduplicateLinks()
    {
        // Arrange
        var html = """
            <html>
            <body>
                <a href="/same-page">Link 1</a>
                <a href="/same-page">Link 2</a>
                <a href="/same-page/">Link 3</a>
            </body>
            </html>
            """;
        _mockHandler.SetResponse(html);

        // Act
        var result = await _sut.DiscoverLinksAsync(
            new Uri("https://example.com"), 
            null, 
            CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task DiscoverLinksAsync_WithNoLinks_ShouldReturnEmpty()
    {
        // Arrange
        var html = "<html><body><p>No links here</p></body></html>";
        _mockHandler.SetResponse(html);

        // Act
        var result = await _sut.DiscoverLinksAsync(
            new Uri("https://example.com"), 
            null, 
            CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DiscoverLinksAsync_ShouldResolveRelativeUrls()
    {
        // Arrange
        var html = """
            <html>
            <body>
                <a href="../other/page">Relative Link</a>
            </body>
            </html>
            """;
        _mockHandler.SetResponse(html);

        // Act
        var result = await _sut.DiscoverLinksAsync(
            new Uri("https://example.com/docs/api/"), 
            null, 
            CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result.First().Url.Should().Be("https://example.com/docs/other/page");
    }

    #endregion

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private string _responseContent = "";
        private HttpStatusCode _statusCode = HttpStatusCode.OK;

        public void SetResponse(string content)
        {
            _responseContent = content;
            _statusCode = HttpStatusCode.OK;
        }

        public void SetStatusCode(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, 
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseContent)
            };
            return Task.FromResult(response);
        }
    }
}
