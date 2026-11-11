using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using DcsTranslationTool.Application.Contracts;
using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Results;
using DcsTranslationTool.Infrastructure.Services;
using DcsTranslationTool.Shared.Models;
using DcsTranslationTool.TestCommon.Http;
using DcsTranslationTool.TestCommon.TestDoubles;

using Moq;

namespace DcsTranslationTool.Infrastructure.UnitTests.Services;

public class ApiServiceTests {
    private static readonly JsonSerializerOptions s_webOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] expected = ["path/one", "path/two"];

    [Fact]
    public async Task GetHealthAsyncは正常レスポンスを成功として返す() {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var client = CreateClient((request, _) => {
            Assert.Equal( HttpMethod.Get, request.Method );
            Assert.Equal( "/v2/health", request.RequestUri?.AbsolutePath );

            var payload = JsonSerializer.Serialize( new
            {
                status = "ok",
                timestamp,
            }, s_webOptions );

            var response = new HttpResponseMessage( HttpStatusCode.OK )
            {
                Content = new StringContent( payload, Encoding.UTF8, "application/json" ),
            };

            return Task.FromResult( response );
        });
        var sut = CreateSut( client );

        // Act
        var result = await sut.GetHealthAsync( TestContext.Current.CancellationToken );

        // Assert
        Assert.True( result.IsSuccess );
        Assert.Equal( ApiHealthStatus.Ok, result.Value.Status );
        Assert.Equal( timestamp, result.Value.Timestamp );
    }

    [Fact]
    public async Task GetHealthAsyncはエラーステータスを失敗として返す() {
        // Arrange
        var client = CreateClient((_, _) => {
            var response = new HttpResponseMessage( HttpStatusCode.InternalServerError )
            {
                ReasonPhrase = "Server Error",
            };
            return Task.FromResult( response );
        });
        var sut = CreateSut( client );

        // Act
        var result = await sut.GetHealthAsync( TestContext.Current.CancellationToken );

        // Assert
        Assert.True( result.IsFailed );
        Assert.Equal( "HTTP 500: Server Error", result.Errors.Single().Message );
    }

    [Fact]
    public async Task GetTreeAsyncはファイル一覧を正しくマッピングする() {
        // Arrange
        const string responsePayload =
            """
            {
              "page": { "nextCursor": null },
              "items": [
                {
                  "mode": "100644",
                  "path": "translations/file.po",
                  "sha": "file-sha",
                  "type": "blob"
                },
                {
                  "mode": "100644",
                  "path": "   ",
                  "sha": "ignore-sha",
                  "type": "blob"
                }
              ]
            }
            """;

        var client = CreateClient((request, _) => {
            Assert.Equal( HttpMethod.Get, request.Method );
            Assert.Equal( "/v2/tree", request.RequestUri?.AbsolutePath );

            var response = new HttpResponseMessage( HttpStatusCode.OK )
            {
                Content = new StringContent( responsePayload, Encoding.UTF8, "application/json" ),
            };

            return Task.FromResult( response );
        });
        var sut = CreateSut( client );

        // Act
        var result = await sut.GetTreeAsync( TestContext.Current.CancellationToken );

        // Assert
        Assert.True( result.IsSuccess );
        var entries = result.Value;
        Assert.Single( entries );

        var file = entries[0];
        Assert.Equal( "file.po", file.Name );
        Assert.Equal( "translations/file.po", file.Path );
        Assert.False( file.IsDirectory );
        Assert.Equal( "file-sha", file.RepoSha );
    }

    [Fact]
    public async Task GetTreeAsyncは初回にhealthレースを実行して勝者経路を利用する() {
        // Arrange
        var defaultRequestCount = 0;
        var ipv4RequestCount = 0;
        var ipv6RequestCount = 0;

        var defaultClient = CreateClient(async (_, cancellationToken) => {
            Interlocked.Increment( ref defaultRequestCount );
            await Task.Delay( 30, cancellationToken ).ConfigureAwait( false );
            return new HttpResponseMessage( HttpStatusCode.OK )
            {
                Content = new StringContent( CreateHealthPayload(), Encoding.UTF8, "application/json" ),
            };
        });

        var ipv4Client = CreateClient((request, _) => {
            Interlocked.Increment( ref ipv4RequestCount );
            if(request.RequestUri?.AbsolutePath == "/v2/tree") {
                return Task.FromResult( new HttpResponseMessage( HttpStatusCode.OK )
                {
                    Content = new StringContent( CreateTreePayload( "ipv4", "ipv4-sha" ), Encoding.UTF8, "application/json" ),
                } );
            }

            return Task.FromResult( new HttpResponseMessage( HttpStatusCode.OK )
            {
                Content = new StringContent( CreateHealthPayload(), Encoding.UTF8, "application/json" ),
            } );
        });

        var ipv6Client = CreateClient((_, _) => {
            Interlocked.Increment( ref ipv6RequestCount );
            return Task.FromResult( new HttpResponseMessage( HttpStatusCode.OK )
            {
                Content = new StringContent( CreateHealthPayload(), Encoding.UTF8, "application/json" ),
            } );
        });

        var sut = CreateSut( defaultClient, ipv4Client, ipv6Client );

        // Act
        var result = await sut.GetTreeAsync( TestContext.Current.CancellationToken );

        // Assert
        Assert.True( result.IsSuccess );
        Assert.InRange( defaultRequestCount, 0, 1 ); // health race の競合で未実行の場合がある
        Assert.Equal( 2, ipv4RequestCount ); // health + tree
        Assert.InRange( ipv6RequestCount, 0, 1 ); // health race の競合で未実行の場合がある
        Assert.Equal( "ipv4", result.Value.Single().Path );
    }

    [Fact]
    public async Task GetTreeAsyncは有効期限内でhealth再検証せずに既存優先経路を利用する() {
        // Arrange
        var requestCount = 0;
        var sharedClient = CreateClient((request, _) => {
            Interlocked.Increment( ref requestCount );
            if(request.RequestUri?.AbsolutePath == "/v2/health") {
                return Task.FromResult( new HttpResponseMessage( HttpStatusCode.OK )
                {
                    Content = new StringContent( CreateHealthPayload(), Encoding.UTF8, "application/json" ),
                } );
            }

            return Task.FromResult( new HttpResponseMessage( HttpStatusCode.OK )
            {
                Content = new StringContent( CreateTreePayload( "shared", "shared-sha" ), Encoding.UTF8, "application/json" ),
            } );
        });
        var settings = new AppSettings
        {
            ApiPreferredRoute = ApiRoutePreference.Default,
            ApiPreferredRouteValidUntilUtc = DateTimeOffset.UtcNow.AddMinutes( 30 ),
        };

        var sut = CreateSut( sharedClient, settings: settings );

        // Act
        var result = await sut.GetTreeAsync( TestContext.Current.CancellationToken );

        // Assert
        Assert.True( result.IsSuccess );
        Assert.Equal( 1, requestCount ); // tree のみ
        Assert.Equal( "shared", result.Value.Single().Path );
    }

    [Fact]
    public async Task GetTreeAsyncはdefault失敗時に代替経路へ再試行する() {
        // Arrange
        var defaultRequestCount = 0;
        var ipv4RequestCount = 0;
        var ipv6RequestCount = 0;

        var defaultClient = CreateClient((request, _) => {
            Interlocked.Increment( ref defaultRequestCount );
            Assert.Equal( "/v2/tree", request.RequestUri?.AbsolutePath );
            return Task.FromResult( new HttpResponseMessage( HttpStatusCode.InternalServerError )
            {
                ReasonPhrase = "default-failed",
            } );
        });
        var ipv4Client = CreateClient((request, _) => {
            Interlocked.Increment( ref ipv4RequestCount );
            Assert.Equal( "/v2/tree", request.RequestUri?.AbsolutePath );
            return Task.FromResult( new HttpResponseMessage( HttpStatusCode.OK )
            {
                Content = new StringContent( CreateTreePayload( "ipv4", "ipv4-sha" ), Encoding.UTF8, "application/json" ),
            } );
        });
        var ipv6Client = CreateClient((request, _) => {
            Interlocked.Increment( ref ipv6RequestCount );
            Assert.Equal( "/v2/tree", request.RequestUri?.AbsolutePath );
            return Task.FromResult( new HttpResponseMessage( HttpStatusCode.OK )
            {
                Content = new StringContent( CreateTreePayload( "ipv6", "ipv6-sha" ), Encoding.UTF8, "application/json" ),
            } );
        });
        var settings = new AppSettings
        {
            ApiPreferredRoute = ApiRoutePreference.Default,
            ApiPreferredRouteValidUntilUtc = DateTimeOffset.UtcNow.AddMinutes( 30 ),
        };
        var sut = CreateSut( defaultClient, ipv4Client, ipv6Client, settings );

        // Act
        var result = await sut.GetTreeAsync( TestContext.Current.CancellationToken );

        // Assert
        Assert.True( result.IsSuccess );
        Assert.Equal( 1, defaultRequestCount );
        Assert.Equal( 1, ipv4RequestCount );
        Assert.Equal( 0, ipv6RequestCount );
        Assert.Equal( "ipv4", result.Value.Single().Path );
        Assert.Equal( ApiRoutePreference.Ipv4, settings.ApiPreferredRoute );
    }

    [Fact]
    public async Task DownloadFilePathsAsyncはhealth検証が失敗してもdefault経路で継続する() {
        // Arrange
        var defaultClient = CreateClient((request, _) => {
            if(request.RequestUri?.AbsolutePath == "/v2/health") {
                return Task.FromResult( new HttpResponseMessage( HttpStatusCode.OK )
                {
                    Content = new StringContent( CreateHealthPayload(), Encoding.UTF8, "application/json" ),
                } );
            }

            Assert.Equal( expected, GetQueryPaths( request.RequestUri! ) );

            const string responsePayload =
                """
                {
                  "files": [
                    { "url": "https://example.test/raw/file1", "path": "path/one" },
                    { "url": "https://example.test/raw/file2", "path": "path/two" }
                  ],
                  "etag": "\"body-etag\""
                }
                """;

            return Task.FromResult( new HttpResponseMessage( HttpStatusCode.OK )
            {
                Content = new StringContent( responsePayload, Encoding.UTF8, "application/json" ),
            } );
        });
        var ipv4Client = CreateClient((_, _) => Task.FromResult( new HttpResponseMessage( HttpStatusCode.InternalServerError ) ));
        var ipv6Client = CreateClient((_, _) => Task.FromResult( new HttpResponseMessage( HttpStatusCode.InternalServerError ) ));
        var sut = CreateSut( defaultClient, ipv4Client, ipv6Client );
        var request = new ApiDownloadFilePathsRequest( ["path/one", "path/two"], null );

        // Act
        var result = await sut.DownloadFilePathsAsync( request, TestContext.Current.CancellationToken );

        // Assert
        Assert.True( result.IsSuccess );
        Assert.Equal( 2, result.Value.Items.Count );
    }

    [Fact]
    public async Task DownloadFilePathsAsyncは優先経路失敗時にフォールバック成功で優先経路を更新する() {
        // Arrange
        var defaultRequestCount = 0;
        var ipv4RequestCount = 0;
        var settings = new AppSettings
        {
            ApiPreferredRoute = ApiRoutePreference.Ipv4,
            ApiPreferredRouteValidUntilUtc = DateTimeOffset.UtcNow.AddMinutes( 30 ),
        };

        var defaultClient = CreateClient((request, _) => {
            Interlocked.Increment( ref defaultRequestCount );
            Assert.Equal( "/v2/files", request.RequestUri?.AbsolutePath );

            Assert.Equal( expected, GetQueryPaths( request.RequestUri! ) );

            const string responsePayload =
                """
                {
                  "files": [
                    { "url": "https://example.test/raw/file1", "path": "path/one" }
                  ],
                  "etag": "\"body-etag\""
                }
                """;
            return Task.FromResult( new HttpResponseMessage( HttpStatusCode.OK )
            {
                Content = new StringContent( responsePayload, Encoding.UTF8, "application/json" ),
            } );
        });
        var ipv4Client = CreateClient((request, _) => {
            Interlocked.Increment( ref ipv4RequestCount );
            Assert.Equal( "/v2/files", request.RequestUri?.AbsolutePath );
            return Task.FromResult( new HttpResponseMessage( HttpStatusCode.BadGateway )
            {
                ReasonPhrase = "ipv4-failed",
            } );
        });
        var ipv6Client = CreateClient((_, _) => Task.FromResult( new HttpResponseMessage( HttpStatusCode.InternalServerError ) ));
        var sut = CreateSut( defaultClient, ipv4Client, ipv6Client, settings );
        var request = new ApiDownloadFilePathsRequest( ["path/one", "path/two"], null );

        // Act
        var result = await sut.DownloadFilePathsAsync( request, TestContext.Current.CancellationToken );

        // Assert
        Assert.True( result.IsSuccess );
        Assert.Equal( 1, ipv4RequestCount );
        Assert.Equal( 1, defaultRequestCount );
        Assert.Equal( ApiRoutePreference.Default, settings.ApiPreferredRoute );
    }

    [Fact]
    public async Task DownloadFilePathsAsyncは優先経路で例外発生時もフォールバックを試行する() {
        // Arrange
        var defaultRequestCount = 0;
        var ipv4RequestCount = 0;
        var settings = new AppSettings
        {
            ApiPreferredRoute = ApiRoutePreference.Ipv4,
            ApiPreferredRouteValidUntilUtc = DateTimeOffset.UtcNow.AddMinutes( 30 ),
        };

        var defaultClient = CreateClient((request, _) => {
            Interlocked.Increment( ref defaultRequestCount );
            Assert.Equal( "/v2/files", request.RequestUri?.AbsolutePath );

            Assert.Equal( expected, GetQueryPaths( request.RequestUri! ) );

            const string responsePayload =
                """
                {
                  "files": [
                    { "url": "https://example.test/raw/file1", "path": "path/one" }
                  ],
                  "etag": "\"body-etag\""
                }
                """;
            return Task.FromResult( new HttpResponseMessage( HttpStatusCode.OK )
            {
                Content = new StringContent( responsePayload, Encoding.UTF8, "application/json" ),
            } );
        });
        var ipv4Client = CreateClient((request, _) => {
            Interlocked.Increment( ref ipv4RequestCount );
            Assert.Equal( "/v2/files", request.RequestUri?.AbsolutePath );
            throw new HttpRequestException( "ipv4-transport-error" );
        });
        var ipv6Client = CreateClient((_, _) => Task.FromResult( new HttpResponseMessage( HttpStatusCode.InternalServerError ) ));
        var sut = CreateSut( defaultClient, ipv4Client, ipv6Client, settings );
        var request = new ApiDownloadFilePathsRequest( ["path/one", "path/two"], null );

        // Act
        var result = await sut.DownloadFilePathsAsync( request, TestContext.Current.CancellationToken );

        // Assert
        Assert.True( result.IsSuccess );
        Assert.Equal( 1, ipv4RequestCount );
        Assert.Equal( 1, defaultRequestCount );
        Assert.Equal( ApiRoutePreference.Default, settings.ApiPreferredRoute );
    }

    [Fact]
    public async Task CreatePullRequestAsyncは優先経路失敗時にフォールバック再試行しない() {
        // Arrange
        var defaultRequestCount = 0;
        var ipv4RequestCount = 0;
        var settings = new AppSettings
        {
            ApiPreferredRoute = ApiRoutePreference.Ipv4,
            ApiPreferredRouteValidUntilUtc = DateTimeOffset.UtcNow.AddMinutes( 30 ),
        };

        var defaultClient = CreateClient((request, _) => {
            Interlocked.Increment( ref defaultRequestCount );
            Assert.Equal( "/v2/prs", request.RequestUri?.AbsolutePath );
            return Task.FromResult( new HttpResponseMessage( HttpStatusCode.OK ) );
        });
        var ipv4Client = CreateClient((request, _) => {
            Interlocked.Increment( ref ipv4RequestCount );
            Assert.Equal( "/v2/prs", request.RequestUri?.AbsolutePath );
            return Task.FromResult( new HttpResponseMessage( HttpStatusCode.BadGateway )
            {
                ReasonPhrase = "ipv4-failed",
            } );
        });
        var ipv6Client = CreateClient((_, _) => Task.FromResult( new HttpResponseMessage( HttpStatusCode.InternalServerError ) ));
        var sut = CreateSut( defaultClient, ipv4Client, ipv6Client, settings );
        var request = new ApiCreatePullRequestRequest(
            "feature/test",
            "feat: add files",
            "Add files",
            "Body",
            [new ApiPullRequestFile( ApiPullRequestFileOperation.Upsert, "content/upsert.txt", "c29tZS1jb250ZW50" )]
        );

        // Act
        var result = await sut.CreatePullRequestAsync( request, TestContext.Current.CancellationToken );

        // Assert
        Assert.True( result.IsFailed );
        Assert.Equal( 1, ipv4RequestCount );
        Assert.Equal( 0, defaultRequestCount );
        Assert.Equal( ApiRoutePreference.None, settings.ApiPreferredRoute );
        Assert.Null( settings.ApiPreferredRouteValidUntilUtc );
    }

    [Fact]
    public async Task CreatePullRequestAsyncは優先経路で例外発生時に優先経路を解除する() {
        // Arrange
        var defaultRequestCount = 0;
        var ipv4RequestCount = 0;
        var settings = new AppSettings
        {
            ApiPreferredRoute = ApiRoutePreference.Ipv4,
            ApiPreferredRouteValidUntilUtc = DateTimeOffset.UtcNow.AddMinutes( 30 ),
        };

        var defaultClient = CreateClient((request, _) => {
            Interlocked.Increment( ref defaultRequestCount );
            Assert.Equal( "/v2/prs", request.RequestUri?.AbsolutePath );
            return Task.FromResult( new HttpResponseMessage( HttpStatusCode.OK ) );
        });
        var ipv4Client = CreateClient((request, _) => {
            Interlocked.Increment( ref ipv4RequestCount );
            Assert.Equal( "/v2/prs", request.RequestUri?.AbsolutePath );
            throw new HttpRequestException( "ipv4-transport-error" );
        });
        var ipv6Client = CreateClient((_, _) => Task.FromResult( new HttpResponseMessage( HttpStatusCode.InternalServerError ) ));
        var sut = CreateSut( defaultClient, ipv4Client, ipv6Client, settings );
        var request = new ApiCreatePullRequestRequest(
            "feature/test",
            "feat: add files",
            "Add files",
            "Body",
            [new ApiPullRequestFile( ApiPullRequestFileOperation.Upsert, "content/upsert.txt", "c29tZS1jb250ZW50" )]
        );

        // Act
        var result = await sut.CreatePullRequestAsync( request, TestContext.Current.CancellationToken );

        // Assert
        Assert.True( result.IsFailed );
        Assert.Equal( 1, ipv4RequestCount );
        Assert.Equal( 0, defaultRequestCount );
        Assert.Equal( ApiRoutePreference.None, settings.ApiPreferredRoute );
        Assert.Null( settings.ApiPreferredRouteValidUntilUtc );
    }

    [Fact]
    public async Task CreatePullRequestAsyncは有効なファイルのみを送信し結果を変換する() {
        // Arrange
        var client = CreateClient(async (request, token) => {
            Assert.Equal( HttpMethod.Post, request.Method );
            Assert.Equal( "/v2/prs", request.RequestUri?.AbsolutePath );

            var body = await request.Content!.ReadAsStringAsync( token ).ConfigureAwait(false);
            using var document = JsonDocument.Parse( body );
            var root = document.RootElement;

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

            const string responsePayload =
                """
                {
                  "branchName": "feature/test",
                  "commitSha": "abc123",
                  "number": 42,
                  "url": "https://example.test/pr/42",
                  "reused": true
                }
                """;

            var response = new HttpResponseMessage( HttpStatusCode.OK )
            {
                Content = new StringContent( responsePayload, Encoding.UTF8, "application/json" ),
            };

            return response;
        });
        var sut = CreateSut( client );

        var request = new ApiCreatePullRequestRequest(
            "feature/test",
            "feat: add files",
            "Add files",
            "Body",
            [
                new ApiPullRequestFile( ApiPullRequestFileOperation.Upsert, "content/upsert.txt", "追加と更新\n本文" ),
                new ApiPullRequestFile( ApiPullRequestFileOperation.Delete, "content/remove.txt", null ),
                new ApiPullRequestFile( ApiPullRequestFileOperation.Upsert, "   ", "ignored" ),
            ]
        );

        // Act
        var result = await sut.CreatePullRequestAsync( request, TestContext.Current.CancellationToken );

        // Assert
        Assert.True( result.IsSuccess );
        var outcome = result.Value;
        Assert.True( outcome.Success );
        Assert.Null( outcome.Message );
        Assert.Single( outcome.Entries );

        var entry = outcome.Entries[0];
        Assert.Equal( "feature/test", entry.BranchName );
        Assert.Equal( "abc123", entry.CommitSha );
        Assert.Equal( 42, entry.PullRequestNumber );
        Assert.Equal( new Uri( "https://example.test/pr/42" ), entry.PullRequestUrl );
        Assert.Null( entry.Note );
        Assert.True( entry.Reused );
    }

    [Fact]
    public async Task DownloadFilePathsAsyncはパス未指定時に失敗を返す() {
        // Arrange
        var sut = CreateSut( CreateClient((_, _) => Task.FromResult( new HttpResponseMessage( HttpStatusCode.OK ) )) );
        var request = new ApiDownloadFilePathsRequest( [], null );

        // Act
        var result = await sut.DownloadFilePathsAsync( request, TestContext.Current.CancellationToken );

        // Assert
        Assert.True( result.IsFailed );
        Assert.Equal( "Paths には少なくとも1つの値が含まれている必要があります。", result.Errors.Single().Message );
        Assert.Equal( nameof( ResultErrorKind.Validation ), result.Errors.Single().Metadata["kind"] );
        Assert.Equal( "API_PATHS_REQUIRED", result.Errors.Single().Metadata["code"] );
    }

    [Fact]
    public async Task DownloadFilePathsAsyncは空白のみパス時に失敗を返す() {
        // Arrange
        var sut = CreateSut( CreateClient((_, _) => Task.FromResult( new HttpResponseMessage( HttpStatusCode.OK ) )) );
        var request = new ApiDownloadFilePathsRequest( ["   ", string.Empty], null );

        // Act
        var result = await sut.DownloadFilePathsAsync( request, TestContext.Current.CancellationToken );

        // Assert
        Assert.True( result.IsFailed );
        Assert.Equal( "Paths には少なくとも1つの空でない値が含まれている必要があります。", result.Errors.Single().Message );
        Assert.Equal( nameof( ResultErrorKind.Validation ), result.Errors.Single().Metadata["kind"] );
        Assert.Equal( "API_PATHS_EMPTY", result.Errors.Single().Metadata["code"] );
    }

    [Fact]
    public async Task DownloadFilePathsAsyncはnullパス時に失敗を返す() {
        // Arrange
        var sut = CreateSut( CreateClient((_, _) => Task.FromResult( new HttpResponseMessage( HttpStatusCode.OK ) )) );
        var request = new ApiDownloadFilePathsRequest( null!, null );

        // Act
        var result = await sut.DownloadFilePathsAsync( request, TestContext.Current.CancellationToken );

        // Assert
        Assert.True( result.IsFailed );
        Assert.Equal( "Paths には少なくとも1つの値が含まれている必要があります。", result.Errors.Single().Message );
        Assert.Equal( nameof( ResultErrorKind.Validation ), result.Errors.Single().Metadata["kind"] );
        Assert.Equal( "API_PATHS_REQUIRED", result.Errors.Single().Metadata["code"] );
    }

    [Fact]
    public async Task DownloadFilePathsAsyncはパス上限超過時に失敗を返す() {
        // Arrange
        var sut = CreateSut( CreateClient((_, _) => Task.FromResult( new HttpResponseMessage( HttpStatusCode.OK ) )) );
        var paths = Enumerable.Range( 1, 501 ).Select( index => $"path/{index}" ).ToArray();
        var request = new ApiDownloadFilePathsRequest( paths, null );

        // Act
        var result = await sut.DownloadFilePathsAsync( request, TestContext.Current.CancellationToken );

        // Assert
        Assert.True( result.IsFailed );
        Assert.Equal( "Paths には500個以下のアイテムを含める必要があります。", result.Errors.Single().Message );
        Assert.Equal( nameof( ResultErrorKind.Validation ), result.Errors.Single().Metadata["kind"] );
        Assert.Equal( "API_PATHS_LIMIT", result.Errors.Single().Metadata["code"] );
    }

    [Fact]
    public async Task DownloadFilePathsAsyncはHTTP失敗時に既存エラーコードを返す() {
        // Arrange
        var client = CreateClient((_, _) => Task.FromResult( new HttpResponseMessage( HttpStatusCode.InternalServerError )
        {
            ReasonPhrase = "Server Error",
            Content = new StringContent( "failed", Encoding.UTF8, "text/plain" ),
        } ));
        var sut = CreateSut( client );
        var request = new ApiDownloadFilePathsRequest( ["path/one"], null );

        // Act
        var result = await sut.DownloadFilePathsAsync( request, TestContext.Current.CancellationToken );

        // Assert
        Assert.True( result.IsFailed );
        Assert.Equal( "Server Error - failed", result.Errors.Single().Message );
        Assert.Equal( nameof( ResultErrorKind.External ), result.Errors.Single().Metadata["kind"] );
        Assert.Equal( "API_HTTP_ERROR", result.Errors.Single().Metadata["code"] );
    }

    [Fact]
    public async Task DownloadFilePathsAsyncはv2にない条件付き取得を送信しない() {
        var client = CreateClient((request, _) => {
            Assert.Equal( HttpMethod.Get, request.Method );
            Assert.Equal( "/v2/files", request.RequestUri?.AbsolutePath );
            Assert.False( request.Headers.Contains( "If-None-Match" ) );
            return Task.FromResult( new HttpResponseMessage( HttpStatusCode.NotModified ) );
        });
        var sut = CreateSut( client );
        var result = await sut.DownloadFilePathsAsync(
            new ApiDownloadFilePathsRequest( ["path/one"], "\"old-etag\"" ),
            TestContext.Current.CancellationToken );
        Assert.True( result.IsFailed );
        Assert.Equal( "API_HTTP_ERROR", result.Errors.Single().Metadata["code"] );
    }

    [Fact]
    public async Task DownloadFilePathsAsyncはURLとパスを正しくマッピングする() {
        // Arrange
        const string responsePayload =
            """
            {
              "files": [
                { "url": "https://example.test/raw/file1", "path": "path/one" },
                { "url": "https://example.test/raw/file2", "path": "path/two" },
                { "url": "", "path": "ignore" }
              ],
              "etag": "\"body-etag\""
            }
            """;

        var client = CreateClient((request, _) => {
            Assert.Equal( HttpMethod.Get, request.Method );
            Assert.Equal( "/v2/files", request.RequestUri?.AbsolutePath );

            Assert.Null( request.Content );
            Assert.Equal( expected, GetQueryPaths( request.RequestUri! ) );

            var response = new HttpResponseMessage( HttpStatusCode.OK )
            {
                Content = new StringContent( responsePayload, Encoding.UTF8, "application/json" ),
            };
            response.Headers.ETag = new EntityTagHeaderValue( "\"etag-header\"" );

            return Task.FromResult( response );
        });
        var sut = CreateSut( client );

        var request = new ApiDownloadFilePathsRequest( ["path/one", "path/two"], null );

        // Act
        var result = await sut.DownloadFilePathsAsync( request, TestContext.Current.CancellationToken );

        // Assert
        Assert.True( result.IsSuccess );
        var value = result.Value;
        Assert.Null( value.ETag );
        var items = value.Items.ToArray();
        Assert.Equal( 2, items.Length );
        Assert.Equal( "https://example.test/raw/file1", items[0].Url );
        Assert.Equal( "path/one", items[0].Path );
        Assert.Equal( "https://example.test/raw/file2", items[1].Url );
        Assert.Equal( "path/two", items[1].Path );
    }

    [Fact]
    public async Task GetTreeAsyncは特殊文字のカーソルをエンコードして全ページを取得する() {
        var requests = new List<string>();
        var client = CreateClient((request, _) => {
            requests.Add( request.RequestUri!.PathAndQuery );
            var payload = requests.Count == 1
                ? """{"items":[{"path":"a/one.lua","sha":"one"}],"page":{"nextCursor":"+/=& 日本語"}}"""
                : """{"items":[{"path":"a/two.lua","sha":"two"}],"page":{"nextCursor":null}}""";
            return Task.FromResult( new HttpResponseMessage( HttpStatusCode.OK )
            {
                Content = new StringContent( payload, Encoding.UTF8, "application/json" ),
            } );
        });
        var result = await CreateSut( client, settings: PreferredDefault() ).GetTreeAsync( TestContext.Current.CancellationToken );
        Assert.True( result.IsSuccess );
        Assert.Equal( ["a/one.lua", "a/two.lua"], result.Value.Select( entry => entry.Path ) );
        Assert.Equal( ["/v2/tree", "/v2/tree?cursor=" + Uri.EscapeDataString( "+/=& 日本語" )], requests );
    }

    [Fact]
    public async Task GetTreeAsyncは途中ページ失敗時に部分的な一覧を返さない() {
        var calls = 0;
        var client = CreateClient((request, _) => {
            calls++;
            return Task.FromResult( request.RequestUri!.Query.Length == 0
                ? new HttpResponseMessage( HttpStatusCode.OK )
                {
                    Content = new StringContent( """{"items":[{"path":"partial.lua"}],"page":{"nextCursor":"next"}}""", Encoding.UTF8, "application/json" ),
                }
                : new HttpResponseMessage( HttpStatusCode.BadRequest ) );
        });
        var result = await CreateSut( client, settings: PreferredDefault() ).GetTreeAsync( TestContext.Current.CancellationToken );
        Assert.True( result.IsFailed );
        Assert.Equal( 6, calls );
    }

    [Theory]
    [InlineData( "{\"items\":[]}" )]
    [InlineData( "{\"items\":[],\"page\":{\"nextCursor\":\"same\"}}" )]
    public async Task GetTreeAsyncは不正なページ情報を成功として返さない( string payload ) {
        var client = CreateClient((_, _) => Task.FromResult( new HttpResponseMessage( HttpStatusCode.OK )
        {
            Content = new StringContent( payload, Encoding.UTF8, "application/json" ),
        } ));
        var result = await CreateSut( client, settings: PreferredDefault() ).GetTreeAsync( TestContext.Current.CancellationToken );
        Assert.True( result.IsFailed );
    }

    [Fact]
    public async Task DownloadFilePathsAsyncは500件を100件ずつ取得して全件を返す() {
        var paths = Enumerable.Range( 0, 500 ).Select( i => $"フォルダ/{i} +&#?,%.lua" ).ToArray();
        var received = new List<string>();
        var client = CreateClient((request, _) => {
            Assert.Equal( HttpMethod.Get, request.Method );
            Assert.Equal( "/v2/files", request.RequestUri!.AbsolutePath );
            Assert.False( request.Headers.Contains( "If-None-Match" ) );
            var batch = GetQueryPaths( request.RequestUri );
            Assert.Equal( 100, batch.Length );
            received.AddRange( batch );
            var response = new HttpResponseMessage( HttpStatusCode.OK )
            {
                Content = JsonContent( new { files = batch.Select( path => new { path, url = "https://example.test/raw" } ) } ),
            };
            response.Headers.ETag = new EntityTagHeaderValue( "\"batch-etag\"" );
            return Task.FromResult( response );
        });
        var result = await CreateSut( client, settings: PreferredDefault() ).DownloadFilePathsAsync(
            new ApiDownloadFilePathsRequest( paths, "\"old-etag\"" ), TestContext.Current.CancellationToken );
        Assert.True( result.IsSuccess );
        Assert.Equal( paths, received );
        Assert.Equal( paths, result.Value.Items.Select( item => item.Path ) );
        Assert.Null( result.Value.ETag );
    }

    [Fact]
    public async Task DownloadFilePathsAsyncは途中バッチ失敗時に部分的な結果を返さない() {
        var client = CreateClient((request, _) => Task.FromResult(
            GetQueryPaths( request.RequestUri! )[0] == "path/100"
                ? new HttpResponseMessage( HttpStatusCode.BadRequest )
                : new HttpResponseMessage( HttpStatusCode.OK )
                {
                    Content = JsonContent( new { files = GetQueryPaths( request.RequestUri! ).Select( path => new { path, url = "https://example.test/raw" } ) } ),
                } ));
        var paths = Enumerable.Range( 0, 101 ).Select( i => $"path/{i}" ).ToArray();
        var result = await CreateSut( client, settings: PreferredDefault() ).DownloadFilePathsAsync(
            new ApiDownloadFilePathsRequest( paths, null ), TestContext.Current.CancellationToken );
        Assert.True( result.IsFailed );
    }

    [Fact]
    public async Task DownloadFilePathsAsyncは重複パスを一度だけ送信する() {
        var client = CreateClient((request, _) => {
            Assert.Equal( ["path/one"], GetQueryPaths( request.RequestUri! ) );
            return Task.FromResult( new HttpResponseMessage( HttpStatusCode.OK )
            {
                Content = JsonContent( new { files = new[] { new { path = "path/one", url = "https://example.test/raw" } } } ),
            } );
        });
        var result = await CreateSut( client, settings: PreferredDefault() ).DownloadFilePathsAsync(
            new ApiDownloadFilePathsRequest( ["path/one", " path/one "], null ), TestContext.Current.CancellationToken );
        Assert.True( result.IsSuccess );
        Assert.Single( result.Value.Items );
    }

    private static StringContent JsonContent( object payload ) =>
        new( JsonSerializer.Serialize( payload, s_webOptions ), Encoding.UTF8, "application/json" );

    private static AppSettings PreferredDefault() => new()
    {
        ApiPreferredRoute = ApiRoutePreference.Default,
        ApiPreferredRouteValidUntilUtc = DateTimeOffset.UtcNow.AddHours( 1 ),
    };

    private static string[] GetQueryPaths( Uri uri ) =>
        uri.Query.TrimStart( '?' ).Split( '&' )
            .Select( part => Uri.UnescapeDataString( part.Split( '=', 2 )[1] ) ).ToArray();

    private static ApiService CreateSut(
        HttpClient defaultClient,
        HttpClient? ipv4Client = null,
        HttpClient? ipv6Client = null,
        AppSettings? settings = null
    ) {
        var provider = new TreeHttpClientProvider( defaultClient, ipv4Client, ipv6Client );
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        appSettingsServiceMock.SetupGet( x => x.Settings ).Returns( settings ?? new AppSettings() );
        return new ApiService( appSettingsServiceMock.Object, NoOpLoggingService.Instance, provider );
    }

    private static string CreateTreePayload( string path, string sha ) =>
        $$"""
          {
            "page": { "nextCursor": null },
            "items": [
              {
                "mode": "100644",
                "path": "{{path}}",
                "sha": "{{sha}}",
                "type": "blob"
              }
            ]
          }
          """;

    private static string CreateHealthPayload() =>
        """
        {
          "status": "ok",
          "timestamp": "2026-01-01T00:00:00Z"
        }
        """;

    private static HttpClient CreateClient( Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder ) {
        return TestHttpClientFactory.Create( responder, "https://example.test/" );
    }
}