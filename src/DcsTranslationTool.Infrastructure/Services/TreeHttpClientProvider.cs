using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;

using DcsTranslationTool.Infrastructure.Interfaces;

namespace DcsTranslationTool.Infrastructure.Services;

/// <summary>
/// Tree API 用の default / IPv4 優先クライアントを供給する。
/// </summary>
public sealed class TreeHttpClientProvider(
    HttpClient? defaultClient = null,
    HttpClient? ipv4PreferredClient = null,
    HttpClient? ipv6PreferredClient = null
) : ITreeHttpClientProvider {
    private const string DefaultBaseUrl = "https://api.dcs-translation-japanese.workers.dev/";

    private readonly TreeClientBundle _bundle = CreateBundle( defaultClient, ipv4PreferredClient, ipv6PreferredClient );

    /// <inheritdoc />
    public HttpClient DefaultClient => this._bundle.DefaultClient;

    /// <inheritdoc />
    public HttpClient Ipv4PreferredClient => this._bundle.Ipv4PreferredClient;

    /// <inheritdoc />
    public HttpClient Ipv6PreferredClient => this._bundle.Ipv6PreferredClient;

    /// <inheritdoc />
    public bool IsIpv4PreferredDedicated => this._bundle.IsIpv4PreferredDedicated;

    /// <inheritdoc />
    public bool IsIpv6PreferredDedicated => this._bundle.IsIpv6PreferredDedicated;

    /// <summary>
    /// 呼び出し条件に応じて default / IPv4 優先クライアントを組み立てる。
    /// </summary>
    /// <param name="defaultClient">既定経路の外部クライアント。</param>
    /// <param name="ipv4PreferredClient">IPv4 優先経路の外部クライアント。</param>
    /// <param name="ipv6PreferredClient">IPv6 優先経路の外部クライアント。</param>
    /// <returns>初期化済みクライアント束。</returns>
    private static TreeClientBundle CreateBundle( HttpClient? defaultClient, HttpClient? ipv4PreferredClient, HttpClient? ipv6PreferredClient ) {
        var normalizedDefaultClient = InitializeClient( defaultClient );
        var normalizedIpv4Client = ResolvePreferredClient(
            normalizedDefaultClient,
            defaultClient,
            ipv4PreferredClient,
            AddressFamily.InterNetwork
        );
        var normalizedIpv6Client = ResolvePreferredClient(
            normalizedDefaultClient,
            defaultClient,
            ipv6PreferredClient,
            AddressFamily.InterNetworkV6
        );

        return new TreeClientBundle(
            normalizedDefaultClient,
            normalizedIpv4Client,
            normalizedIpv6Client,
            !ReferenceEquals( normalizedDefaultClient, normalizedIpv4Client ),
            !ReferenceEquals( normalizedDefaultClient, normalizedIpv6Client )
        );
    }

    /// <summary>
    /// 優先経路クライアントを解決する。
    /// </summary>
    /// <param name="defaultClient">既定経路クライアント。</param>
    /// <param name="injectedDefaultClient">外部注入された既定経路クライアント。</param>
    /// <param name="preferredClient">外部注入された優先経路クライアント。</param>
    /// <param name="addressFamily">優先するアドレスファミリー。</param>
    /// <returns>解決したクライアント。</returns>
    private static HttpClient ResolvePreferredClient(
        HttpClient defaultClient,
        HttpClient? injectedDefaultClient,
        HttpClient? preferredClient,
        AddressFamily addressFamily
    ) {
        if(preferredClient is not null)
            return InitializeClient( preferredClient, defaultClient );

        if(injectedDefaultClient is not null)
            return defaultClient;

        return InitializePreferredClient( defaultClient, addressFamily );
    }

    /// <summary>
    /// 渡された <see cref="HttpClient"/> を初期化する。未指定時は新規生成する。
    /// </summary>
    /// <param name="client">初期化対象クライアント。</param>
    /// <param name="fallbackClient">既定値継承元クライアント。</param>
    /// <returns>初期化済み <see cref="HttpClient"/>。</returns>
    private static HttpClient InitializeClient( HttpClient? client, HttpClient? fallbackClient = null ) {
        var resolvedClient = client ?? new HttpClient();

        resolvedClient.BaseAddress ??= fallbackClient?.BaseAddress ?? new Uri( DefaultBaseUrl );
        if(resolvedClient.DefaultRequestHeaders.Accept.All( header => header.MediaType != "application/json" ))
            resolvedClient.DefaultRequestHeaders.Accept.Add( new MediaTypeWithQualityHeaderValue( "application/json" ) );

        return resolvedClient;
    }

    /// <summary>
    /// 指定アドレスファミリー優先の接続設定を持つ <see cref="HttpClient"/> を初期化する。
    /// </summary>
    /// <param name="sourceClient">既定値継承元クライアント。</param>
    /// <param name="preferredAddressFamily">優先するアドレスファミリー。</param>
    /// <returns>初期化済み <see cref="HttpClient"/>。</returns>
    private static HttpClient InitializePreferredClient( HttpClient sourceClient, AddressFamily preferredAddressFamily ) {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            PooledConnectionLifetime = TimeSpan.FromMinutes( 5 ),
            ConnectTimeout = TimeSpan.FromSeconds( 10 ),
            ConnectCallback = async ( context, token ) => {
                var addresses = await Dns.GetHostAddressesAsync( context.DnsEndPoint.Host, token ).ConfigureAwait( false );
                var preferredAddresses = addresses
                    .Where( address => address.AddressFamily == preferredAddressFamily )
                    .ToArray();
                var candidates = preferredAddresses.Length > 0 ? preferredAddresses : addresses;

                Exception? lastException = null;
                foreach(var address in candidates) {
                    var socket = new Socket( address.AddressFamily, SocketType.Stream, ProtocolType.Tcp );
                    try {
                        await socket.ConnectAsync( new IPEndPoint( address, context.DnsEndPoint.Port ), token ).ConfigureAwait( false );
                        return new NetworkStream( socket, ownsSocket: true );
                    }
                    catch(Exception ex) {
                        lastException = ex;
                        socket.Dispose();
                    }
                }

                throw new HttpRequestException(
                    $"接続可能なアドレスが見つからない。Host={context.DnsEndPoint.Host}, AddressFamily={preferredAddressFamily}",
                    lastException
                );
            },
        };

        var client = new HttpClient( handler )
        {
            BaseAddress = sourceClient.BaseAddress ?? new Uri( DefaultBaseUrl ),
            Timeout = sourceClient.Timeout,
            DefaultRequestVersion = sourceClient.DefaultRequestVersion,
            DefaultVersionPolicy = sourceClient.DefaultVersionPolicy,
            MaxResponseContentBufferSize = sourceClient.MaxResponseContentBufferSize,
        };

        foreach(var header in sourceClient.DefaultRequestHeaders) {
            client.DefaultRequestHeaders.TryAddWithoutValidation( header.Key, header.Value );
        }

        if(client.DefaultRequestHeaders.Accept.All( header => header.MediaType != "application/json" ))
            client.DefaultRequestHeaders.Accept.Add( new MediaTypeWithQualityHeaderValue( "application/json" ) );

        return client;
    }

    /// <summary>
    /// Tree API に利用するクライアント束を表す。
    /// </summary>
    /// <param name="DefaultClient">既定経路クライアント。</param>
    /// <param name="Ipv4PreferredClient">IPv4 優先経路クライアント。</param>
    /// <param name="Ipv6PreferredClient">IPv6 優先経路クライアント。</param>
    /// <param name="IsIpv4PreferredDedicated">IPv4 優先経路が専用クライアントかどうか。</param>
    /// <param name="IsIpv6PreferredDedicated">IPv6 優先経路が専用クライアントかどうか。</param>
    private sealed record TreeClientBundle(
        HttpClient DefaultClient,
        HttpClient Ipv4PreferredClient,
        HttpClient Ipv6PreferredClient,
        bool IsIpv4PreferredDedicated,
        bool IsIpv6PreferredDedicated
    );
}