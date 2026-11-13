namespace DcsTranslationTool.Presentation.Wpf.Features.CoachMarks;

/// <summary>
/// 初期設定、.miz 注入、Tips、完了までの操作案内の段階を表す。
/// </summary>
public enum CoachMarkStep {
    /// <summary>操作案内を表示していない状態。</summary>
    Inactive,
    /// <summary>操作案内の説明を表示し、開始またはスキップを待つ段階。</summary>
    Introduction,
    /// <summary>DCS World のインストールフォルダーの入力を案内する段階。</summary>
    InstallDirectory,
    /// <summary>ユーザーミッションフォルダーの入力を案内する段階。</summary>
    UserDirectory,
    /// <summary>翻訳ファイルのダウンロード先の入力を案内する段階。</summary>
    DownloadDirectory,
    /// <summary>機体の翻訳注入先の推奨設定を案内する段階。</summary>
    AircraftRecommendation,
    /// <summary>キャンペーンの翻訳注入先の推奨設定を案内する段階。</summary>
    CampaignRecommendation,
    /// <summary>TF-51D の翻訳を選択する操作を案内する段階。</summary>
    SelectTranslation,
    /// <summary>選択した翻訳のダウンロードと適用を案内する段階。</summary>
    ApplyTranslation,
    /// <summary>翻訳の適用成功を確認する段階。</summary>
    TranslationApplied,
    /// <summary>ダウンロード画面の使い方を紹介する段階。</summary>
    DownloadTip,
    /// <summary>翻訳作成機能を紹介する段階。</summary>
    TranslationTip,
    /// <summary>翻訳ファイルのアップロード機能を紹介する段階。</summary>
    UploadTip,
    /// <summary>操作案内の終了と再表示方法を案内する段階。</summary>
    Completed,
}