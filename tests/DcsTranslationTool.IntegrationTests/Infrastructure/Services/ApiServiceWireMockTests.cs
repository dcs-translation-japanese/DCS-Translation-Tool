using System.Net;
using System.Text;
using System.Text.Json;

using DcsTranslationTool.Application.Contracts;
using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Infrastructure.Services;
using DcsTranslationTool.IntegrationTests.Infrastructure.Services.WireMock;
using DcsTranslationTool.Shared.Models;

using Moq;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace DcsTranslationTool.IntegrationTests.Infrastructure.Services;

/// <summary>
/// <see cref="ApiService"/> の HTTP 契約を WireMock で検証する。
/// </summary>
public sealed class ApiServiceWireMockTests {
    /// <summary>
    /// health エンドポイントを実 HTTP で正しく解釈することを検証する。
    /// </summary>
    [Fact]
    public async Task GetHealthAsyncはWireMockレスポンスを正しく解釈する() {
        using var server = new WireMockTestServer();
        var timestamp = DateTimeOffset.Parse( "2026-01-01T00:00:00Z" );
        server.Server
            .Given( Request.Create().WithPath( "/v2/health" ).UsingGet() )
            .RespondWith(
                Response.Create()
                    .WithStatusCode( (int)HttpStatusCode.OK )
                    .WithHeader( "Content-Type", "application/json; charset=utf-8" )
                    .WithBody(
                        $$"""
                        {
                          "status": "ok",
                          "timestamp": "{{timestamp:O}}"
                        }
                        """ ) );

        var sut = CreateSut( server.CreateHttpClient() );

        var result = await sut.GetHealthAsync( TestContext.Current.CancellationToken );

        Assert.True( result.IsSuccess );
        Assert.Equal( ApiHealthStatus.Ok, result.Value.Status );
        Assert.Equal( timestamp, result.Value.Timestamp );

        var request = RequireSingleRequest( server, "/v2/health" );
        Assert.Equal( "GET", request.Method );
        Assert.Equal( "/v2/health", request.Path );
    }

    /// <summary>
    /// tree エンドポイントの JSON を実 HTTP で正しくマッピングすることを検証する。
    /// </summary>
    [Fact]
    public async Task GetTreeAsyncはWireMockレスポンスを正しくマッピングする() {
        using var server = new WireMockTestServer();
        server.Server
            .Given( Request.Create().WithPath( "/v2/tree" ).UsingGet() )
            .RespondWith(
                Response.Create()
                    .WithStatusCode( (int)HttpStatusCode.OK )
                    .WithHeader( "Content-Type", "application/json; charset=utf-8" )
                    .WithBody(
                        """
                        {
                          "page": { "nextCursor": null },
                          "items": [
                            {
                              "mode": "100644",
                              "path": "translations/file.po",
                              "sha": "file-sha",
                              "type": "blob"
                            }
                          ]
                        }
                        """ ) );

        var settings = new AppSettings
        {
            ApiPreferredRoute = ApiRoutePreference.Default,
            ApiPreferredRouteValidUntilUtc = DateTimeOffset.UtcNow.AddMinutes( 30 ),
        };
        var sut = CreateSut( server.CreateHttpClient(), settings: settings );

        var result = await sut.GetTreeAsync( TestContext.Current.CancellationToken );

        Assert.True( result.IsSuccess );
        Assert.Collection(
            result.Value,
            file => {
                Assert.Equal( "file.po", file.Name );
                Assert.Equal( "translations/file.po", file.Path );
                Assert.False( file.IsDirectory );
                Assert.Equal( "file-sha", file.RepoSha );
            } );

        var request = RequireSingleRequest( server, "/v2/tree" );
        Assert.Equal( "GET", request.Method );
        Assert.Equal( "/v2/tree", request.Path );
    }

