using Caliburn.Micro;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Features.Settings;
using DcsTranslationTool.Resources;

namespace DcsTranslationTool.Presentation.Wpf.Features.CoachMarks;

/// <summary>
/// 操作案内の進行と初回表示の保存を管理する。
/// </summary>
/// <param name="appSettingsService">設定の参照と永続化を行うサービス。</param>
public sealed class CoachMarkService( IAppSettingsService appSettingsService ) : PropertyChangedBase {
    /// <summary>操作案内で翻訳対象として選択する TF-51D の相対パス。</summary>
    /// <remarks>TF-51D は標準で付属する初期機体で、すべてのユーザーが所持しているため、操作案内の対象に使用する。</remarks>
    internal const string TranslationSelectionPath = "DCSWorld/Mods/aircraft/TF-51D";

    /// <summary>非表示状態と導入を除いた、進捗番号を付ける案内段階の一覧。</summary>
    private static readonly CoachMarkStep[] ProgressSteps = Enum.GetValues<CoachMarkStep>()
        .Where( step => step is not (CoachMarkStep.Inactive or CoachMarkStep.Introduction) ).ToArray();

    /// <summary>現在の案内の段階。</summary>
    private CoachMarkStep _step;

    /// <summary>開始または次の段階への遷移に必要な設定を保存中かどうか。</summary>
    private bool _busy;

    /// <summary>保存待機中に案内がスキップされたことを判別するための世代番号。</summary>
    private int _session;

    /// <summary>入力後もダウンロード先の案内を進捗に含めるため、表示済みかどうかを保持する。</summary>
    private bool _downloadDirectoryShown;

    /// <summary>進行中の案内に対する注入結果を集計中かどうか。</summary>
    private bool _applying;

    /// <summary>集計中の注入処理で実際の .miz への書き込みが成功したかどうか。</summary>
    private bool _injectedMiz;

    /// <summary>集計中の注入処理に失敗したグループが含まれるかどうか。</summary>
    private bool _applyFailed;

    /// <summary>案内に表示する現在のエラーメッセージ。</summary>
    private string _errorMessage = string.Empty;

    /// <summary>現在の案内の段階を取得する。</summary>
    public CoachMarkStep Step => _step;

    /// <summary>完了画面を含め、案内が表示中かどうかを取得する。</summary>
    public bool IsActive => Step != CoachMarkStep.Inactive;

    /// <summary>現在の段階が必須フォルダーまたは推奨注入先の設定案内かどうかを取得する。</summary>
    public bool IsSettingsStep => Step is >= CoachMarkStep.InstallDirectory and <= CoachMarkStep.CampaignRecommendation;

    /// <summary>Tips を MainView で表示する段階かどうかを取得する。</summary>
    public bool IsMainTip => Step is CoachMarkStep.TranslationTip or CoachMarkStep.UploadTip;

    /// <summary>完了案内を表示してスキップできるかどうかを取得する。</summary>
    public bool CanSkip => IsActive && Step != CoachMarkStep.Completed;

    /// <summary>保存中ではなく、開始、次へ、または閉じる操作を実行できるかどうか。</summary>
    public bool CanNext => !_busy && (IsSettingsStep || Step == CoachMarkStep.Introduction || Step >= CoachMarkStep.TranslationApplied);

    /// <summary>案内に表示する現在のエラーメッセージを取得する。</summary>
    public string ErrorMessage => _errorMessage;

    /// <summary>現在の段階に対応する開始、次へ、または閉じるボタンの表示文言を取得する。</summary>
    public string NextButtonText => Step switch
    {
        CoachMarkStep.Introduction => Strings_CoachMarks.BeginButtonText,
        CoachMarkStep.Completed => Strings_CoachMarks.CloseButtonText,
        _ => Strings_CoachMarks.NextButtonText,
    };

