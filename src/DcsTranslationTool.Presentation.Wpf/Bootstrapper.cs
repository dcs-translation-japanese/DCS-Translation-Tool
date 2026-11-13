using System.Windows;

using Caliburn.Micro;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Composition;
using DcsTranslationTool.Presentation.Wpf.Features.CreatePullRequest;
using DcsTranslationTool.Presentation.Wpf.Features.Download;
using DcsTranslationTool.Presentation.Wpf.Features.Main;
using DcsTranslationTool.Presentation.Wpf.Features.Settings;
using DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;
using DcsTranslationTool.Presentation.Wpf.Features.TranslationFileSelection;
using DcsTranslationTool.Presentation.Wpf.Features.Upload;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.Shell;

namespace DcsTranslationTool.Presentation.Wpf;

/// <summary>
/// アプリケーションの依存関係登録、起動、終了処理を構成する。
/// </summary>
public class Bootstrapper : BootstrapperBase {
    /// <summary>画面やサービスの依存関係を解決する DI コンテナー。</summary>
    private SimpleContainer? container;
    /// <summary>起動処理や未処理例外を記録するログサービス。</summary>
    private ILoggingService? loggingService;
    /// <summary>アプリケーション設定の読み込みと保存を行うサービス。</summary>
    private IAppSettingsService? appSettingsService;

    /// <summary>
    /// Caliburn.Micro のブートストラッパーを初期化する。
    /// </summary>
    public Bootstrapper() {
        Initialize();
    }

    /// <summary>
    /// アプリケーションサービスと画面の依存関係をコンテナーに登録する。
    /// </summary>
    protected override void Configure() {
        container = new SimpleContainer();

        // Caliburn 基盤
        container.Singleton<IWindowManager, WindowManager>();
        container.Singleton<IEventAggregator, EventAggregator>();
        container.Instance( container );

        // Presentation層で実装していないサービス群
        CompositionRegistration.Register( container );
        loggingService = container.GetInstance<ILoggingService>();
        container.Singleton<IApplicationInfoService, ApplicationInfoService>();
        appSettingsService = container.GetInstance<IAppSettingsService>();

        container.Singleton<IDialogService, DialogService>();
        container.Singleton<IDialogProvider, DialogProvider>();
        container.Singleton<IDispatcherService, DispatcherService>();
        container.Singleton<IApplicationWindowProvider, ApplicationWindowProvider>();
        container.Singleton<IApplicationRestartCoordinator, ApplicationRestartCoordinator>();
        container.Singleton<IEntryApplyService, EntryApplyService>();
        container.Singleton<IRepoOnlySyncService, RepoOnlySyncService>();
        container.Singleton<IApplyWorkflowService, ApplyWorkflowService>();
        container.Singleton<IDownloadWorkflowService, DownloadWorkflowService>();
        container.Singleton<IFileEntryWatcherLifecycle, FileEntryWatcherLifecycle>();
        container.Singleton<IFileEntryTreeService, FileEntryTreeService>();
        container.Singleton<ITranslationArchiveTreeService, TranslationArchiveTreeService>();
        container.Singleton<ITranslationFileSelectionWorkflowService, TranslationFileSelectionWorkflowService>();
        container.Singleton<ITranslationFileSelectionActionService, TranslationFileSelectionActionService>();
        container.Singleton<ITranslationFileSelectionWorkflowUiAdapter, TranslationFileSelectionWorkflowUiAdapter>();
        container.Singleton<IPathSafetyGuard, PathSafetyGuard>();
        container.Singleton<ISnackbarService, SnackbarService>();
        container.Singleton<ITranslationCreationPathService, TranslationCreationPathService>();
        container.Singleton<ITranslationCreationFilterService, TranslationCreationFilterService>();
        container.Singleton<ITranslationCreationLayoutStateService, TranslationCreationLayoutStateService>();
        container.Singleton<ITranslationCreationDialogService, TranslationCreationDialogService>();
        container.Singleton<ITranslationCreationImportExportService, TranslationCreationImportExportService>();
        container.Singleton<TranslationCreationDictionaryLoader>();
        container.Singleton<ITranslationCreationNotificationService, TranslationCreationNotificationService>();
        container.Singleton<ITranslationCreationWorkflowService, TranslationCreationWorkflowService>();
        container.Singleton<ITranslationCreationViewModelFactory, TranslationCreationViewModelFactory>();

        // ViewModels
        container.Singleton<DcsTranslationTool.Presentation.Wpf.Features.CoachMarks.CoachMarkService>();
        container.Singleton<ShellViewModel>();
        container.PerRequest<MainViewModel>();
        container.PerRequest<SettingsViewModel>();
        container.PerRequest<DownloadViewModel>();
        container.PerRequest<UploadViewModel>();
        container.PerRequest<TranslationFileSelectionViewModel>();
        container.PerRequest<CreatePullRequestViewModel>();
    }