    /// <summary>
    /// 優先経路失敗時にフォールバック経路へ再試行し優先経路を更新することを検証する。
    /// </summary>
    [Fact]
    public async Task GetTreeAsyncはWireMockでフォールバック経路へ再試行する() {
        using var defaultServer = new WireMockTestServer();
        using var ipv4Server = new WireMockTestServer();
        using var ipv6Server = new WireMockTestServer();

        ipv4Server.Server
            .Given( Request.Create().WithPath( "/v2/tree" ).UsingGet() )
            .RespondWith(
                Response.Create()
                    .WithStatusCode( (int)HttpStatusCode.InternalServerError )
                    .WithBody( "ipv4 failed" ) );

        defaultServer.Server
            .Given( Request.Create().WithPath( "/v2/tree" ).UsingGet() )
            .RespondWith(
                Response.Create()
                    .WithStatusCode( (int)HttpStatusCode.OK )
                    .WithHeader( "Content-Type", "application/json; charset=utf-8" )
                    .WithBody(
                        """
                        {
                          "page": { "nextCursor": null },
                          "items": [
                            {
                              "mode": "100644",
                              "path": "fallback/path.lua",
                              "sha": "fallback-sha",
                              "type": "blob"
                            }
                          ]
                        }
                        """ ) );

        var settings = new AppSettings
        {
            ApiPreferredRoute = ApiRoutePreference.Ipv4,
            ApiPreferredRouteValidUntilUtc = DateTimeOffset.UtcNow.AddMinutes( 30 ),
        };
        var sut = CreateSut(
            defaultServer.CreateHttpClient(),
            ipv4Server.CreateHttpClient(),
            ipv6Server.CreateHttpClient(),
            settings );

        var result = await sut.GetTreeAsync( TestContext.Current.CancellationToken );

        Assert.True( result.IsSuccess );
        Assert.Equal( "fallback/path.lua", result.Value.Single().Path );
        Assert.Equal( ApiRoutePreference.Default, settings.ApiPreferredRoute );

        var defaultRequest = RequireSingleRequest( defaultServer, "/v2/tree" );
        Assert.Equal( "/v2/tree", defaultRequest.Path );
        var ipv4Request = RequireSingleRequest( ipv4Server, "/v2/tree" );
        Assert.Equal( "/v2/tree", ipv4Request.Path );
        Assert.DoesNotContain( ipv6Server.Server.LogEntries, entry => string.Equals( entry.RequestMessage?.Path, "/v2/tree", StringComparison.Ordinal ) );
    }

    /// <summary>
    /// download-file-paths の HTTP 要求とレスポンス解釈を検証する。
    /// </summary>
    [Fact]
    public async Task DownloadFilePathsAsyncはWireMockでHTTP契約を満たす() {
        using var server = new WireMockTestServer();
        server.Server
            .Given( Request.Create().WithPath( "/v2/files" ).UsingGet() )
            .RespondWith(
                Response.Create()
                    .WithStatusCode( (int)HttpStatusCode.OK )
                    .WithHeader( "Content-Type", "application/json; charset=utf-8" )
                    .WithHeader( "ETag", "\"header-etag\"" )
                    .WithBody(
                        """
                        {
                          "files": [
                            { "url": "https://example.test/raw/file1", "path": "path/one" },
                            { "url": "https://example.test/raw/file2", "path": "path/two" }
                          ],
                          "etag": "\"body-etag\""
                        }
                        """ ) );

        var sut = CreateSut( server.CreateHttpClient() );

        var result = await sut.DownloadFilePathsAsync(
            new ApiDownloadFilePathsRequest( [" フォルダ/a +&#?,%.lua ", "path/two"], "\"old-etag\"" ),
            TestContext.Current.CancellationToken );

        Assert.True( result.IsSuccess );
        Assert.Null( result.Value.ETag );
        Assert.Collection(
            result.Value.Items,
            first => {
                Assert.Equal( "https://example.test/raw/file1", first.Url );
                Assert.Equal( "path/one", first.Path );
            },
            second => {
                Assert.Equal( "https://example.test/raw/file2", second.Url );
                Assert.Equal( "path/two", second.Path );
            } );

        var request = RequireSingleRequest( server, "/v2/files" );
        Assert.Equal( "GET", request.Method );
        Assert.Equal( "/v2/files", request.Path );
        Assert.Contains( request.AcceptHeaders, value => value.Contains( "application/json", StringComparison.Ordinal ) );
        Assert.Contains( request.AcceptHeaders, value => value.Contains( "application/problem+json", StringComparison.Ordinal ) );
        Assert.Empty( request.Body );
        Assert.False( request.Headers.ContainsKey( "If-None-Match" ) );
        var query = server.Server.LogEntries.Single( entry => entry.RequestMessage?.Path == "/v2/files" ).RequestMessage!.Query!;
        Assert.Equal( ["フォルダ/a +&#?,%.lua", "path/two"], query["path"] );
    }