    /// <summary>強調表示する画面要素の識別子を取得する。対象がない場合は空文字列を返す。</summary>
    public string Target => Step switch
    {
        CoachMarkStep.InstallDirectory => "DcsWorldInstall",
        CoachMarkStep.UserDirectory => "UserMission",
        CoachMarkStep.DownloadDirectory => "TranslateFile",
        CoachMarkStep.AircraftRecommendation => "ExternalAircraftInjection",
        CoachMarkStep.CampaignRecommendation => "ExternalCampaignInjection",
        CoachMarkStep.SelectTranslation => TranslationSelectionPath,
        CoachMarkStep.ApplyTranslation => "ApplyTranslation",
        CoachMarkStep.DownloadTip => "DownloadTranslation",
        CoachMarkStep.TranslationTip => "OpenTranslation",
        CoachMarkStep.UploadTip => "OpenUpload",
        _ => string.Empty,
    };

    /// <summary>現在の段階の進捗を含む見出し、または完了画面の見出しを取得する。</summary>
    public string Title {
        get {
            var title = Step switch
            {
                CoachMarkStep.Introduction => Strings_CoachMarks.IntroductionTitle,
                CoachMarkStep.InstallDirectory => Strings_CoachMarks.InstallDirectoryTitle,
                CoachMarkStep.UserDirectory => Strings_CoachMarks.UserDirectoryTitle,
                CoachMarkStep.DownloadDirectory => Strings_CoachMarks.DownloadDirectoryTitle,
                CoachMarkStep.AircraftRecommendation => Strings_CoachMarks.AircraftRecommendationTitle,
                CoachMarkStep.CampaignRecommendation => Strings_CoachMarks.CampaignRecommendationTitle,
                CoachMarkStep.SelectTranslation => Strings_CoachMarks.SelectTranslationTitle,
                CoachMarkStep.ApplyTranslation => Strings_CoachMarks.ApplyTranslationTitle,
                CoachMarkStep.TranslationApplied => Strings_CoachMarks.TranslationAppliedTitle,
                CoachMarkStep.DownloadTip => Strings_CoachMarks.DownloadTipTitle,
                CoachMarkStep.TranslationTip => Strings_CoachMarks.TranslationTipTitle,
                CoachMarkStep.UploadTip => Strings_CoachMarks.UploadTipTitle,
                CoachMarkStep.Completed => Strings_CoachMarks.CompletedTitle,
                _ => string.Empty,
            };
            var steps = ProgressSteps.Where( step => step != CoachMarkStep.DownloadDirectory ||
                _downloadDirectoryShown || SettingsDirectoryValidation.GetError( "TranslateFile", appSettingsService.Settings.TranslateFileDir ) is not null ).ToArray();
            var index = Array.IndexOf( steps, Step );
            return index < 0 ? title : $"{index + 1} / {steps.Length}　{title}";
        }
    }

    /// <summary>現在の段階で必要な操作、または完了を伝える説明文を取得する。</summary>
    public string Description => Step switch
    {
        CoachMarkStep.Introduction => Strings_CoachMarks.IntroductionDescription,
        CoachMarkStep.InstallDirectory => Strings_CoachMarks.InstallDirectoryDescription,
        CoachMarkStep.UserDirectory => Strings_CoachMarks.UserDirectoryDescription,
        CoachMarkStep.DownloadDirectory => Strings_CoachMarks.DownloadDirectoryDescription,
        CoachMarkStep.AircraftRecommendation => Strings_CoachMarks.AircraftRecommendationDescription,
        CoachMarkStep.CampaignRecommendation => Strings_CoachMarks.CampaignRecommendationDescription,
        CoachMarkStep.SelectTranslation => Strings_CoachMarks.SelectTranslationDescription,
        CoachMarkStep.ApplyTranslation => Strings_CoachMarks.ApplyTranslationDescription,
        CoachMarkStep.TranslationApplied => Strings_CoachMarks.TranslationAppliedDescription,
        CoachMarkStep.DownloadTip => Strings_CoachMarks.DownloadTipDescription,
        CoachMarkStep.TranslationTip => Strings_CoachMarks.TranslationTipDescription,
        CoachMarkStep.UploadTip => Strings_CoachMarks.UploadTipDescription,
        CoachMarkStep.Completed => Strings_CoachMarks.CompletedDescription,
        _ => string.Empty,
    };

