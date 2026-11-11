using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using DcsTranslationTool.Application.Contracts;
using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Results;
using DcsTranslationTool.Infrastructure.Http.ApiClient.Models;
using DcsTranslationTool.Infrastructure.Interfaces;
using DcsTranslationTool.Shared.Models;

using FluentResults;

using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using Microsoft.Kiota.Serialization.Json;

using DcsApiClient = DcsTranslationTool.Infrastructure.Http.ApiClient.ApiClient;

namespace DcsTranslationTool.Infrastructure.Services;

/// <summary>APIクライアントを操作する</summary>
public class ApiService(
    IAppSettingsService appSettingsService,
    ILoggingService loggingService,
    ITreeHttpClientProvider treeHttpClientProvider
) : IApiService {
    private const string DefaultBaseUrl = "https://api.dcs-translation-japanese.workers.dev/";
    private const int TreePrimaryTimeoutSeconds = 10;
    private const int TreeFallbackTimeoutSeconds = 15;
    private static readonly TimeSpan RoutePreferenceTtl = TimeSpan.FromDays( 1 );
    private static readonly TimeSpan RouteVerificationRetryBackoff = TimeSpan.FromMinutes( 5 );
    private static readonly TimeSpan HealthRaceTimeout = TimeSpan.FromSeconds( 5 );

    private static readonly JsonSerializerOptions SerializerOptions = new( JsonSerializerDefaults.Web )
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IAppSettingsService _appSettingsService = appSettingsService;
    private readonly ILoggingService _logger = loggingService;
    private readonly ITreeHttpClientProvider _treeHttpClientProvider = treeHttpClientProvider;
    private readonly object _routePreferenceGate = new();

    /// <summary>APIのヘルスチェックを実行して結果を返却する</summary>
    public async Task<Result<ApiHealth>> GetHealthAsync( CancellationToken cancellationToken = default ) {
        try {
            return await ExecuteWithRouteFallbackAsync(
                async ( routeClient, token ) => {
                    using var response = await routeClient.Client.GetAsync( "v2/health", token ).ConfigureAwait( false );
                    if(!response.IsSuccessStatusCode)
                        return Result.Fail( ResultErrorFactory.External( $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}", "API_HTTP_ERROR" ) );

                    var payload = await response.Content.ReadFromJsonAsync<HealthResponse>( SerializerOptions, token ).ConfigureAwait( false );
                    if(payload is null)
                        return Result.Fail( ResultErrorFactory.External( "Response body was null.", "API_EMPTY_RESPONSE" ) );

                    var status = payload.Status switch
                    {
                        string value when string.Equals( value, "ok", StringComparison.OrdinalIgnoreCase ) => ApiHealthStatus.Ok,
                        _ => ApiHealthStatus.Unknown,
                    };

                    return Result.Ok( new ApiHealth( status, payload.Timestamp ) );
                },
                "GetHealthAsync",
                cancellationToken
            ).ConfigureAwait( false );
        }
        catch(Exception ex) {
            return Result.Fail( ResultErrorFactory.Unexpected( ex, "API_HEALTH_EXCEPTION" ) );
        }
    }

    /// <summary>APIを呼び出してツリー情報を取得しコレクションとして返却する</summary>
    public async Task<Result<IReadOnlyList<FileEntry>>> GetTreeAsync( CancellationToken cancellationToken = default ) {
        var routeClient = await ResolvePreferredClientAsync( cancellationToken ).ConfigureAwait( false );
        var preferredAttempt = await ExecuteTreeRequestAsync(
            routeClient.Client,
            $"{routeClient.Label}-preferred",
            TimeSpan.FromSeconds( TreePrimaryTimeoutSeconds ),
            cancellationToken
        ).ConfigureAwait( false );
        LogTreeAttempt( preferredAttempt );
        if(preferredAttempt.Result.IsSuccess) {
            LogTreeSummary( preferredAttempt );
            return preferredAttempt.Result;
        }

        var fallbackTargets = CreateTreeFallbackRoutes( routeClient.Preference );
        var lastAttempt = preferredAttempt;
        MarkRouteDegraded( routeClient.Preference, $"GetTreeAsync 優先経路が失敗した。Path={preferredAttempt.PathLabel}" );
        foreach(var fallbackTarget in fallbackTargets) {
            var fallbackAttempt = await ExecuteTreeRequestAsync(
                fallbackTarget.Client,
                fallbackTarget.Label,
                TimeSpan.FromSeconds( TreeFallbackTimeoutSeconds ),
                cancellationToken
            ).ConfigureAwait( false );
            LogTreeAttempt( fallbackAttempt );
            if(fallbackAttempt.Result.IsSuccess) {
                PromoteRoutePreference( fallbackTarget.Preference, $"GetTreeAsync フォールバック経路に成功した。Path={fallbackTarget.Label}" );
                LogTreeSummary( fallbackAttempt );
                return fallbackAttempt.Result;
            }

            lastAttempt = fallbackAttempt;
        }

        LogTreeSummary( lastAttempt );
        return lastAttempt.Result;
    }

    /// <summary>Tree取得失敗時の代替経路候補を生成する。</summary>
    /// <param name="preferred">優先経路。</param>
    /// <returns>再試行対象の経路一覧。</returns>
    private IReadOnlyList<RouteClient> CreateTreeFallbackRoutes( ApiRoutePreference preferred ) => preferred switch
    {
        ApiRoutePreference.Ipv4 =>
        [
            new RouteClient( ApiRoutePreference.Default, "default-fallback", _treeHttpClientProvider.DefaultClient ),
            new RouteClient( ApiRoutePreference.Ipv6, "ipv6-fallback", _treeHttpClientProvider.Ipv6PreferredClient ),
        ],
        ApiRoutePreference.Ipv6 =>
        [
            new RouteClient( ApiRoutePreference.Default, "default-fallback", _treeHttpClientProvider.DefaultClient ),
            new RouteClient( ApiRoutePreference.Ipv4, "ipv4-fallback", _treeHttpClientProvider.Ipv4PreferredClient ),
        ],
        _ =>
        [
            new RouteClient( ApiRoutePreference.Ipv4, "ipv4-fallback", _treeHttpClientProvider.Ipv4PreferredClient ),
            new RouteClient( ApiRoutePreference.Ipv6, "ipv6-fallback", _treeHttpClientProvider.Ipv6PreferredClient ),
        ],
    };

    /// <summary>優先経路クライアントを解決する。</summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>解決した経路クライアント。</returns>
    private async Task<RouteClient> ResolvePreferredClientAsync( CancellationToken cancellationToken ) {
        var now = DateTimeOffset.UtcNow;
        var (preference, validUntil, retryAfter) = GetRouteState();

        if(preference != ApiRoutePreference.None
            && validUntil.HasValue
            && validUntil.Value > now) {
            return CreateRouteClient( preference );
        }

        if(retryAfter > now) {
            _logger.Info( $"経路検証の再試行猶予中のため default を利用する。RetryAfter={retryAfter.Value:O}" );
            return CreateRouteClient( ApiRoutePreference.Default );
        }

        var verifiedPreference = await VerifyRouteByHealthRaceAsync( cancellationToken ).ConfigureAwait( false );
        return CreateRouteClient( verifiedPreference );
    }

    /// <summary>/v2/health エンドポイントで IPv4/IPv6 の経路レースを実行して優先経路を検証する。</summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>採用する経路。</returns>
    private async Task<ApiRoutePreference> VerifyRouteByHealthRaceAsync( CancellationToken cancellationToken ) {
        using var verificationCts = CancellationTokenSource.CreateLinkedTokenSource( cancellationToken );
        verificationCts.CancelAfter( HealthRaceTimeout );

        var ipv4Task = TryHealthAsync( _treeHttpClientProvider.Ipv4PreferredClient, "ipv4", verificationCts.Token );
        var ipv6Task = TryHealthAsync( _treeHttpClientProvider.Ipv6PreferredClient, "ipv6", verificationCts.Token );

        var firstCompletedTask = await Task.WhenAny( ipv4Task, ipv6Task ).ConfigureAwait( false );
        var firstAttempt = await firstCompletedTask.ConfigureAwait( false );

        if(firstAttempt.IsSuccess) {
            verificationCts.Cancel();
            var preference = firstAttempt.Label == "ipv4" ? ApiRoutePreference.Ipv4 : ApiRoutePreference.Ipv6;
            PersistRoutePreference( preference, true );
            _logger.Info( $"/health 経路検証に成功した。Winner={firstAttempt.Label}, ElapsedMs={firstAttempt.ElapsedMs}" );
            return preference;
        }

        var secondTask = ReferenceEquals( firstCompletedTask, ipv4Task ) ? ipv6Task : ipv4Task;
        var secondAttempt = await secondTask.ConfigureAwait( false );
        if(secondAttempt.IsSuccess) {
            var preference = secondAttempt.Label == "ipv4" ? ApiRoutePreference.Ipv4 : ApiRoutePreference.Ipv6;
            PersistRoutePreference( preference, true );
            _logger.Info( $"/health 経路検証に成功した。Winner={secondAttempt.Label}, ElapsedMs={secondAttempt.ElapsedMs}" );
            return preference;
        }

        PersistRoutePreference( ApiRoutePreference.None, false );
        _logger.Warn( $"/health 経路検証で両経路が失敗した。First={firstAttempt.Label}, Second={secondAttempt.Label}" );
        return ApiRoutePreference.Default;
    }

    /// <summary>/health を呼び出して疎通可否を返す。</summary>
    /// <param name="client">呼び出しに使用するクライアント。</param>
    /// <param name="label">経路ラベル。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>呼び出し結果。</returns>
    private static async Task<HealthProbeAttempt> TryHealthAsync( HttpClient client, string label, CancellationToken cancellationToken ) {
        var stopwatch = Stopwatch.StartNew();
        try {
            using var response = await client.GetAsync( "v2/health", cancellationToken ).ConfigureAwait( false );
            return new HealthProbeAttempt( label, response.IsSuccessStatusCode, (int)response.StatusCode, stopwatch.ElapsedMilliseconds );
        }
        catch(OperationCanceledException) {
            return new HealthProbeAttempt( label, false, null, stopwatch.ElapsedMilliseconds );
        }
        catch {
            return new HealthProbeAttempt( label, false, null, stopwatch.ElapsedMilliseconds );
        }
    }

    /// <summary>検証結果を設定へ反映する。</summary>
    /// <param name="preference">反映する優先経路。</param>
    /// <param name="verified">検証成功かどうか。</param>
    private void PersistRoutePreference( ApiRoutePreference preference, bool verified ) {
        var now = DateTimeOffset.UtcNow;
        lock(_routePreferenceGate) {
            var settings = _appSettingsService.Settings;
            settings.ApiRouteLastVerifiedAtUtc = now;

            if(verified) {
                settings.ApiPreferredRoute = preference;
                settings.ApiPreferredRouteValidUntilUtc = now.Add( RoutePreferenceTtl );
                settings.ApiRouteVerificationRetryAfterUtc = null;
                return;
            }

            settings.ApiPreferredRoute = ApiRoutePreference.None;
            settings.ApiPreferredRouteValidUntilUtc = null;
            settings.ApiRouteVerificationRetryAfterUtc = now.Add( RouteVerificationRetryBackoff );
        }
    }

    /// <summary>設定から経路状態を取得する。</summary>
    /// <returns>経路状態。</returns>
    private (ApiRoutePreference Preference, DateTimeOffset? ValidUntilUtc, DateTimeOffset? RetryAfterUtc) GetRouteState() {
        lock(_routePreferenceGate) {
            var settings = _appSettingsService.Settings;
            return (
                settings.ApiPreferredRoute,
                settings.ApiPreferredRouteValidUntilUtc,
                settings.ApiRouteVerificationRetryAfterUtc
            );
        }
    }

    /// <summary>優先経路に応じたクライアントを生成する。</summary>
    /// <param name="preference">優先経路。</param>
    /// <returns>経路クライアント。</returns>
    private RouteClient CreateRouteClient( ApiRoutePreference preference ) => preference switch
    {
        ApiRoutePreference.Ipv4 => new RouteClient( ApiRoutePreference.Ipv4, "ipv4", _treeHttpClientProvider.Ipv4PreferredClient ),
        ApiRoutePreference.Ipv6 => new RouteClient( ApiRoutePreference.Ipv6, "ipv6", _treeHttpClientProvider.Ipv6PreferredClient ),
        _ => new RouteClient( ApiRoutePreference.Default, "default", _treeHttpClientProvider.DefaultClient ),
    };

    /// <summary>優先経路失敗時に代替経路へ順次フォールバックして実行する。</summary>
    /// <typeparam name="T">戻り値の型。</typeparam>
    /// <param name="operation">経路ごとの実行処理。</param>
    /// <param name="operationName">実行名。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>実行結果。</returns>
    private async Task<Result<T>> ExecuteWithRouteFallbackAsync<T>(
        Func<RouteClient, CancellationToken, Task<Result<T>>> operation,
        string operationName,
        CancellationToken cancellationToken
    ) {
        var preferredRoute = await ResolvePreferredClientAsync( cancellationToken ).ConfigureAwait( false );
        var preferredResult = await ExecuteRouteOperationSafelyAsync(
            operation,
            preferredRoute,
            cancellationToken
        ).ConfigureAwait( false );
        if(preferredResult.IsSuccess)
            return preferredResult;

        if(!IsRouteRetryableFailure( preferredResult ))
            return preferredResult;

        MarkRouteDegraded( preferredRoute.Preference, $"{operationName} の優先経路が失敗した。Path={preferredRoute.Label}" );
        var fallbackRoutes = CreateFallbackRoutes( preferredRoute.Preference );
        var lastResult = preferredResult;
        foreach(var fallbackRoute in fallbackRoutes) {
            var fallbackResult = await ExecuteRouteOperationSafelyAsync(
                operation,
                fallbackRoute,
                cancellationToken
            ).ConfigureAwait( false );
            if(fallbackResult.IsSuccess) {
                PromoteRoutePreference( fallbackRoute.Preference, $"{operationName} のフォールバック経路が成功した。Path={fallbackRoute.Label}" );
                return fallbackResult;
            }

            if(!IsRouteRetryableFailure( fallbackResult ))
                return fallbackResult;

            lastResult = fallbackResult;
        }

        return lastResult;
    }

    /// <summary>経路実行を安全に実行し、例外を失敗結果へ変換する。</summary>
    /// <typeparam name="T">戻り値の型。</typeparam>
    /// <param name="operation">経路実行処理。</param>
    /// <param name="routeClient">実行経路。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>成功または失敗結果。</returns>
    private static async Task<Result<T>> ExecuteRouteOperationSafelyAsync<T>(
        Func<RouteClient, CancellationToken, Task<Result<T>>> operation,
        RouteClient routeClient,
        CancellationToken cancellationToken
    ) {
        try {
            return await operation( routeClient, cancellationToken ).ConfigureAwait( false );
        }
        catch(Exception ex) {
            return Result.Fail( ResultErrorFactory.Unexpected( ex, "API_ROUTE_OPERATION_EXCEPTION" ) );
        }
    }

    /// <summary>優先経路に対する代替経路候補を生成する。</summary>
    /// <param name="preferred">優先経路。</param>
    /// <returns>代替経路候補。</returns>
    private IReadOnlyList<RouteClient> CreateFallbackRoutes( ApiRoutePreference preferred ) => preferred switch
    {
        ApiRoutePreference.Ipv4 =>
        [
            new RouteClient( ApiRoutePreference.Default, "default-fallback", _treeHttpClientProvider.DefaultClient ),
            new RouteClient( ApiRoutePreference.Ipv6, "ipv6-fallback", _treeHttpClientProvider.Ipv6PreferredClient ),
        ],
        ApiRoutePreference.Ipv6 =>
        [
            new RouteClient( ApiRoutePreference.Default, "default-fallback", _treeHttpClientProvider.DefaultClient ),
            new RouteClient( ApiRoutePreference.Ipv4, "ipv4-fallback", _treeHttpClientProvider.Ipv4PreferredClient ),
        ],
        _ =>
        [
            new RouteClient( ApiRoutePreference.Ipv4, "ipv4-fallback", _treeHttpClientProvider.Ipv4PreferredClient ),
            new RouteClient( ApiRoutePreference.Ipv6, "ipv6-fallback", _treeHttpClientProvider.Ipv6PreferredClient ),
        ],
    };

    /// <summary>ルーティング起因の再試行対象エラーかどうかを判定する。</summary>
    /// <param name="result">判定対象。</param>
    /// <returns>再試行対象なら true。</returns>
    private static bool IsRouteRetryableFailure<T>( Result<T> result ) =>
        result.IsFailed && result.Errors.Any( IsRouteRetryableError );

    /// <summary>ルーティング起因の再試行対象エラーかどうかを判定する。</summary>
    /// <param name="error">判定対象。</param>
    /// <returns>再試行対象なら true。</returns>
    private static bool IsRouteRetryableError( IError error ) {
        if(error.Metadata.TryGetValue( "code", out var codeObject )
            && codeObject is string code) {
            if(code is "API_HTTP_ERROR" or "API_TIMEOUT")
                return true;
            if(code.EndsWith( "_EXCEPTION", StringComparison.Ordinal ))
                return true;
        }

        return false;
    }

    /// <summary>優先経路の劣化を反映し、次回再検証可能な状態へ更新する。</summary>
    /// <param name="failedRoute">失敗した優先経路。</param>
    /// <param name="reason">更新理由。</param>
    private void MarkRouteDegraded( ApiRoutePreference failedRoute, string reason ) {
        if(failedRoute == ApiRoutePreference.None) return;

        lock(_routePreferenceGate) {
            var settings = _appSettingsService.Settings;
            settings.ApiRouteLastVerifiedAtUtc = DateTimeOffset.UtcNow;
            settings.ApiPreferredRoute = ApiRoutePreference.None;
            settings.ApiPreferredRouteValidUntilUtc = null;
            settings.ApiRouteVerificationRetryAfterUtc = null;
        }

        _logger.Warn( $"{reason} 失敗経路={failedRoute}, 優先経路を解除した。" );
    }

    /// <summary>成功した経路を優先経路として昇格する。</summary>
    /// <param name="preference">昇格する経路。</param>
    /// <param name="reason">更新理由。</param>
    private void PromoteRoutePreference( ApiRoutePreference preference, string reason ) {
        if(preference == ApiRoutePreference.None) return;

        var now = DateTimeOffset.UtcNow;
        lock(_routePreferenceGate) {
            var settings = _appSettingsService.Settings;
            settings.ApiRouteLastVerifiedAtUtc = now;
            settings.ApiPreferredRoute = preference;
            settings.ApiPreferredRouteValidUntilUtc = now.Add( RoutePreferenceTtl );
            settings.ApiRouteVerificationRetryAfterUtc = null;
        }

        _logger.Info( $"{reason} 優先経路を更新した。Preference={preference}" );
    }

    /// <summary>APIを呼び出して複数ファイルのダウンロードリンクを取得する</summary>
    public async Task<Result<ApiDownloadFilePathsResult>> DownloadFilePathsAsync(
        ApiDownloadFilePathsRequest request,
        CancellationToken cancellationToken = default
    ) {
        ArgumentNullException.ThrowIfNull( request );

        var sanitizeResult = ValidateAndSanitizePaths( request.Paths );
        if(sanitizeResult.IsFailed)
            return Result.Fail( sanitizeResult.Errors );

        try {
            return await ExecuteWithRouteFallbackAsync(
                async ( routeClient, token ) => {
                    var items = new List<ApiDownloadFilePathsItem>();
                    foreach(var batch in sanitizeResult.Value.Distinct( StringComparer.Ordinal ).Chunk( 100 )) {
                        // Kiota 1.29 は配列クエリをカンマ区切りにするため、v2 の繰り返し path を構築する。
                        var query = string.Join( "&", batch.Select( path => $"path={Uri.EscapeDataString( path )}" ) );
                        using var message = new HttpRequestMessage( HttpMethod.Get, $"v2/files?{query}" );
                        message.Headers.Accept.Add( new MediaTypeWithQualityHeaderValue( "application/json" ) );
                        message.Headers.Accept.Add( new MediaTypeWithQualityHeaderValue( "application/problem+json" ) );
                        using var response = await routeClient.Client
                            .SendAsync( message, HttpCompletionOption.ResponseHeadersRead, token )
                            .ConfigureAwait( false );

                        var ensureResult = await EnsureSuccessStatusCodeAsync( response, token ).ConfigureAwait( false );
                        if(ensureResult.IsFailed)
                            return Result.Fail( ensureResult.Errors );

                        using var stream = await response.Content.ReadAsStreamAsync( token ).ConfigureAwait( false );
                        var node = await new JsonParseNodeFactory().GetRootParseNodeAsync( "application/json", stream, token ).ConfigureAwait( false );
                        var payload = node.GetObjectValue( Files.CreateFromDiscriminatorValue );
                        if(payload?.FilesProp is null)
                            return Result.Fail( ResultErrorFactory.External( "Response files were missing.", "API_EMPTY_RESPONSE" ) );

                        items.AddRange( payload.FilesProp
                            .Where( file => !string.IsNullOrWhiteSpace( file.Url ) && !string.IsNullOrWhiteSpace( file.Path ) )
                            .Select( file => new ApiDownloadFilePathsItem( file.Url!, file.Path! ) ) );
                    }

                    return Result.Ok( new ApiDownloadFilePathsResult( items, null ) );
                },
                "DownloadFilePathsAsync",
                cancellationToken
            ).ConfigureAwait( false );
        }
        catch(Exception ex) {
            return Result.Fail( ResultErrorFactory.Unexpected( ex, "API_DOWNLOAD_PATHS_EXCEPTION" ) );
        }
    }

    /// <summary>APIを呼び出してPull Requestを作成する</summary>
    public async Task<Result<ApiCreatePullRequestOutcome>> CreatePullRequestAsync(
        ApiCreatePullRequestRequest request,
        CancellationToken cancellationToken = default
    ) {
        ArgumentNullException.ThrowIfNull( request );
        RouteClient? routeClient = null;

        try {
            routeClient = await ResolvePreferredClientAsync( cancellationToken ).ConfigureAwait( false );
            var apiClientContext = InitializeApiClientContext( routeClient.Client );
            var files = (request.Files ?? [])
                .Where( file => file is not null && !string.IsNullOrWhiteSpace( file.Path ) )
                .Select( ToFilePayload )
                .Where( payload => payload is not null )
                .Select( payload => payload! )
                .ToList();

            var requestInfo = apiClientContext.ApiClient.V2.Prs.ToPostRequestInformation(
                new PullRequestCreate
                {
                    BranchName = request.BranchName,
                    CommitMessage = request.CommitMessage,
                    PrTitle = request.PrTitle,
                    PrBody = request.PrBody,
                    Files = files,
                }
            );

            using var message = await apiClientContext.RequestAdapter
                .ConvertToNativeRequestAsync<HttpRequestMessage>( requestInfo, cancellationToken )
                .ConfigureAwait( false );
            if(message is null)
                return Result.Fail( ResultErrorFactory.External( "Request message was null.", "API_REQUEST_BUILD_ERROR" ) );

            using var response = await routeClient.Client
                .SendAsync( message, HttpCompletionOption.ResponseHeadersRead, cancellationToken )
                .ConfigureAwait( false );

            if(!response.IsSuccessStatusCode) {
                var failedResult = Result.Fail<ApiCreatePullRequestOutcome>(
                    ResultErrorFactory.External( $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}", "API_HTTP_ERROR" )
                );
                if(IsRouteRetryableFailure( failedResult ))
                    MarkRouteDegraded( routeClient.Preference, $"CreatePullRequestAsync の優先経路が失敗した。Path={routeClient.Label}" );
                return failedResult;
            }

            var result = await response.Content
                .ReadFromJsonAsync<PullRequestCreation>( SerializerOptions, cancellationToken )
                .ConfigureAwait( false );

            if(result is null)
                return Result.Fail( ResultErrorFactory.External( "Response body was null.", "API_EMPTY_RESPONSE" ) );

            var entry = ToCreatePrEntry( result );
            var outcome = new ApiCreatePullRequestOutcome( true, null, [entry] );
            return Result.Ok( outcome );
        }
        catch(Exception ex) {
            var failedResult = Result.Fail<ApiCreatePullRequestOutcome>( ResultErrorFactory.Unexpected( ex, "API_CREATE_PR_EXCEPTION" ) );
            if(routeClient is not null && IsRouteRetryableFailure( failedResult ))
                MarkRouteDegraded( routeClient.Preference, $"CreatePullRequestAsync の優先経路で例外が発生した。Path={routeClient.Label}" );
            return failedResult;
        }
    }

    /// <summary>
    /// 渡された <see cref="HttpClient"/> と Kiota クライアント群を初期化する。
    /// </summary>
    /// <param name="httpClient">外部から供給される <see cref="HttpClient"/>。</param>
    /// <returns>API呼び出しに必要な依存一式を返す。</returns>
    private static ApiClientContext InitializeApiClientContext( HttpClient httpClient ) {
        var client = httpClient;
        var requestAdapter = new HttpClientRequestAdapter( new AnonymousAuthenticationProvider(), httpClient: client )
        {
            BaseUrl = client.BaseAddress?.AbsoluteUri?.TrimEnd( '/' ) ?? DefaultBaseUrl.TrimEnd( '/' )
        };
        var apiClient = new DcsApiClient( requestAdapter );
        return new ApiClientContext( client, requestAdapter, apiClient );
    }

    /// <summary>パス一覧を検証し、空白除去済み配列へ正規化する。</summary>
    /// <param name="paths">検証対象のパス一覧。</param>
    /// <returns>成功時は正規化済みパス配列、失敗時は検証エラーを返す。</returns>
    private static Result<string[]> ValidateAndSanitizePaths( IReadOnlyList<string>? paths ) {
        if(paths is null || paths.Count == 0)
            return Result.Fail( ResultErrorFactory.Validation( "Paths には少なくとも1つの値が含まれている必要があります。", "API_PATHS_REQUIRED" ) );

        var sanitizedPaths = paths
            .Select( path => path?.Trim() )
            .Where( path => !string.IsNullOrWhiteSpace( path ) )
            .Select( path => path! )
            .ToArray();

        if(sanitizedPaths.Length == 0)
            return Result.Fail( ResultErrorFactory.Validation( "Paths には少なくとも1つの空でない値が含まれている必要があります。", "API_PATHS_EMPTY" ) );

        if(sanitizedPaths.Length > 500)
            return Result.Fail( ResultErrorFactory.Validation( "Paths には500個以下のアイテムを含める必要があります。", "API_PATHS_LIMIT" ) );

        return Result.Ok( sanitizedPaths );
    }

    /// <summary>HTTPレスポンスの失敗を共通フォーマットでエラー変換する。</summary>
    /// <param name="response">判定対象のレスポンス。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>成功ステータス時は成功、失敗ステータス時はExternalエラーを返す。</returns>
    private static async Task<Result> EnsureSuccessStatusCodeAsync( HttpResponseMessage response, CancellationToken cancellationToken ) {
        if(response.IsSuccessStatusCode)
            return Result.Ok();

        var reason = response.ReasonPhrase ?? $"HTTP {(int)response.StatusCode}";
        if(response.Content is not null) {
            var body = await response.Content.ReadAsStringAsync( cancellationToken ).ConfigureAwait( false );
            if(!string.IsNullOrWhiteSpace( body ))
                reason = $"{reason} - {body}";
        }

        return Result.Fail( ResultErrorFactory.External( reason, "API_HTTP_ERROR" ) );
    }

    /// <summary>パスの末尾要素から名前を抽出する。</summary>
    /// <param name="path">スラッシュ区切りのパス。</param>
    /// <returns>末尾のセグメント。セグメントが無い場合は入力値を返す。</returns>
    private static string ExtractName( string path ) {
        var segments = path.Split( '/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
        return segments.Length switch
        {
            0 => path,
            _ => segments[^1],
        };
    }

    /// <summary>
    /// Tree API を呼び出し、処理結果と内訳を返す。
    /// </summary>
    /// <param name="client">呼び出しに使用する HTTP クライアント。</param>
    /// <param name="pathLabel">呼び出し経路ラベル。</param>
    /// <param name="timeout">試行タイムアウト。</param>
    /// <param name="cancellationToken">呼び出し元キャンセルトークン。</param>
    /// <returns>試行結果。</returns>
    private static async Task<TreeRequestAttempt> ExecuteTreeRequestAsync(
        HttpClient client,
        string pathLabel,
        TimeSpan timeout,
        CancellationToken cancellationToken
    ) {
        var totalStopwatch = Stopwatch.StartNew();
        long httpElapsedMs = 0;
        long deserializeElapsedMs = 0;
        long mapElapsedMs = 0;
        var rawCount = 0;
        var mappedCount = 0;
        int? statusCode = null;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource( cancellationToken );
        timeoutCts.CancelAfter( timeout );

        try {
            var entries = new List<FileEntry>();
            var cursors = new HashSet<string>( StringComparer.Ordinal );
            string? cursor = null;
            do {
                var httpStopwatch = Stopwatch.StartNew();
                var url = cursor is null ? "v2/tree" : $"v2/tree?cursor={Uri.EscapeDataString( cursor )}";
                using var response = await client.GetAsync( url, timeoutCts.Token ).ConfigureAwait( false );
                httpElapsedMs += httpStopwatch.ElapsedMilliseconds;
                statusCode = (int)response.StatusCode;
                if(!response.IsSuccessStatusCode) {
                    var error = ResultErrorFactory.External( $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}", "API_HTTP_ERROR" );
                    return new TreeRequestAttempt(
                        pathLabel, Result.Fail<IReadOnlyList<FileEntry>>( error ),
                        httpElapsedMs, deserializeElapsedMs, mapElapsedMs, totalStopwatch.ElapsedMilliseconds,
                        rawCount, mappedCount, statusCode, false
                    );
                }

                var deserializeStopwatch = Stopwatch.StartNew();
                using var stream = await response.Content.ReadAsStreamAsync( timeoutCts.Token ).ConfigureAwait( false );
                var node = await new JsonParseNodeFactory().GetRootParseNodeAsync( "application/json", stream, timeoutCts.Token ).ConfigureAwait( false );
                var payload = node.GetObjectValue( TreeSnapshot.CreateFromDiscriminatorValue );
                deserializeElapsedMs += deserializeStopwatch.ElapsedMilliseconds;
                if(payload?.Items is null || payload.Page is null)
                    throw new JsonException( "Tree response items or page were missing." );

                rawCount += payload.Items.Count;
                var mapStopwatch = Stopwatch.StartNew();
                foreach(var item in payload.Items) {
                    if(string.IsNullOrWhiteSpace( item.Path )) continue;
                    entries.Add( new FileEntry( ExtractName( item.Path ), item.Path, false, repoSha: item.Sha ) );
                }
                mapElapsedMs += mapStopwatch.ElapsedMilliseconds;
                cursor = payload.Page.NextCursor;
                if(cursor is not null && !cursors.Add( cursor ))
                    throw new JsonException( "Tree response cursor was repeated." );
            } while(cursor is not null);

            mappedCount = entries.Count;
            return new TreeRequestAttempt(
                pathLabel,
                Result.Ok<IReadOnlyList<FileEntry>>( entries ),
                httpElapsedMs,
                deserializeElapsedMs,
                mapElapsedMs,
                totalStopwatch.ElapsedMilliseconds,
                rawCount,
                mappedCount,
                statusCode,
                false
            );
        }
        catch(OperationCanceledException) when(!cancellationToken.IsCancellationRequested) {
            var error = ResultErrorFactory.External( $"Tree API がタイムアウトした。TimeoutMs={(int)timeout.TotalMilliseconds}", "API_TIMEOUT" );
            return new TreeRequestAttempt(
                pathLabel,
                Result.Fail<IReadOnlyList<FileEntry>>( error ),
                httpElapsedMs,
                deserializeElapsedMs,
                mapElapsedMs,
                totalStopwatch.ElapsedMilliseconds,
                rawCount,
                mappedCount,
                statusCode,
                true
            );
        }
        catch(Exception ex) {
            var error = ResultErrorFactory.Unexpected( ex, "API_TREE_EXCEPTION" );
            return new TreeRequestAttempt(
                pathLabel,
                Result.Fail<IReadOnlyList<FileEntry>>( error ),
                httpElapsedMs,
                deserializeElapsedMs,
                mapElapsedMs,
                totalStopwatch.ElapsedMilliseconds,
                rawCount,
                mappedCount,
                statusCode,
                false
            );
        }
    }

    /// <summary>
    /// Tree API の試行結果詳細をデバッグログへ記録する。
    /// </summary>
    /// <param name="attempt">記録対象の試行結果。</param>
    private void LogTreeAttempt( TreeRequestAttempt attempt ) {
        _logger.Debug(
            $"GetTreeAsync 内訳。Path={attempt.PathLabel}, StatusCode={attempt.StatusCode?.ToString() ?? "null"}, TimedOut={attempt.TimedOut}, HttpElapsedMs={attempt.HttpElapsedMs}, DeserializeElapsedMs={attempt.DeserializeElapsedMs}, MapElapsedMs={attempt.MapElapsedMs}, RawCount={attempt.RawCount}, MappedCount={attempt.MappedCount}, TotalElapsedMs={attempt.TotalElapsedMs}"
        );
    }

    /// <summary>
    /// Tree API の最終結果を情報ログへ記録する。
    /// </summary>
    /// <param name="attempt">最終試行結果。</param>
    private void LogTreeSummary( TreeRequestAttempt attempt ) {
        if(attempt.Result.IsSuccess) {
            _logger.Info( $"GetTreeAsync が完了した。Path={attempt.PathLabel}, Count={attempt.MappedCount}, TotalElapsedMs={attempt.TotalElapsedMs}" );
            return;
        }

        var reason = attempt.Result.Errors.Count > 0
            ? attempt.Result.Errors[0].Message
            : "unknown";
        _logger.Info( $"GetTreeAsync が失敗した。Path={attempt.PathLabel}, TotalElapsedMs={attempt.TotalElapsedMs}, Reason={reason}" );
    }

    /// <summary>API用のファイル変更ペイロードに変換する。</summary>
    /// <param name="file">PRに含めるファイル情報。</param>
    /// <returns>変換結果。対応外の操作は null。</returns>
    private static PullRequestCreate.PullRequestCreate_files? ToFilePayload( ApiPullRequestFile file ) {
        ArgumentNullException.ThrowIfNull( file );

        return file.Operation switch
        {
            ApiPullRequestFileOperation.Upsert => new PullRequestCreate.PullRequestCreate_files
            {
                FileUpsert = new FileUpsert
                {
                    Operation = FileUpsert_operation.Upsert,
                    Path = file.Path,
                    Content = file.Content,
                },
            },
            ApiPullRequestFileOperation.Delete => new PullRequestCreate.PullRequestCreate_files
            {
                FileDelete = new FileDelete
                {
                    Operation = FileDelete_operation.Delete,
                    Path = file.Path,
                },
            },
            _ => null,
        };
    }

    /// <summary>PR作成レスポンスをアプリ内モデルに変換する。</summary>
    private static ApiCreatePullRequestEntry ToCreatePrEntry( PullRequestCreation payload ) {
        Uri? prUrl = null;
        if(payload.Url is { Length: > 0 } url && Uri.TryCreate( url, UriKind.Absolute, out var parsed )) prUrl = parsed;
        return new ApiCreatePullRequestEntry( payload.BranchName, payload.CommitSha, payload.Number, prUrl, null, payload.Reused ?? false );
    }

    /// <summary>ヘルスチェック応答のペイロード。</summary>
    /// <param name="Status">サービス状態。"ok" など。</param>
    /// <param name="Timestamp">サーバー時刻。</param>
    private sealed record HealthResponse(
        [property: JsonPropertyName( "status" )] string? Status,
        [property: JsonPropertyName( "timestamp" )] DateTimeOffset? Timestamp
    );

    /// <summary>APIクライアント依存をまとめる。</summary>
    /// <param name="HttpClient">HTTP送信用クライアント。</param>
    /// <param name="RequestAdapter">KiotaのHTTP変換アダプター。</param>
    /// <param name="ApiClient">Kiota生成のAPIクライアント。</param>
    private sealed record ApiClientContext( HttpClient HttpClient, HttpClientRequestAdapter RequestAdapter, DcsApiClient ApiClient );

    /// <summary>
    /// Tree API 試行結果を保持する。
    /// </summary>
    /// <param name="PathLabel">試行経路ラベル。</param>
    /// <param name="Result">実行結果。</param>
    /// <param name="HttpElapsedMs">HTTP 区間の経過時間。</param>
    /// <param name="DeserializeElapsedMs">逆シリアライズ区間の経過時間。</param>
    /// <param name="MapElapsedMs">マッピング区間の経過時間。</param>
    /// <param name="TotalElapsedMs">全体経過時間。</param>
    /// <param name="RawCount">受信件数。</param>
    /// <param name="MappedCount">変換件数。</param>
    /// <param name="StatusCode">HTTP ステータスコード。</param>
    /// <param name="TimedOut">タイムアウトで失敗したかどうか。</param>
    private sealed record TreeRequestAttempt(
        string PathLabel,
        Result<IReadOnlyList<FileEntry>> Result,
        long HttpElapsedMs,
        long DeserializeElapsedMs,
        long MapElapsedMs,
        long TotalElapsedMs,
        int RawCount,
        int MappedCount,
        int? StatusCode,
        bool TimedOut
    );

    /// <summary>/v2/health の疎通試行結果を保持する。</summary>
    /// <param name="Label">経路ラベル。</param>
    /// <param name="IsSuccess">成功したかどうか。</param>
    /// <param name="StatusCode">HTTPステータスコード。</param>
    /// <param name="ElapsedMs">経過時間。</param>
    private sealed record HealthProbeAttempt( string Label, bool IsSuccess, int? StatusCode, long ElapsedMs );

    /// <summary>API呼び出しに使う経路クライアントを保持する。</summary>
    /// <param name="Preference">選択した経路種別。</param>
    /// <param name="Label">経路ラベル。</param>
    /// <param name="Client">利用するクライアント。</param>
    private sealed record RouteClient( ApiRoutePreference Preference, string Label, HttpClient Client );
}