    /// <summary>
    /// v2 で未定義の 304 応答を成功として扱わないことを検証する。
    /// </summary>
    [Fact]
    public async Task DownloadFilePathsAsyncはWireMockで304を失敗として扱う() {
        using var server = new WireMockTestServer();
        server.Server
            .Given( Request.Create().WithPath( "/v2/files" ).UsingGet() )
            .RespondWith(
                Response.Create()
                    .WithStatusCode( (int)HttpStatusCode.NotModified )
                    .WithHeader( "ETag", "\"etag-value\"" ) );

        var sut = CreateSut( server.CreateHttpClient() );

        var result = await sut.DownloadFilePathsAsync(
            new ApiDownloadFilePathsRequest( [" path/one ", "path/two"], "\"etag-value\"" ),
            TestContext.Current.CancellationToken );

        Assert.True( result.IsFailed );

        var request = RequireSingleRequest( server, "/v2/files" );
        Assert.False( request.Headers.ContainsKey( "If-None-Match" ) );
    }

    /// <summary>
    /// create-pr の送信 JSON とレスポンス変換を検証する。
    /// </summary>
    [Fact]
    public async Task CreatePullRequestAsyncはWireMockでHTTP契約を満たす() {
        using var server = new WireMockTestServer();
        server.Server
            .Given( Request.Create().WithPath( "/v2/prs" ).UsingPost() )
            .RespondWith(
                Response.Create()
                    .WithStatusCode( (int)HttpStatusCode.OK )
                    .WithHeader( "Content-Type", "application/json; charset=utf-8" )
                    .WithBody(
                        """
                        {
                          "branchName": "feature/test",
                          "commitSha": "abc123",
                          "number": 42,
                          "url": "https://example.test/pr/42",
                          "reused": false
                        }
                        """ ) );

        var sut = CreateSut( server.CreateHttpClient() );
        var request = new ApiCreatePullRequestRequest(
            "feature/test",
            "feat: add files",
            "Add files",
            "Body",
            [
                new ApiPullRequestFile( ApiPullRequestFileOperation.Upsert, "content/upsert.txt", "追加と更新\n本文" ),
                new ApiPullRequestFile( ApiPullRequestFileOperation.Delete, "content/remove.txt", null ),
                new ApiPullRequestFile( ApiPullRequestFileOperation.Upsert, "   ", "ignored" ),
            ] );

        var result = await sut.CreatePullRequestAsync( request, TestContext.Current.CancellationToken );

        Assert.True( result.IsSuccess );
        Assert.True( result.Value.Success );
        Assert.Null( result.Value.Message );
        Assert.Single( result.Value.Entries );
        Assert.Equal( 42, result.Value.Entries[0].PullRequestNumber );
        Assert.Equal( new Uri( "https://example.test/pr/42" ), result.Value.Entries[0].PullRequestUrl );

        var received = RequireSingleRequest( server, "/v2/prs" );
        Assert.Equal( "POST", received.Method );
        Assert.Equal( "/v2/prs", received.Path );

        using var body = JsonDocument.Parse( received.Body );
        var root = body.RootElement;
        Assert.Equal( "feature/test", root.GetProperty( "branchName" ).GetString() );
        Assert.Equal( "feat: add files", root.GetProperty( "commitMessage" ).GetString() );
        Assert.Equal( "Add files", root.GetProperty( "prTitle" ).GetString() );
        Assert.Equal( "Body", root.GetProperty( "prBody" ).GetString() );

        var files = root.GetProperty( "files" ).EnumerateArray().ToArray();
        Assert.Equal( 2, files.Length );
        Assert.Equal( "upsert", files[0].GetProperty( "operation" ).GetString() );
        Assert.Equal( "content/upsert.txt", files[0].GetProperty( "path" ).GetString() );
        Assert.Equal( "追加と更新\n本文", files[0].GetProperty( "content" ).GetString() );
        Assert.Equal( "delete", files[1].GetProperty( "operation" ).GetString() );
        Assert.Equal( "content/remove.txt", files[1].GetProperty( "path" ).GetString() );
        Assert.False( files[1].TryGetProperty( "content", out _ ) );
    }