    /// <summary>
    /// 表示済み状態を保存して案内を最初から開始する。自動起動の場合は表示済みのユーザーを除外する。
    /// </summary>
    /// <param name="automatic">初回自動表示の条件を適用する場合は true。手動で開始する場合は false。</param>
    /// <returns>処理の完了を表すタスク。</returns>
    public async Task StartAsync( bool automatic = false ) {
        if(_busy || (automatic && appSettingsService.Settings.HasSeenCoachMark)) return;
        _busy = true;
        NotifyOfPropertyChange( nameof( CanNext ) );
        var session = ++_session;
        var wasSeen = appSettingsService.Settings.HasSeenCoachMark;
        try {
            // 途中終了でも再表示しないため、表示前に保存を完了する。
            appSettingsService.Settings.HasSeenCoachMark = true;
            await appSettingsService.SaveAsync();
            if(session != _session) return;
            _applying = false;
            _downloadDirectoryShown = false;
            MoveTo( CoachMarkStep.Inactive );
            MoveTo( automatic ? CoachMarkStep.Introduction : CoachMarkStep.InstallDirectory );
        }
        catch {
            appSettingsService.Settings.HasSeenCoachMark = wasSeen;
            throw;
        }
        finally {
            _busy = false;
            NotifyOfPropertyChange( nameof( CanNext ) );
        }
    }

    /// <summary>
    /// 現在の設定を検証・保存して案内を進める。完了画面では案内を閉じる。
    /// </summary>
    /// <returns>処理の完了を表すタスク。</returns>
    public async Task NextAsync() {
        if(!CanNext) return;
        if(Step == CoachMarkStep.Introduction) {
            MoveTo( CoachMarkStep.InstallDirectory );
            return;
        }
        if(Step == CoachMarkStep.Completed) {
            MoveTo( CoachMarkStep.Inactive );
            return;
        }
        if(Step >= CoachMarkStep.TranslationApplied) {
            MoveTo( Step switch
            {
                CoachMarkStep.TranslationApplied => CoachMarkStep.DownloadTip,
                CoachMarkStep.DownloadTip => CoachMarkStep.TranslationTip,
                CoachMarkStep.TranslationTip => CoachMarkStep.UploadTip,
                _ => CoachMarkStep.Completed,
            } );
            return;
        }

        var step = Step;
        var settings = appSettingsService.Settings;
        var error = Step switch
        {
            CoachMarkStep.InstallDirectory => SettingsDirectoryValidation.GetError( "DcsWorldInstall", settings.DcsWorldInstallDir ),
            CoachMarkStep.UserDirectory => SettingsDirectoryValidation.GetError( "UserMission", settings.SourceUserMissionDir ),
            CoachMarkStep.DownloadDirectory => SettingsDirectoryValidation.GetError( "TranslateFile", settings.TranslateFileDir ),
            _ => null,
        };
        if(error is not null) {
            SetError( error );
            return;
        }

        _busy = true;
        NotifyOfPropertyChange( nameof( CanNext ) );
        try {
            await appSettingsService.SaveAsync();
            if(Step != step) return; // 保存中のスキップを取り消さない。
            if(step == CoachMarkStep.InstallDirectory) {
                MoveTo( CoachMarkStep.UserDirectory );
            }
            else if(step < CoachMarkStep.AircraftRecommendation) {
                MoveTo( FirstMissingRequiredStep() ?? CoachMarkStep.AircraftRecommendation );
            }
            else if(step == CoachMarkStep.AircraftRecommendation) {
                MoveTo( CoachMarkStep.CampaignRecommendation );
            }
            else {
                MoveTo( FirstMissingRequiredStep() ?? CoachMarkStep.SelectTranslation );
            }
        }
        finally {
            _busy = false;
            NotifyOfPropertyChange( nameof( CanNext ) );
        }
    }

