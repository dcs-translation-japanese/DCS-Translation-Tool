using Caliburn.Micro;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Features.CoachMarks;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Resources;

namespace DcsTranslationTool.Presentation.Wpf.Features.Settings;

/// <summary>
/// フォルダー設定、操作案内の開始、アプリケーション情報の表示を管理する。
/// </summary>
/// <param name="applicationInfoService">アプリケーションのバージョン情報を提供するサービス。</param>
/// <param name="appSettingsService">設定の参照と永続化を行うサービス。</param>
/// <param name="dialogProvider">フォルダー選択などのシステムダイアログを表示するサービス。</param>
/// <param name="eventAggregator">画面間のイベントを配信するサービス。</param>
/// <param name="logger">操作やエラーを記録するサービス。</param>
/// <param name="systemService">ブラウザーなどの外部機能を呼び出すサービス。</param>
/// <param name="coachMarks">操作案内の進行を管理するサービス。指定しない場合は案内と連携しない。</param>
public class SettingsViewModel(
    IApplicationInfoService applicationInfoService,
    IAppSettingsService appSettingsService,
    IDialogProvider dialogProvider,
    IEventAggregator eventAggregator,
    ILoggingService logger,
    ISystemService systemService,
    CoachMarkService? coachMarks = null
) : Screen, IActivate {

    #region Fields

    /// <summary>操作案内の開始に失敗した場合に表示するエラーメッセージ。</summary>
    private string _coachMarkError = string.Empty;
    /// <summary>DCS World のインストールフォルダーのパスを保持する。</summary>
    private string _dcsWorldInstallDir = appSettingsService.Settings.DcsWorldInstallDir;
    /// <summary>ユーザーミッションフォルダーのパスを保持する。</summary>
    private string _sourceUserMissionDir = appSettingsService.Settings.SourceUserMissionDir;
    /// <summary>翻訳ファイルのダウンロード先フォルダーのパスを保持する。</summary>
    private string _translateFileDir = appSettingsService.Settings.TranslateFileDir;
    /// <summary>機体の翻訳注入先に外部フォルダーを使用するかどうかを保持する。</summary>
    private bool _useExternalAircraftInjectionDir = appSettingsService.Settings.UseExternalAircraftInjectionDir;
    /// <summary>機体の翻訳注入先に使用する外部フォルダーのパスを保持する。</summary>
    private string _externalAircraftInjectionDir = appSettingsService.Settings.ExternalAircraftInjectionDir;
    /// <summary>キャンペーンの翻訳注入先に外部フォルダーを使用するかどうかを保持する。</summary>
    private bool _useExternalCampaignInjectionDir = appSettingsService.Settings.UseExternalCampaignInjectionDir;
    /// <summary>キャンペーンの翻訳注入先に使用する外部フォルダーのパスを保持する。</summary>
    private string _externalCampaignInjectionDir = appSettingsService.Settings.ExternalCampaignInjectionDir;

    #endregion

    #region Properties
    /// <summary>画面で使用する操作案内サービス。操作案内を使用しない場合は null。</summary>
    public CoachMarkService? CoachMarks => coachMarks;
    /// <summary>操作案内の開始に失敗した場合に画面へ表示するエラーメッセージ。</summary>
    public string CoachMarkError {
        get => _coachMarkError;
        private set => Set( ref _coachMarkError, value );
    }

    /// <summary>インストールフォルダーの検証エラー。入力が有効な場合は null。</summary>
    public string? InstallDirectoryError => SettingsDirectoryValidation.GetError( "DcsWorldInstall", DcsWorldInstallDir );
    /// <summary>ユーザーミッションフォルダーの検証エラー。入力が有効な場合は null。</summary>
    public string? UserDirectoryError => SettingsDirectoryValidation.GetError( "UserMission", SourceUserMissionDir );
    /// <summary>ダウンロード先フォルダーの検証エラー。入力が有効な場合は null。</summary>
    public string? DownloadDirectoryError => SettingsDirectoryValidation.GetError( "TranslateFile", TranslateFileDir );

    /// <summary>
    /// 操作案内を最初から開始し、開始に必要な設定保存に失敗した場合は画面にエラーを表示する。
    /// </summary>
    /// <returns>処理の完了を表すタスク。</returns>
    public async Task StartCoachMark() {
        if(coachMarks is null) return;
        try {
            CoachMarkError = string.Empty;
            await coachMarks.StartAsync();
        }
        catch(Exception ex) {
            logger.Error( "操作案内の開始に必要な設定保存に失敗した。", ex );
            CoachMarkError = Strings_CoachMarks.StartSaveFailureMessage;
        }
    }

    /// <summary>DCS World のインストールフォルダーのパスを取得または設定する。</summary>
    public string DcsWorldInstallDir {
        get => _dcsWorldInstallDir;
        set {
            if(!Set( ref _dcsWorldInstallDir, value )) return;
            appSettingsService.Settings.DcsWorldInstallDir = value;
            NotifyOfPropertyChange( nameof( InstallDirectoryError ) );
            logger.Info( $"DcsWorldInstallDir を更新する。Value={value}" );
        }
    }

    /// <summary>ユーザーミッションフォルダーのパスを取得または設定する。</summary>
    public string SourceUserMissionDir {
        get => _sourceUserMissionDir;
        set {
            if(!Set( ref _sourceUserMissionDir, value )) return;
            appSettingsService.Settings.SourceUserMissionDir = value;
            NotifyOfPropertyChange( nameof( UserDirectoryError ) );
            logger.Info( $"SourceUserMissionDir を更新する。Value={value}" );
        }
    }

    /// <summary>翻訳ファイルのダウンロード先フォルダーのパスを取得または設定する。</summary>
    public string TranslateFileDir {
        get => _translateFileDir;
        set {
            if(!Set( ref _translateFileDir, value )) return;
            appSettingsService.Settings.TranslateFileDir = value;
            NotifyOfPropertyChange( nameof( DownloadDirectoryError ) );
            logger.Info( $"TranslateFileDir を更新する。Value={value}" );
        }
    }

    /// <summary>機体の翻訳注入先に外部フォルダーを使用するかどうかを取得または設定する。</summary>
    public bool UseExternalAircraftInjectionDir {
        get => _useExternalAircraftInjectionDir;
        set {
            if(!Set( ref _useExternalAircraftInjectionDir, value )) return;
            appSettingsService.Settings.UseExternalAircraftInjectionDir = value;
            logger.Info( $"UseExternalAircraftInjectionDir を更新する。Value={value}" );
        }
    }

    /// <summary>機体の翻訳注入先に使用する外部フォルダーのパスを取得または設定する。</summary>
    public string ExternalAircraftInjectionDir {
        get => _externalAircraftInjectionDir;
        set {
            if(!Set( ref _externalAircraftInjectionDir, value )) return;
            appSettingsService.Settings.ExternalAircraftInjectionDir = value;
            logger.Info( $"ExternalAircraftInjectionDir を更新する。Value={value}" );
        }
    }

    /// <summary>キャンペーンの翻訳注入先に外部フォルダーを使用するかどうかを取得または設定する。</summary>
    public bool UseExternalCampaignInjectionDir {
        get => _useExternalCampaignInjectionDir;
        set {
            if(!Set( ref _useExternalCampaignInjectionDir, value )) return;
            appSettingsService.Settings.UseExternalCampaignInjectionDir = value;
            logger.Info( $"UseExternalCampaignInjectionDir を更新する。Value={value}" );
        }
    }

    /// <summary>キャンペーンの翻訳注入先に使用する外部フォルダーのパスを取得または設定する。</summary>
    public string ExternalCampaignInjectionDir {
        get => _externalCampaignInjectionDir;
        set {
            if(!Set( ref _externalCampaignInjectionDir, value )) return;
            appSettingsService.Settings.ExternalCampaignInjectionDir = value;
            logger.Info( $"ExternalCampaignInjectionDir を更新する。Value={value}" );
        }
    }

    /// <summary>
    /// 画面に表示するバージョン情報
    /// </summary>
    public string VersionDescription { get; init; } = $"{Resources.Strings_Shared.AppDisplayName} - {applicationInfoService.GetVersion()}";

    /// <summary>画面に表示するアプリケーションのライセンス種別。</summary>
    public string LicenseType { get; init; } = appSettingsService.Settings.LicenseType;

    #endregion

    #region Lifecycle

    /// <summary>
    /// 設定画面のアクティブ化に伴う読み込み状態を通知する。
    /// </summary>
    /// <param name="cancellationToken">処理のキャンセル通知。</param>
    /// <returns>処理の完了を表すタスク。</returns>
    public async Task ActivateAsync( CancellationToken cancellationToken ) {
        logger.Info( "SettingsViewModel をアクティブ化する。" );
        await eventAggregator.PublishOnUIThreadAsync( "Loading", cancellationToken: cancellationToken );
        await eventAggregator.PublishOnUIThreadAsync( string.Empty, cancellationToken: cancellationToken );
    }

    /// <summary>
    /// 設定画面の非アクティブ化を通知する。
    /// </summary>
    /// <param name="close">画面を閉じる場合は true。非アクティブ化のみの場合は false。</param>
    /// <param name="cancellationToken">処理のキャンセル通知。</param>
    /// <returns>処理の完了を表すタスク。</returns>
    protected override async Task OnDeactivateAsync( bool close, CancellationToken cancellationToken ) {
        logger.Info( $"SettingsViewModel を非アクティブ化する。Close={close}" );
        await eventAggregator.PublishOnUIThreadAsync( "Closing", cancellationToken: cancellationToken );
    }

    #endregion

    #region Action Messages

    /// <summary>
    /// プライバシーステートメントを開くコマンド。
    /// </summary>
    public void BrowseToPrivacyStatement() {
        logger.Info( $"プライバシーステートメントを開く。Url={appSettingsService.Settings.PrivacyStatement}" );
        systemService.OpenInWebBrowser( appSettingsService.Settings.PrivacyStatement );
    }

    /// <summary>
    /// フォルダ選択を実行し、key に応じて該当プロパティを更新する。
    /// </summary>
    public void Browse( string path, string key ) {
        logger.Info( $"フォルダー選択を開始する。Path={path}, Key={key}" );
        if(dialogProvider.ShowFolderPicker( path, out var selected )) {
            switch(key) {
                case "DcsWorldInstall":
                    DcsWorldInstallDir = selected;
                    logger.Info( $"DcsWorldInstallDir ディレクトリを設定した。Selected={selected}" );
                    break;
                case "UserMission":
                    SourceUserMissionDir = selected;
                    logger.Info( $"SourceUserMissionDir ディレクトリを設定した。Selected={selected}" );
                    break;
                case "TranslateFile":
                    TranslateFileDir = selected;
                    logger.Info( $"TranslateFileDir ディレクトリを設定した。Selected={selected}" );
                    break;
                case "ExternalAircraftInjection":
                    ExternalAircraftInjectionDir = selected;
                    logger.Info( $"ExternalAircraftInjectionDir ディレクトリを設定した。Selected={selected}" );
                    break;
                case "ExternalCampaignInjection":
                    ExternalCampaignInjectionDir = selected;
                    logger.Info( $"ExternalCampaignInjectionDir ディレクトリを設定した。Selected={selected}" );
                    break;
                default:
                    logger.Warn( $"未対応のキーを受信した。Key={key}" );
                    break;
            }
        }
        else {
            logger.Info( "フォルダー選択がキャンセルされた。" );
        }
    }

    /// <summary>
    /// 最新リリースのページを既定のブラウザーで開く。
    /// </summary>
    public void BrowseToReleaseLatestPage() {
        logger.Info( $"最新リリースページを開く。Url={appSettingsService.Settings.ReleaseLatestUrl}" );
        systemService.OpenInWebBrowser( appSettingsService.Settings.ReleaseLatestUrl );
    }

    /// <summary>
    /// ライセンスのページを既定のブラウザーで開く。
    /// </summary>
    public void BrowseToLicensePage() {
        logger.Info( $"ライセンスページを開く。Url={appSettingsService.Settings.LicenseUrl}" );
        systemService.OpenInWebBrowser( appSettingsService.Settings.LicenseUrl );
    }

    #endregion

}