    [Fact]
    public async Task GetTreeAsyncはWireMockで複数ページとカーソルのエンコードを検証する() {
        using var server = new WireMockTestServer();
        const string cursor = "+/=& 日本語";
        server.Server
            .Given( Request.Create().WithPath( "/v2/tree" ).UsingGet() )
            .AtPriority( 2 )
            .RespondWith( Response.Create().WithStatusCode( 200 )
                .WithHeader( "Content-Type", "application/json" )
                .WithBody( JsonSerializer.Serialize( new {
                    items = new[] { new { path = "dir/one.lua", sha = "one" } },
                    page = new { nextCursor = cursor },
                } ) ) );
        server.Server
            .Given( Request.Create().WithPath( "/v2/tree" ).WithParam( "cursor", cursor ).UsingGet() )
            .AtPriority( 1 )
            .RespondWith( Response.Create().WithStatusCode( 200 )
                .WithHeader( "Content-Type", "application/json" )
                .WithBody( """{"items":[{"path":"dir/two.lua","sha":"two"}],"page":{"nextCursor":null}}""" ) );
        var settings = new AppSettings
        {
            ApiPreferredRoute = ApiRoutePreference.Default,
            ApiPreferredRouteValidUntilUtc = DateTimeOffset.UtcNow.AddHours( 1 ),
        };
        var result = await CreateSut( server.CreateHttpClient(), settings: settings ).GetTreeAsync( TestContext.Current.CancellationToken );
        Assert.True( result.IsSuccess );
        Assert.Equal( ["dir/one.lua", "dir/two.lua"], result.Value.Select( entry => entry.Path ) );
        Assert.Equal( 2, server.Server.LogEntries.Count() );
    }

    [Theory]
    [InlineData( 101 )]
    [InlineData( 500 )]
    [InlineData( 1001 )]
    public async Task DownloadFilePathsAsyncはWireMockで100件ずつの繰り返しクエリを送信する( int count ) {
        using var server = new WireMockTestServer();
        var paths = Enumerable.Range( 0, count ).Select( i => $"p/{i} +&#?,%.lua" ).ToArray();
        foreach(var batch in paths.Chunk( 100 )) {
            server.Server
                .Given( Request.Create().WithPath( "/v2/files" ).WithParam( "path", batch ).UsingGet() )
                .RespondWith( Response.Create().WithStatusCode( 200 )
                    .WithHeader( "Content-Type", "application/json" )
                    .WithHeader( "ETag", "\"batch-etag\"" )
                    .WithBody( JsonSerializer.Serialize( new {
                        files = batch.Select( path => new { path, url = "https://example.test/raw" } ),
                    } ) ) );
        }
        var settings = new AppSettings
        {
            ApiPreferredRoute = ApiRoutePreference.Default,
            ApiPreferredRouteValidUntilUtc = DateTimeOffset.UtcNow.AddHours( 1 ),
        };
        var result = await CreateSut( server.CreateHttpClient(), settings: settings ).DownloadFilePathsAsync(
            new ApiDownloadFilePathsRequest( paths, "\"old-etag\"" ), TestContext.Current.CancellationToken );
        Assert.True( result.IsSuccess, string.Join( "; ", result.Errors.Select( error => error.Message ) ) );
        Assert.Equal( paths, result.Value.Items.Select( item => item.Path ) );
        Assert.Null( result.Value.ETag );
        var requests = server.Server.LogEntries.Select( entry => entry.RequestMessage! ).ToArray();
        Assert.Equal( (count + 99) / 100, requests.Length );
        Assert.All( requests, request => {
            Assert.Equal( "GET", request.Method );
            Assert.InRange( request.Query!["path"].Count, 1, 100 );
            Assert.False( request.Headers?.ContainsKey( "If-None-Match" ) ?? false );
        } );
    }

