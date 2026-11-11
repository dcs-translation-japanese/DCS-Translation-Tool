namespace DcsTranslationTool.Application.Contracts;

/// <summary>Pull Request作成時のAPI応答を表現する</summary>
/// <param name="BranchName">作成されたブランチ名を示す</param>
/// <param name="CommitSha">作成されたコミットのSHAを示す</param>
/// <param name="PullRequestNumber">作成されたPull Requestの番号を示す</param>
/// <param name="PullRequestUrl">作成されたPull RequestのURLを示す</param>
/// <param name="Note">APIが返却した補足情報を保持する</param>
/// <param name="Reused">既存のPull Requestが再利用されたかを示す</param>
public sealed record ApiCreatePullRequestEntry( string? BranchName, string? CommitSha, int? PullRequestNumber, Uri? PullRequestUrl, string? Note, bool Reused = false );