    /// <summary>
    /// 更新適用モードを処理するか、通常のシェル画面を表示する。
    /// </summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">イベントの情報。</param>
    protected override async void OnStartup( object sender, StartupEventArgs e ) {
        loggingService?.Info( "起動" );
        if(e.Args is ["--apply-update", var requestPath]) {
            var updateService = container!.GetInstance<IApplicationUpdateService>();
            var applyResult = await updateService.ApplyPreparedUpdateAsync( requestPath );
            if(applyResult.IsFailed) {
                loggingService?.Error( $"更新専用モードに失敗した。Errors={string.Join( "; ", applyResult.Errors.Select( error => error.Message ) )}" );
                MessageBox.Show(
                    string.Format(
                        DcsTranslationTool.Resources.Strings_Shared.ResourceManager.GetString( "ApplicationUpdateApplyFailureMessageFormat" ) ?? "{0}",
                        string.Join( Environment.NewLine, applyResult.Errors.Select( error => error.Message ) ) ),
                    DcsTranslationTool.Resources.Strings_Shared.ResourceManager.GetString( "ApplicationUpdateApplyFailureTitle" ) ?? "DCS Translation Tool",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error );
            }
            System.Windows.Application.Current.Shutdown( applyResult.IsSuccess ? 0 : 1 );
            return;
        }

        var cleanupResult = container!.GetInstance<IApplicationUpdateService>().CleanupCompletedUpdates();
        if(cleanupResult.IsFailed) {
            loggingService?.Warn( $"完了済み更新のクリーンアップを継続できなかった。Errors={string.Join( "; ", cleanupResult.Errors.Select( error => error.Message ) )}" );
        }

        var shellView = new ShellView();
        var frameAdapter = new FrameAdapter( shellView.RootFrame, true );
        container!.Instance<INavigationService>( frameAdapter );
        var shellVm = container.GetInstance<ShellViewModel>();
        ViewModelBinder.Bind( shellVm, shellView, null );
        await ScreenExtensions.TryActivateAsync( shellVm );
        shellView.Show();
        // （Caliburn の表示ヘルパは使わず、Attach 済み状態で起動する）
        await Task.CompletedTask;
    }
    /// <summary>
    /// 指定された型とキーで登録済みのサービスを解決する。
    /// </summary>
    /// <param name="service">解決対象のサービス型。</param>
    /// <param name="key">サービスを識別する登録キー。</param>
    /// <returns>解決したインスタンス。コンテナーが未構築の場合は null。</returns>
    protected override object? GetInstance( Type service, string key ) => container?.GetInstance( service, key );

    /// <summary>
    /// 指定された型で登録されているすべてのサービスを取得する。
    /// </summary>
    /// <param name="service">解決対象のサービス型。</param>
    /// <returns>登録されたインスタンスの列挙。コンテナーが未構築の場合は null。</returns>
    protected override IEnumerable<object>? GetAllInstances( Type service ) => container?.GetAllInstances( service );

    /// <summary>
    /// 既存のインスタンスにコンテナーから依存関係を設定する。
    /// </summary>
    /// <param name="instance">依存関係を設定する既存のインスタンス。</param>
    protected override void BuildUp( object instance ) => container?.BuildUp( instance );

    /// <summary>
    /// アプリ終了時処理。
    /// </summary>
    protected override void OnExit( object sender, EventArgs e ) {
        loggingService?.Info( "終了" );
        base.OnExit( sender, e );
        if(appSettingsService is IAsyncDisposable asyncDisposable) {
            try {
                loggingService?.Debug( "AppSettingsService を非同期破棄する。" );
                asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
                loggingService?.Debug( "AppSettingsService の非同期破棄が完了した。" );
            }
            catch(Exception ex) {
                loggingService?.Error( "AppSettingsService の破棄に失敗した。", ex );
            }
        }
        else if(appSettingsService is IDisposable disposable) {
            try {
                loggingService?.Debug( "AppSettingsService を同期破棄する。" );
                disposable.Dispose();
                loggingService?.Debug( "AppSettingsService の同期破棄が完了した。" );
            }
            catch(Exception ex) {
                loggingService?.Error( "AppSettingsService の同期破棄に失敗した。", ex );
            }
        }
        NLog.LogManager.Shutdown();
    }
}