namespace DcsTranslationTool.Application.Contracts;

/// <summary>複数ファイルダウンロードパスAPIの結果を表現する</summary>
/// <param name="Items">ダウンロードURLとリポジトリ上のパスのコレクション</param>
/// <param name="ETag">取得結果のETag。v2では常にnull</param>
public sealed record ApiDownloadFilePathsResult( IReadOnlyCollection<ApiDownloadFilePathsItem> Items, string? ETag );

/// <summary>ダウンロードURLとリポジトリ上のパスを表現する</summary>
/// <param name="Url">ダウンロードURL</param>
/// <param name="Path">ダウンロード対象のリポジトリ上のパス</param>
public sealed record ApiDownloadFilePathsItem( string Url, string Path );