    /// <summary>
    /// 必須フォルダー設定のうち、最初に検証を通過しない項目の段階を取得する。
    /// </summary>
    /// <returns>設定が不正な最初の段階。すべて有効な場合は null。</returns>
    private CoachMarkStep? FirstMissingRequiredStep() {
        var settings = appSettingsService.Settings;
        if(SettingsDirectoryValidation.GetError( "DcsWorldInstall", settings.DcsWorldInstallDir ) is not null) return CoachMarkStep.InstallDirectory;
        if(SettingsDirectoryValidation.GetError( "UserMission", settings.SourceUserMissionDir ) is not null) return CoachMarkStep.UserDirectory;
        if(SettingsDirectoryValidation.GetError( "TranslateFile", settings.TranslateFileDir ) is not null) return CoachMarkStep.DownloadDirectory;
        return null;
    }

    /// <summary>
    /// 保存済みの設定を保持したまま案内をスキップし、完了案内を表示する。
    /// </summary>
    public void Skip() {
        if(!IsActive) return;
        _session++;
        _applying = false;
        MoveTo( CoachMarkStep.Completed );
    }

    /// <summary>
    /// 選択中の翻訳に応じて選択または適用の案内へ進める。
    /// </summary>
    /// <param name="hasMizTranslation">選択したファイルに .miz 内の翻訳が含まれるかどうか。</param>
    public void UpdateSelection( bool hasMizTranslation ) {
        if(Step is not (CoachMarkStep.SelectTranslation or CoachMarkStep.ApplyTranslation) || _applying) return;
        if(!hasMizTranslation) MoveTo( CoachMarkStep.SelectTranslation );
        else if(Step == CoachMarkStep.SelectTranslation) MoveTo( CoachMarkStep.ApplyTranslation );
    }

    /// <summary>
    /// 進行中の案内に対する注入結果の集計を開始する。
    /// </summary>
    public void BeginApply() {
        _applying = Step is CoachMarkStep.SelectTranslation or CoachMarkStep.ApplyTranslation;
        _injectedMiz = false;
        _applyFailed = false;
    }

    /// <summary>
    /// 注入処理のグループごとに、実際の .miz 注入成功と失敗の有無を集計する。
    /// </summary>
    /// <param name="injectedMiz">実際の .miz への注入に成功したかどうか。</param>
    /// <param name="failed">注入処理に失敗が含まれるかどうか。</param>
    public void RecordApplyResult( bool injectedMiz, bool failed ) {
        if(!_applying) return;
        _injectedMiz |= injectedMiz;
        _applyFailed |= failed;
    }

    /// <summary>
    /// 処理全体と実際の .miz 注入が成功し、失敗がない場合に案内を完了する。
    /// </summary>
    /// <param name="succeeded">注入ワークフロー全体が成功したかどうか。</param>
    public void ApplyFinished( bool succeeded ) {
        var completed = _applying && succeeded && _injectedMiz && !_applyFailed;
        _applying = false;
        if(completed) MoveTo( CoachMarkStep.TranslationApplied );
    }

    /// <summary>
    /// 案内のエラーメッセージを更新して表示へ通知する。
    /// </summary>
    /// <param name="message">画面に表示するエラーメッセージ。</param>
    private void SetError( string message ) {
        _errorMessage = message;
        NotifyOfPropertyChange( nameof( ErrorMessage ) );
    }

    /// <summary>
    /// 案内の段階を切り替え、関連する表示プロパティの変更を通知する。
    /// </summary>
    /// <param name="step">対象となる案内の段階。</param>
    private void MoveTo( CoachMarkStep step ) {
        if(_step == step) return;
        _step = step;
        if(step == CoachMarkStep.DownloadDirectory) _downloadDirectoryShown = true;
        SetError( string.Empty );
        NotifyOfPropertyChange( nameof( Step ) );
        NotifyOfPropertyChange( nameof( IsActive ) );
        NotifyOfPropertyChange( nameof( IsSettingsStep ) );
        NotifyOfPropertyChange( nameof( IsMainTip ) );
        NotifyOfPropertyChange( nameof( CanSkip ) );
        NotifyOfPropertyChange( nameof( CanNext ) );
        NotifyOfPropertyChange( nameof( Target ) );
        NotifyOfPropertyChange( nameof( Title ) );
        NotifyOfPropertyChange( nameof( Description ) );
        NotifyOfPropertyChange( nameof( NextButtonText ) );
    }
}