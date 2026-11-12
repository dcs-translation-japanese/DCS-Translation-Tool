using System.Net.Http.Headers;

using WireMock.Server;

namespace DcsTranslationTool.IntegrationTests.Infrastructure.Services.WireMock;

/// <summary>
/// WireMock サーバーと対応する HTTP クライアントをまとめて扱う。
/// </summary>
public sealed class WireMockTestServer : IDisposable {

    /// <summary>
    /// WireMock サーバーを起動する。
    /// </summary>
    public WireMockTestServer() {
        this.Server = WireMockServer.Start();
        this.BaseUri = new Uri( this.Server.Urls[0] );
    }

    /// <summary>
    /// サーバーのベース URL を返す。
    /// </summary>
    public Uri BaseUri { get; }

    /// <summary>
    /// サーバー本体を返す。
    /// </summary>
    public WireMockServer Server { get; }

    /// <summary>
    /// テスト用 HTTP クライアントを生成する。
    /// </summary>
    /// <returns>生成した HTTP クライアントを返す。</returns>
    public HttpClient CreateHttpClient() {
        var client = new HttpClient
        {
            BaseAddress = BaseUri,
        };
        client.DefaultRequestHeaders.Accept.Add( new MediaTypeWithQualityHeaderValue( "application/json" ) );
        return client;
    }

    /// <summary>
    /// 使用したリソースを破棄する。
    /// </summary>
    public void Dispose() {
        this.Server.Stop();
        this.Server.Dispose();
        GC.SuppressFinalize( this );
    }
}