    /// <summary>
    /// テスト対象サービスを生成する。
    /// </summary>
    /// <param name="defaultClient">既定経路の HTTP クライアント。</param>
    /// <param name="ipv4Client">IPv4 優先経路の HTTP クライアント。</param>
    /// <param name="ipv6Client">IPv6 優先経路の HTTP クライアント。</param>
    /// <param name="settings">設定オブジェクト。</param>
    /// <returns>生成した API サービスを返す。</returns>
    private static ApiService CreateSut(
        HttpClient defaultClient,
        HttpClient? ipv4Client = null,
        HttpClient? ipv6Client = null,
        AppSettings? settings = null ) {
        var provider = new TreeHttpClientProvider( defaultClient, ipv4Client, ipv6Client );
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        appSettingsServiceMock.SetupGet( x => x.Settings ).Returns( settings ?? new AppSettings() );
        return new ApiService( appSettingsServiceMock.Object, Mock.Of<ILoggingService>(), provider );
    }

    /// <summary>
    /// 指定パスに一致する単一の受信リクエストを取得する。
    /// </summary>
    /// <param name="server">対象 WireMock サーバー。</param>
    /// <param name="path">対象パス。</param>
    /// <returns>受信リクエスト情報を返す。</returns>
    private static ReceivedRequest RequireSingleRequest( WireMockTestServer server, string path ) {
        Assert.Contains( server.Server.LogEntries, item => string.Equals( item.RequestMessage?.Path, path, StringComparison.Ordinal ) );
        var entry = server.Server.LogEntries.Last( item => string.Equals( item.RequestMessage?.Path, path, StringComparison.Ordinal ) );
        var requestMessage = entry.RequestMessage!;
        var headers = requestMessage.Headers is null
            ? new Dictionary<string, string[]>( StringComparer.OrdinalIgnoreCase )
            : requestMessage.Headers.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Select( value => value?.ToString() ?? string.Empty ).ToArray(),
                StringComparer.OrdinalIgnoreCase );
        var acceptHeaders = headers.TryGetValue( "Accept", out var acceptValues )
            ? acceptValues
            : [];

        return new ReceivedRequest(
            requestMessage.Method?.ToString() ?? string.Empty,
            requestMessage.Path?.ToString() ?? string.Empty,
            requestMessage.Body?.ToString() ?? string.Empty,
            headers,
            acceptHeaders );
    }

    /// <summary>
    /// 受信リクエストを表す。
    /// </summary>
    /// <param name="Method">HTTP メソッド。</param>
    /// <param name="Path">要求パス。</param>
    /// <param name="Body">要求本文。</param>
    /// <param name="Headers">ヘッダー一覧。</param>
    /// <param name="AcceptHeaders">Accept ヘッダー一覧。</param>
    private sealed record ReceivedRequest(
        string Method,
        string Path,
        string Body,
        IReadOnlyDictionary<string, string[]> Headers,
        IReadOnlyList<string> AcceptHeaders );
}