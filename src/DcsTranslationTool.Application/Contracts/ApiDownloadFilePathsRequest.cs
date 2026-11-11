namespace DcsTranslationTool.Application.Contracts;

/// <summary>複数ファイルダウンロードAPIの入力を表現する</summary>
/// <param name="Paths">ダウンロード対象のパス一覧を表現する</param>
/// <param name="ETag">旧APIのETag。v2は条件付き取得に対応しないため送信しない</param>
public sealed record ApiDownloadFilePathsRequest( IReadOnlyList<string> Paths, string? ETag );