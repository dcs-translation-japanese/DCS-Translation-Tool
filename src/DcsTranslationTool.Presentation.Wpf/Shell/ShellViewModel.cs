using System.ComponentModel;
using System.Windows.Navigation;
using System.Windows.Threading;

using Caliburn.Micro;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Presentation.Wpf.Features.CoachMarks;
using DcsTranslationTool.Presentation.Wpf.Features.Download;
using DcsTranslationTool.Presentation.Wpf.Features.Main;
using DcsTranslationTool.Presentation.Wpf.Features.Settings;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Results;
using DcsTranslationTool.Resources;
using DcsTranslationTool.Shared.Constants;

using MaterialDesignThemes.Wpf;

namespace DcsTranslationTool.Presentation.Wpf.Shell;

/// <summary>
/// シェルの画面遷移、操作案内、アプリケーション更新を管理する。
/// </summary>
/// <param name="appSettingsService">設定の参照と永続化を行うサービス。</param>
/// <param name="eventAggregator">画面間のイベントを配信するサービス。</param>
/// <param name="logger">操作やエラーを記録するサービス。</param>
/// <param name="navigationService">画面遷移とナビゲーション履歴を管理するサービス。</param>
/// <param name="snackbarService">通知メッセージを表示するサービス。</param>
/// <param name="systemService">ブラウザーなどの外部機能を呼び出すサービス。</param>
/// <param name="updateCheckService">アプリケーションの更新を確認するサービス。</param>
/// <param name="applicationUpdateService">更新の準備と適用を行うサービス。</param>
/// <param name="dialogService">確認や更新進捗のダイアログを表示するサービス。</param>
/// <param name="applicationRestartCoordinator">再起動前に編集中のウィンドウの終了を調整するサービス。</param>
/// <param name="coachMarks">操作案内の進行を管理するサービス。指定しない場合は案内と連携しない。</param>
public class ShellViewModel(
    IAppSettingsService appSettingsService,
    IEventAggregator eventAggregator,
    ILoggingService logger,
    INavigationService navigationService,
    ISnackbarService snackbarService,
    ISystemService systemService,
    IUpdateCheckService updateCheckService,
    IApplicationUpdateService? applicationUpdateService = null,
    IDialogService? dialogService = null,
    IApplicationRestartCoordinator? applicationRestartCoordinator = null,
    CoachMarkService? coachMarks = null
) : Conductor<IScreen>.Collection.OneActive, IHandle<string>, IActivate {

    #region Fields

    /// <summary>メインウィンドウの初期化が完了したかどうかを保持する。</summary>
    private bool _initialized = false;
    /// <summary>起動時の更新確認を開始済みかどうかを保持する。</summary>
    private bool _startupUpdateCheckStarted = false;
    /// <summary>アプリケーションの更新処理が実行中かどうかを保持する。</summary>
    private bool _applicationUpdateInProgress = false;
    /// <summary>メインウィンドウの幅を保持する。</summary>
    private double _shellWidth = appSettingsService.Settings.ShellWidth;
    /// <summary>メインウィンドウの高さを保持する。</summary>
    private double _shellHeight = appSettingsService.Settings.ShellHeight;

    #endregion

    #region Properties

    /// <summary>
    /// シェルウィンドウの幅
    /// </summary>
    public double ShellWidth {
        get => _shellWidth;
        set {
            _shellWidth = value;
            appSettingsService.Settings.ShellWidth = value;
            NotifyOfPropertyChange( nameof( ShellWidth ) );
        }
    }

    /// <summary>
    /// シェルウィンドウの高さ
    /// </summary>
    public double ShellHeight {
        get => _shellHeight;
        set {
            _shellHeight = value;
            appSettingsService.Settings.ShellHeight = value;
            NotifyOfPropertyChange( nameof( ShellHeight ) );
        }
    }

    /// <summary>画面で使用する操作案内サービス。操作案内を使用しない場合は null。</summary>
    public CoachMarkService? CoachMarks => coachMarks;

    /// <summary>現在の画面でユーザーに操作してもらう案内対象。</summary>
    public string CoachMarkTarget => CoachMarkNavigationTarget ?? coachMarks?.Target ?? string.Empty;

    /// <summary>画面遷移が必要な場合は、その操作を案内する見出し。</summary>
    public string CoachMarkTitle => CoachMarkNavigationTarget switch
    {
        "OpenSettings" => Strings_CoachMarks.OpenSettingsTitle,
        "GoBack" => Strings_CoachMarks.GoBackTitle,
        "OpenDownload" => Strings_CoachMarks.OpenDownloadTitle,
        _ => coachMarks?.Title ?? string.Empty,
    };

    /// <summary>画面遷移が必要な場合は、その操作を案内する説明文。</summary>
    public string CoachMarkDescription => CoachMarkNavigationTarget switch
    {
        "OpenSettings" => Strings_CoachMarks.OpenSettingsDescription,
        "GoBack" => Strings_CoachMarks.GoBackDescription,
        "OpenDownload" => Strings_CoachMarks.OpenDownloadDescription,
        _ => coachMarks?.Description ?? string.Empty,
    };

    /// <summary>案内対象の画面を開き、現在の段階を進められるかどうか。</summary>
    public bool CanNextCoachMark => CoachMarkNavigationTarget is null && coachMarks?.CanNext == true;

    /// <summary>現在の案内段階に必要な画面移動ボタンの識別子。画面移動が不要な場合は null。</summary>
    private string? CoachMarkNavigationTarget {
        get {
            if(coachMarks?.IsActive != true || coachMarks.Step is CoachMarkStep.Introduction or CoachMarkStep.Completed) return null;
            if(coachMarks.IsSettingsStep) return navigationService.CurrentContent is SettingsView ? null : "OpenSettings";
            if(coachMarks.IsMainTip) return navigationService.CurrentContent is MainView ? null : "GoBack";
            if(navigationService.CurrentContent is DownloadView) return null;
            return navigationService.CurrentContent is MainView ? "OpenDownload" : "GoBack";
        }
    }

    /// <summary>履歴内の前の画面へ戻れるかどうかを取得する。</summary>
    public bool CanGoBack => navigationService.CanGoBack;

    /// <summary>履歴内の次の画面へ進めるかどうかを取得する。</summary>
    public bool CanGoForward => navigationService.CanGoForward;

    /// <summary>Snackbar のメッセージキュー</summary>
    public ISnackbarMessageQueue MessageQueue => snackbarService.MessageQueue;

    #endregion

    #region Lifecycle

    /// <summary>
    /// シェルビューの読み込み完了を記録する。
    /// </summary>
    /// <param name="view">読み込みが完了したビュー。</param>
    protected override void OnViewLoaded( object view ) {
        base.OnViewLoaded( view );
        logger.Info( $"シェルビューを読み込んだ。ViewType={view.GetType().Name}" );
    }

    /// <summary>
    /// 受信した文字列イベントを記録する。
    /// </summary>
    /// <param name="message">受信したイベントの文字列。</param>
    /// <param name="cancellationToken">処理のキャンセル通知。</param>
    /// <returns>処理の完了を表すタスク。</returns>
    public Task HandleAsync( string message, CancellationToken cancellationToken ) {
        logger.Info( $"イベントを受信した。Message={message}" );
        return Task.CompletedTask;
    }

    /// <summary>
    /// ナビゲーションイベントを購読し、初回の画面表示、操作案内、更新確認を開始する。
    /// </summary>
    /// <param name="cancellationToken">処理のキャンセル通知。</param>
    /// <returns>処理の完了を表すタスク。</returns>
    public async Task ActivateAsync( CancellationToken cancellationToken ) {
        logger.Info( $"ShellViewModel をアクティブ化する。初期化済み={_initialized}" );
        eventAggregator.SubscribeOnUIThread( this );

        // Navigation 状態変化で CanGoBack/CanGoForward を更新
        navigationService.Navigating += OnNavigating;
        navigationService.Navigated += OnNavigated;
        if(coachMarks is not null) coachMarks.PropertyChanged += OnCoachMarkChanged;

        if(!_initialized) {
            navigationService.NavigateToViewModel<MainViewModel>();
            if(coachMarks is not null) {
                await ExecuteCoachMarkActionAsync( () => coachMarks.StartAsync( automatic: true ) );
            }
            _ = ExecuteStartupUpdateCheckAsync( CancellationToken.None );
            _initialized = true;
        }
        await Task.CompletedTask;
    }

    /// <summary>
    /// シェルの非アクティブ化時にイベント購読を解除する。
    /// </summary>
    /// <param name="close">画面を閉じる場合は true。非アクティブ化のみの場合は false。</param>
    /// <param name="cancellationToken">処理のキャンセル通知。</param>
    /// <returns>処理の完了を表すタスク。</returns>
    protected override Task OnDeactivateAsync( bool close, CancellationToken cancellationToken ) {
        logger.Info( $"ShellViewModel を非アクティブ化する。Close={close}" );
        eventAggregator.Unsubscribe( this );
        if(coachMarks is not null) coachMarks.PropertyChanged -= OnCoachMarkChanged;
        navigationService.Navigating -= OnNavigating;
        navigationService.Navigated -= OnNavigated;
        return base.OnDeactivateAsync( close, cancellationToken );
    }

    #endregion

    #region Action Messages

    /// <summary>
    /// ナビゲーション履歴の前の画面へ戻る。
    /// </summary>
    public void GoBack() {
        logger.Info( $"前のページへ戻る。CanGoBack={navigationService.CanGoBack}" );
        navigationService.GoBack();
        NotifyOfPropertyChange( nameof( CanGoBack ) );
        NotifyOfPropertyChange( nameof( CanGoForward ) );
    }

    /// <summary>
    /// ナビゲーション履歴の次の画面へ進む。
    /// </summary>
    public void GoForward() {
        logger.Info( $"次のページへ進む。CanGoForward={navigationService.CanGoForward}" );
        navigationService.GoForward();
        NotifyOfPropertyChange( nameof( CanGoBack ) );
        NotifyOfPropertyChange( nameof( CanGoForward ) );
    }

    /// <summary>
    /// 設定画面へ遷移し、ナビゲーションボタンの状態を更新する。
    /// </summary>
    public void NavToSettings() {
        logger.Info( "SettingsViewModel へ遷移する。" );
        navigationService.For<SettingsViewModel>().Navigate();
        NotifyOfPropertyChange( nameof( CanGoBack ) );
        NotifyOfPropertyChange( nameof( CanGoForward ) );
    }

    /// <summary>
    /// 操作案内を次へ進め、設定保存の失敗を通知する。
    /// </summary>
    /// <returns>処理の完了を表すタスク。</returns>
    public Task NextCoachMark() => CanNextCoachMark
        ? ExecuteCoachMarkActionAsync( () => coachMarks!.NextAsync() )
        : Task.CompletedTask;

    /// <summary>
    /// 保存済みの設定を保持したまま操作案内を終了する。
    /// </summary>
    public void SkipCoachMark() => coachMarks?.Skip();

    /// <summary>
    /// 案内を更新し、メニューの Tips に進むときは MainView を開く。
    /// </summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">イベントの情報。</param>
    private void OnCoachMarkChanged( object? sender, PropertyChangedEventArgs e ) {
        if(e.PropertyName == nameof( CoachMarkService.Step )) {
            if(coachMarks?.IsMainTip == true && navigationService.CurrentContent is not MainView) {
                navigationService.NavigateToViewModel<MainViewModel>();
            }
            NotifyCoachMarkPresentation();
        }
        else if(e.PropertyName == nameof( CoachMarkService.CanNext )) NotifyOfPropertyChange( nameof( CanNextCoachMark ) );
    }

    /// <summary>案内対象、見出し、説明と次へ進む操作の可否の変更を画面へ通知する。</summary>
    private void NotifyCoachMarkPresentation() {
        NotifyOfPropertyChange( nameof( CoachMarkTarget ) );
        NotifyOfPropertyChange( nameof( CoachMarkTitle ) );
        NotifyOfPropertyChange( nameof( CoachMarkDescription ) );
        NotifyOfPropertyChange( nameof( CanNextCoachMark ) );
    }

    /// <summary>
    /// 案内の操作を実行し、例外を記録して設定保存の失敗メッセージを表示する。
    /// </summary>
    /// <param name="action">実行する操作案内の非同期処理。</param>
    /// <returns>処理の完了を表すタスク。</returns>
    private async Task ExecuteCoachMarkActionAsync( Func<Task> action ) {
        try {
            await action();
        }
        catch(Exception ex) {
            logger.Error( "操作案内に必要な設定保存に失敗した。", ex );
            snackbarService.Show( Strings_CoachMarks.SaveFailureMessage );
        }
    }

    /// <summary>
    /// 翻訳リポジトリーを既定のブラウザーで開く。
    /// </summary>
    public void BrowseTranslationRepository() {
        logger.Info( $"翻訳リポジトリをブラウザで開く。Url={TargetRepository.Url}" );
        systemService.OpenInWebBrowser( TargetRepository.Url );
    }

    #endregion

    #region Navigation event handlers

    /// <summary>
    /// ページ遷移の開始時にナビゲーションボタンの状態を更新する。
    /// </summary>
    /// <param name="s">イベントの送信元。</param>
    /// <param name="e">イベントの情報。</param>
    private void OnNavigating( object? s, NavigatingCancelEventArgs e ) {
        logger.Info( $"ナビゲーションを開始した。Uri={e.Uri}" );
        // 直前でも有効/無効が変わるケースに備えて更新
        NotifyOfPropertyChange( nameof( CanGoBack ) );
        NotifyOfPropertyChange( nameof( CanGoForward ) );
    }

    /// <summary>
    /// ページ遷移後にナビゲーションボタンを更新し、必須設定の不足を通知する。
    /// </summary>
    /// <param name="s">イベントの送信元。</param>
    /// <param name="e">イベントの情報。</param>
    private void OnNavigated( object? s, NavigationEventArgs e ) {
        logger.Info( $"ナビゲーションが完了した。Content={e.Content?.GetType().Name ?? "null"}" );
        // スタック確定後に更新
        NotifyOfPropertyChange( nameof( CanGoBack ) );
        NotifyOfPropertyChange( nameof( CanGoForward ) );

        NotifyCoachMarkPresentation();
        ValidateAppSettingsAndNotify();
    }

    #endregion

    /// <summary>最低限のアプリ設定を検証し、不足時に通知する</summary>
    private void ValidateAppSettingsAndNotify() {
        var settings = appSettingsService.Settings;
        var hasMissingRequiredSetting =
            string.IsNullOrEmpty( settings.DcsWorldInstallDir ) ||
            string.IsNullOrEmpty( settings.TranslateFileDir ) ||
            (settings.UseExternalAircraftInjectionDir && string.IsNullOrEmpty( settings.ExternalAircraftInjectionDir )) ||
            (settings.UseExternalCampaignInjectionDir && string.IsNullOrEmpty( settings.ExternalCampaignInjectionDir ));

        if(hasMissingRequiredSetting) {
            logger.Warn( "アプリ設定に不足がある" );
            snackbarService.Show(
                "設定が不足しています。",
                "設定",
                NavToSettings,
                TimeSpan.FromSeconds( 3 ) );
        }
        else {
            logger.Info( "アプリ設定が検証済み。全ての必須ディレクトリが設定済み。" );
        }
    }

    /// <summary>
    /// 起動時に 1 回だけ更新確認を実行し、更新を検知したときに通知する。
    /// </summary>
    /// <param name="cancellationToken">キャンセル通知を受け取る。</param>
    private async Task ExecuteStartupUpdateCheckAsync( CancellationToken cancellationToken ) {
        if(this._startupUpdateCheckStarted) {
            return;
        }

        this._startupUpdateCheckStarted = true;

        try {
            if(applicationUpdateService is not null) {
                var pendingResult = applicationUpdateService.GetPendingUpdate();
                if(pendingResult.IsSuccess && pendingResult.Value is not null) {
                    await ShowPendingUpdateNotificationAsync( pendingResult.Value ).ConfigureAwait( false );
                    return;
                }
            }

            var updateCheckResult = await updateCheckService.CheckForUpdateAsync( cancellationToken ).ConfigureAwait( false );
            if(!updateCheckResult.IsUpdateAvailable ||
                string.IsNullOrWhiteSpace( updateCheckResult.LatestVersionLabel ) ||
                string.IsNullOrWhiteSpace( updateCheckResult.ReleaseUrl )) {
                return;
            }

            await Execute.OnUIThreadAsync( () => {
                snackbarService.Show(
                    string.Format( Strings_Shared.UpdateAvailableMessageFormat, updateCheckResult.LatestVersionLabel ),
                    applicationUpdateService is null || dialogService is null ? Strings_Shared.UpdateActionOpenRelease : GetResourceString( "ApplicationUpdateAction" ),
                    applicationUpdateService is null || dialogService is null
                        ? () => systemService.OpenInWebBrowser( updateCheckResult.ReleaseUrl )
                        : () => _ = ExecuteApplicationUpdateAsync( updateCheckResult ),
                    duration: TimeSpan.FromSeconds( 4 ) );
                return Task.CompletedTask;
            } ).ConfigureAwait( false );
        }
        catch(Exception ex) {
            logger.Warn( "起動時更新確認でエラーが発生したため通知を行わない。", ex );
        }
    }

    /// <summary>
    /// 更新のダウンロードを確認し、準備した更新の再起動確認へ進む。
    /// </summary>
    /// <param name="update">検出した更新の情報。</param>
    /// <returns>処理の完了を表すタスク。</returns>
    private async Task ExecuteApplicationUpdateAsync( UpdateCheckResult update ) {
        if(this._applicationUpdateInProgress || applicationUpdateService is null || dialogService is null || update.PreferredAssets.Count == 0) {
            return;
        }

        this._applicationUpdateInProgress = true;
        try {
            var confirmResult = await dialogService.ConfirmationDialogShowAsync( new ConfirmationDialogParameters
            {
                AutomationId = "ApplicationUpdateDownloadConfirmationDialog",
                Title = GetResourceString( "ApplicationUpdateDialogTitle" ),
                Message = string.Format( GetResourceString( "ApplicationUpdateDownloadMessageFormat" ), update.LatestVersionLabel, FormatFileSize( update.PreferredDownloadSize ) ),
                ConfirmButtonText = GetResourceString( "ApplicationUpdateDownloadButton" ),
                ConfirmButtonStyleKey = "MaterialDesignRaisedButton",
                CancelButtonText = GetResourceString( "ApplicationUpdateCancelButton" )
            } );
            if(confirmResult != ConfirmationDialogResult.Confirm) {
                return;
            }

            var stageResult = await dialogService.ApplicationUpdateProgressDialogShowAsync(
                new ApplicationUpdateProgressDialogParameters(
                    GetResourceString( "ApplicationUpdateProgressTitle" ),
                    (progress, cancellationToken) => applicationUpdateService.DownloadAndStageAsync( update, progress, cancellationToken ),
                    logger ) );
            if(stageResult.IsFailed) {
                await HandleUpdateFailureAsync( update );
                return;
            }

            await ConfirmRestartAsync( stageResult.Value );
        }
        catch(Exception ex) {
            logger.Error( "アプリケーション更新の操作に失敗した。", ex );
            snackbarService.Show( GetResourceString( "ApplicationUpdatePrepareFailureMessage" ) );
        }
        finally {
            this._applicationUpdateInProgress = false;
        }
    }

    /// <summary>
    /// 更新の再起動確認を表示し、選択に応じて準備済み更新を保持・破棄するか更新適用を開始する。
    /// </summary>
    /// <param name="stageResult">準備済みの更新の情報。</param>
    /// <returns>処理の完了を表すタスク。</returns>
    private async Task ConfirmRestartAsync( ApplicationUpdateStageResult stageResult ) {
        if(applicationUpdateService is null || dialogService is null) {
            return;
        }

        var restartResult = await dialogService.ConfirmationDialogShowAsync( new ConfirmationDialogParameters
        {
            AutomationId = "ApplicationUpdateRestartConfirmationDialog",
            Title = GetResourceString( "ApplicationUpdateReadyTitle" ),
            Message = string.Format( GetResourceString( "ApplicationUpdateRestartMessageFormat" ), stageResult.TargetVersion.ToString( 3 ) ),
            ConfirmButtonText = GetResourceString( "ApplicationUpdateRestartNowButton" ),
            SecondaryButtonText = GetResourceString( "ApplicationUpdateLaterButton" ),
            CancelButtonText = GetResourceString( "ApplicationUpdateCancelButton" ),
            ConfirmButtonStyleKey = "MaterialDesignRaisedButton",
            SecondaryButtonStyleKey = "MaterialDesignRaisedButton",
            ButtonOrder = [
                ConfirmationDialogResult.Confirm,
                ConfirmationDialogResult.Secondary,
                ConfirmationDialogResult.Cancel
            ]
        } );
        if(restartResult == ConfirmationDialogResult.Secondary) {
            return;
        }

        if(restartResult != ConfirmationDialogResult.Confirm) {
            this.DiscardStagedUpdate( stageResult );
            return;
        }

        if(!await ((IGuardClose)this).CanCloseAsync( CancellationToken.None )) {
            logger.Warn( "シェルの終了確認により更新再起動を中止した。" );
            snackbarService.Show( GetResourceString( "ApplicationUpdateUnsavedMessage" ) );
            return;
        }

        if(applicationRestartCoordinator is not null &&
            !await applicationRestartCoordinator.PrepareForRestartAsync( CancellationToken.None )) {
            logger.Warn( "編集中ウィンドウの終了確認により更新再起動を中止した。" );
            snackbarService.Show( GetResourceString( "ApplicationUpdateUnsavedMessage" ) );
            return;
        }

        var launchResult = applicationUpdateService.LaunchApplyProcess( stageResult, Environment.ProcessId );
        if(launchResult.IsFailed) {
            logger.Error( $"更新適用プロセスを開始できなかった。Errors={string.Join( "; ", launchResult.Errors.Select( error => error.Message ) )}" );
            snackbarService.Show( GetResourceString( "ApplicationUpdateRestartFailureMessage" ) );
            return;
        }

        logger.Info( "更新適用プロセスを開始したためアプリケーションを終了する。" );
        _ = System.Windows.Application.Current.Dispatcher.BeginInvoke(
            System.Windows.Application.Current.Shutdown,
            DispatcherPriority.ApplicationIdle );
    }

    /// <summary>
    /// 適用待ちの更新を UI スレッドで通知し、再起動の操作を提示する。
    /// </summary>
    /// <param name="stageResult">準備済みの更新の情報。</param>
    /// <returns>処理の完了を表すタスク。</returns>
    private Task ShowPendingUpdateNotificationAsync( ApplicationUpdateStageResult stageResult ) =>
        Execute.OnUIThreadAsync( () => {
            snackbarService.Show(
                string.Format( GetResourceString( "ApplicationUpdatePendingMessageFormat" ), stageResult.TargetVersion.ToString( 3 ) ),
                GetResourceString( "ApplicationUpdateRestartAction" ),
                () => _ = ConfirmRestartAsync( stageResult ),
                duration: TimeSpan.FromSeconds( 6 ) );
            return Task.CompletedTask;
        } );

    /// <summary>
    /// 更新準備の失敗時に、再試行またはリリースページを開く操作を提示する。
    /// </summary>
    /// <param name="update">検出した更新の情報。</param>
    /// <returns>処理の完了を表すタスク。</returns>
    private async Task HandleUpdateFailureAsync( UpdateCheckResult update ) {
        if(dialogService is null || string.IsNullOrWhiteSpace( update.ReleaseUrl )) {
            return;
        }

        var failureResult = await dialogService.ConfirmationDialogShowAsync( new ConfirmationDialogParameters
        {
            AutomationId = "ApplicationUpdateFailureConfirmationDialog",
            Title = GetResourceString( "ApplicationUpdateFailureTitle" ),
            Message = GetResourceString( "ApplicationUpdateFailureMessage" ),
            ConfirmButtonText = GetResourceString( "ApplicationUpdateRetryButton" ),
            ConfirmButtonStyleKey = "MaterialDesignRaisedButton",
            SecondaryButtonText = GetResourceString( "ApplicationUpdateReleasePageButton" ),
            SecondaryButtonStyleKey = "MaterialDesignRaisedButton",
            CancelButtonText = GetResourceString( "ApplicationUpdateCloseButton" ),
            ButtonOrder = [
                ConfirmationDialogResult.Confirm,
                ConfirmationDialogResult.Secondary,
                ConfirmationDialogResult.Cancel
            ]
        } );
        if(failureResult == ConfirmationDialogResult.Confirm) {
            this._applicationUpdateInProgress = false;
            await ExecuteApplicationUpdateAsync( update );
        }
        else if(failureResult == ConfirmationDialogResult.Secondary) {
            systemService.OpenInWebBrowser( update.ReleaseUrl );
        }
    }

    /// <summary>
    /// ステージ済み更新を破棄し、失敗時は記録だけして更新操作を継続する。
    /// </summary>
    /// <param name="stageResult">破棄するステージ済み更新を受け取る。</param>
    private void DiscardStagedUpdate( ApplicationUpdateStageResult stageResult ) {
        if(applicationUpdateService is null) {
            return;
        }

        var discardResult = applicationUpdateService.DiscardStagedUpdate( stageResult );
        if(discardResult.IsFailed) {
            logger.Warn( $"ステージ済み更新を破棄できなかった。Errors={string.Join( "; ", discardResult.Errors.Select( error => error.Message ) )}" );
        }
    }

    /// <summary>
    /// バイト数を bytes、KB、MB の単位で表示用の文字列に整形する。
    /// </summary>
    /// <param name="bytes">表示するファイルサイズのバイト数。</param>
    /// <returns>単位を付けたファイルサイズの文字列。</returns>
    private static string FormatFileSize( long bytes ) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / 1024d / 1024d:N1} MB",
        >= 1024 => $"{bytes / 1024d:N1} KB",
        _ => $"{bytes:N0} bytes"
    };

    /// <summary>
    /// 共有リソースから文字列を取得し、未定義の場合はリソース名を返す。
    /// </summary>
    /// <param name="name">取得する共有リソースの名前。</param>
    /// <returns>リソースの文字列。見つからない場合は指定したリソース名。</returns>
    private static string GetResourceString( string name ) =>
        Strings_Shared.ResourceManager.GetString( name ) ?? name;
}