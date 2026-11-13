using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Domain.Models;
using DcsTranslationTool.Presentation.Wpf.Features.CoachMarks;
using DcsTranslationTool.Presentation.Wpf.Features.Common;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Results;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;
using DcsTranslationTool.Presentation.Wpf.ViewModels;
using DcsTranslationTool.Resources;

namespace DcsTranslationTool.Presentation.Wpf.Features.Download;

/// <summary>
/// ダウンロードページの状態を管理する ViewModel
/// </summary>
public class DownloadViewModel(
    IApiService apiService,
    IAppSettingsService appSettingsService,
    IDialogService dialogService,
    IDownloadWorkflowService downloadWorkflowService,
    IDispatcherService dispatcherService,
    IFileEntryService fileEntryService,
    IFileEntryWatcherLifecycle fileEntryWatcherLifecycle,
    IFileEntryTreeService fileEntryTreeService,
    ILoggingService logger,
    ISnackbarService snackbarService,
    ISystemService systemService,
    CoachMarkService? coachMarks = null
) : FileEntryTabsViewModelBase(
    apiService,
    dispatcherService,
    fileEntryService,
    fileEntryWatcherLifecycle,
    fileEntryTreeService,
    logger,
    snackbarService
) {

    #region Fields

    /// <summary>ダウンロード処理が実行中かどうかを保持する。</summary>
    private bool _isDownloading;
    /// <summary>ダウンロード処理の進捗値を保持する。</summary>
    private double _downloadedProgress = 0.0;
    /// <summary>翻訳の適用処理が実行中かどうかを保持する。</summary>
    private bool _isApplying;
    /// <summary>翻訳の適用処理の進捗値を保持する。</summary>
    private double _appliedProgress = 0.0;
    /// <summary>一括更新中にエントリー変更に伴う表示更新を抑制するフラグ。</summary>
    private bool _suppressEntriesChanged;
    /// <summary>ダウンロード処理の通知を画面の状態に反映するアダプター。</summary>
    private DownloadWorkflowUiAdapter? _uiAdapter;

    /// <summary>
    /// Download 画面向けの UI 更新アダプタを取得する。
    /// </summary>
    private DownloadWorkflowUiAdapter UiAdapter =>
        _uiAdapter ??= new(
            DispatcherService,
            SnackbarService,
            value => DownloadedProgress = value,
            value => AppliedProgress = value
        );


    #endregion

    #region Properties

    /// <summary>画面で使用する操作案内サービス。操作案内を使用しない場合は null。</summary>
    public CoachMarkService? CoachMarks => coachMarks;

    /// <summary>ダウンロード処理が実行中かどうかを取得または設定する。</summary>
    public bool IsDownloading {
        get => _isDownloading;
        set {
            if(!Set( ref _isDownloading, value )) return;
            NotifyOfPropertyChange( nameof( IsTreeInteractionEnabled ) );
        }
    }

    /// <summary>ダウンロード処理の進捗値を取得または設定する。</summary>
    public double DownloadedProgress {
        get => _downloadedProgress;
        set => Set( ref _downloadedProgress, Math.Clamp( value, 0, 100 ) );
    }

    /// <summary>翻訳の適用処理が実行中かどうかを取得または設定する。</summary>
    public bool IsApplying {
        get => _isApplying;
        set {
            if(!Set( ref _isApplying, value )) return;
            NotifyOfPropertyChange( nameof( IsTreeInteractionEnabled ) );
        }
    }

    /// <summary>翻訳の適用処理の進捗値を取得または設定する。</summary>
    public double AppliedProgress {
        get => _appliedProgress;
        set => Set( ref _appliedProgress, Math.Clamp( value, 0, 100 ) );
    }

    #endregion

    #region Action Guards

    /// <summary>
    /// ダウンロード可能か
    /// </summary>
    public bool CanDownload => !IsDownloading && GetDownloadableCheckedEntries().Length > 0;

    /// <summary>
    /// 適用可能か
    /// </summary>
    public bool CanApply => !IsApplying && HasCheckedEntries();

    #endregion

    #region Actions

    /// <summary>
    /// チェック状態のファイルをダウンロードする
    /// </summary>
    /// <returns>非同期タスク</returns>
    public async Task Download() {
        Logger.Info( "ダウンロード処理を開始する。" );
        if(!CanDownload) {
            Logger.Warn( "ダウンロードは現在許可されていないため処理を中断する。" );
            return;
        }

        var saveRootPath = appSettingsService.Settings.TranslateFileDir;
        if(string.IsNullOrWhiteSpace( saveRootPath )) {
            Logger.Warn( "保存先ディレクトリが設定されていないため保存を中断する。" );
            await UiAdapter.ShowSnackbarAsync( "保存先フォルダーが設定されていません" );
            return;
        }

        await ExecuteWithEntriesChangedSuppressedAsync( isDownload: true, async () => {
            var request = new DownloadExecutionRequest(
                GetSelectedTab(),
                saveRootPath
            );
            var result = await downloadWorkflowService.ExecuteDownloadAsync(
                request,
                UiAdapter.UpdateDownloadProgressAsync
            );
            await HandleWorkflowEventsAsync( result.Events );
        } );
    }

    /// <summary>
    /// 選択されたファイルを miz/trk に適用する
    /// </summary>
    /// <returns>非同期タスク</returns>
    public async Task Apply() {
        Logger.Info( "適用処理を開始する。" );
        if(!CanApply) {
            Logger.Warn( "適用は現在許可されていないため処理を中断する。" );
            return;
        }

        var applySucceeded = false;
        coachMarks?.BeginApply();
        try {
            await ExecuteWithEntriesChangedSuppressedAsync( isDownload: false, async () => {
                var selectedTab = GetSelectedTab();
                var checkedEntries = selectedTab?.GetCheckedViewModels()
                    .Where( entry => !entry.IsDirectory )
                    .ToList()
                    ?? [];
                var modifiedEntries = checkedEntries
                    .Where( entry => entry.ChangeType == FileChangeType.Modified )
                    .ToList();

                if(modifiedEntries.Count > 0) {
                    var repositoryTargets = await ResolveRepositorySyncTargetsAsync( modifiedEntries );
                    if(repositoryTargets is null) {
                        Logger.Warn( "差分ファイル適用確認で中止が選択されたため処理を終了する。" );
                        return;
                    }

                    if(repositoryTargets.Count > 0) {
                        var synced = await downloadWorkflowService.SyncModifiedFilesWithRepositoryAsync(
                            repositoryTargets,
                            appSettingsService.Settings.TranslateFileDir,
                            UiAdapter.ShowSnackbarAsync
                        );
                        if(!synced) {
                            Logger.Warn( "サーバー版での差分同期に失敗したため適用処理を中断する。" );
                            return;
                        }
                    }
                }

                var request = new ApplyExecutionRequest(
                    selectedTab,
                    appSettingsService.Settings.DcsWorldInstallDir,
                    appSettingsService.Settings.SourceUserMissionDir,
                    appSettingsService.Settings.UseExternalAircraftInjectionDir,
                    appSettingsService.Settings.ExternalAircraftInjectionDir,
                    appSettingsService.Settings.UseExternalCampaignInjectionDir,
                    appSettingsService.Settings.ExternalCampaignInjectionDir,
                    appSettingsService.Settings.TranslateFileDir
                );
                var result = await downloadWorkflowService.ExecuteApplyAsync(
                    request,
                    UiAdapter.ShowSnackbarAsync,
                    UiAdapter.UpdateApplyProgressAsync
                );
                applySucceeded = result.IsSuccess;
                await HandleWorkflowEventsAsync( result.Events );
            } );
        }
        finally {
            coachMarks?.ApplyFinished( applySucceeded );
        }
    }

    /// <summary>
    /// 翻訳ファイルの管理ディレクトリをエクスプローラーで開く
    /// </summary>
    public void OpenDirectory() {
        Logger.Info( $"翻訳ファイルディレクトリを開く。Directory={appSettingsService.Settings.TranslateFileDir}" );
        systemService.OpenDirectory( appSettingsService.Settings.TranslateFileDir );
    }

    /// <summary>
    /// EntriesChanged を抑止してワークフロー処理を実行する。
    /// </summary>
    /// <param name="isDownload">ダウンロード処理かどうか。</param>
    /// <param name="action">実行処理。</param>
    /// <returns>非同期タスク。</returns>
    private async Task ExecuteWithEntriesChangedSuppressedAsync( bool isDownload, Func<Task> action ) {
        _suppressEntriesChanged = true;
        SuspendEntriesChangedSubscription();

        if(isDownload) {
            IsDownloading = true;
            await UiAdapter.UpdateDownloadProgressAsync( 0 );
        }
        else {
            IsApplying = true;
            await UiAdapter.UpdateApplyProgressAsync( 0 );
        }

        NotifyOfPropertyChange( nameof( CanDownload ) );
        NotifyOfPropertyChange( nameof( CanApply ) );

        try {
            await action();
        }
        finally {
            if(isDownload) {
                IsDownloading = false;
            }
            else {
                IsApplying = false;
            }

            _suppressEntriesChanged = false;
            ResumeEntriesChangedSubscription();

            await RefreshLocalEntriesAsync();
            NotifyOfPropertyChange( nameof( CanDownload ) );
            NotifyOfPropertyChange( nameof( CanApply ) );
            Logger.Info( $"{(isDownload ? "ダウンロード" : "適用")}処理を終了した。" );
        }
    }

    /// <summary>
    /// 選択中タブを取得する。
    /// </summary>
    /// <returns>選択中タブ。未選択時は <see langword="null"/>。</returns>
    private TabItemViewModel? GetSelectedTab() =>
        Tabs.Count > 0 && SelectedTabIndex >= 0 && SelectedTabIndex < Tabs.Count
            ? Tabs[SelectedTabIndex]
            : null;

    /// <summary>
    /// 選択中タブのうちダウンロード可能なチェック済みファイル一覧を取得する。
    /// </summary>
    /// <returns>ダウンロード可能なチェック済みファイル一覧。</returns>
    private IFileEntryViewModel[] GetDownloadableCheckedEntries() =>
        GetSelectedTab()?.GetCheckedViewModels()
            .Where( entry => !entry.IsDirectory )
            .Where( entry => entry.ChangeType is FileChangeType.Modified or FileChangeType.RepoOnly )
            .ToArray()
        ?? [];

    /// <summary>
    /// Modified 適用時にサーバー同期対象を解決する。
    /// </summary>
    /// <param name="modifiedEntries">差分エントリ一覧。</param>
    /// <returns>サーバー同期対象。中止時は <see langword="null"/>。</returns>
    private async Task<IReadOnlyList<IFileEntryViewModel>?> ResolveRepositorySyncTargetsAsync( List<IFileEntryViewModel> modifiedEntries ) {
        var modeResult = await dialogService.DownloadModifiedApplyModeDialogShowAsync(
            new DownloadModifiedApplyModeDialogParameters
            {
                Title = Strings_Download.ModifiedApplyModeDialogTitle,
                Message = string.Format( Strings_Download.ModifiedApplyModeDialogMessage, modifiedEntries.Count ),
                ApplyAllRepositoryButtonText = Strings_Download.ModifiedApplyModeDialogApplyAllRepositoryButtonText,
                ApplyAllLocalButtonText = Strings_Download.ModifiedApplyModeDialogApplyAllLocalButtonText,
                SelectIndividuallyButtonText = Strings_Download.ModifiedApplyModeDialogSelectIndividuallyButtonText,
                CancelButtonText = Strings_Download.ModifiedApplyModeDialogCancelButtonText,
            } );

        switch(modeResult) {
            case DownloadModifiedApplyModeDialogResult.ApplyAllRepository:
                return modifiedEntries.ToList();
            case DownloadModifiedApplyModeDialogResult.ApplyAllLocal:
                return [];
            case DownloadModifiedApplyModeDialogResult.SelectIndividually:
                return await ResolveRepositoryTargetsFromSelectionDialogAsync( modifiedEntries );
            case DownloadModifiedApplyModeDialogResult.Cancel:
            default:
                return null;
        }
    }

    /// <summary>
    /// 個別選択ダイアログからサーバー同期対象を解決する。
    /// </summary>
    /// <param name="modifiedEntries">差分エントリ一覧。</param>
    /// <returns>サーバー同期対象。中止時は <see langword="null"/>。</returns>
    private async Task<IReadOnlyList<IFileEntryViewModel>?> ResolveRepositoryTargetsFromSelectionDialogAsync( List<IFileEntryViewModel> modifiedEntries ) {
        var items = modifiedEntries
            .Select( entry => new DownloadModifiedApplySelectionItem( entry.Path ) )
            .ToArray();

        var selectionResult = await dialogService.DownloadModifiedApplySelectionDialogShowAsync(
            new DownloadModifiedApplySelectionDialogParameters
            {
                Title = Strings_Download.ModifiedApplySelectionDialogTitle,
                Message = string.Format( Strings_Download.ModifiedApplySelectionDialogMessage, modifiedEntries.Count ),
                PathHeader = Strings_Download.ModifiedApplySelectionDialogPathHeader,
                RepositoryHeader = Strings_Download.ModifiedApplySelectionDialogRepositoryHeader,
                LocalHeader = Strings_Download.ModifiedApplySelectionDialogLocalHeader,
                ConfirmButtonText = Strings_Download.ModifiedApplySelectionDialogConfirmButtonText,
                CancelButtonText = Strings_Download.ModifiedApplySelectionDialogCancelButtonText,
                Items = items,
            } );

        if(!selectionResult.IsConfirmed) {
            return null;
        }

        return modifiedEntries
            .Where( entry =>
                selectionResult.SelectedSources.TryGetValue( entry.Path, out var source ) &&
                source == DownloadModifiedApplySource.Repository )
            .ToList();
    }

    /// <summary>
    /// ワークフローイベントを UI へ反映する。
    /// </summary>
    /// <param name="events">反映対象イベント。</param>
    /// <returns>非同期タスク。</returns>
    private async Task HandleWorkflowEventsAsync( IReadOnlyList<WorkflowEvent> events ) {
        foreach(var workflowEvent in events) {
            switch(workflowEvent.Kind) {
                case WorkflowEventKind.Notification when !string.IsNullOrWhiteSpace( workflowEvent.Message ):
                    await UiAdapter.ShowSnackbarAsync( workflowEvent.Message );
                    break;
                case WorkflowEventKind.DownloadProgress when workflowEvent.Progress.HasValue:
                    await UiAdapter.UpdateDownloadProgressAsync( workflowEvent.Progress.Value );
                    break;
                case WorkflowEventKind.ApplyProgress when workflowEvent.Progress.HasValue:
                    await UiAdapter.UpdateApplyProgressAsync( workflowEvent.Progress.Value );
                    break;
            }
        }
    }

    /// <inheritdoc />
    protected override string ViewModelName => nameof( DownloadViewModel );

    /// <inheritdoc />
    protected override ChangeTypeMode TreeMode => ChangeTypeMode.Download;

    /// <inheritdoc />
    protected override bool ShouldIgnoreEntriesChanged() {
        if(!_suppressEntriesChanged) {
            return false;
        }

        Logger.Info( "ダウンロード中のため EntriesChanged を即時破棄する。" );
        return true;
    }

    /// <inheritdoc />
    protected override void NotifyGuardProperties() {
        NotifyOfPropertyChange( nameof( CanDownload ) );
        NotifyOfPropertyChange( nameof( CanApply ) );
        var hasMizTranslation = GetSelectedTab()?.GetCheckedViewModels().Any( entry =>
            !entry.IsDirectory
            && (coachMarks?.IsActive != true || entry.Path.StartsWith( CoachMarkService.TranslationSelectionPath + "/", StringComparison.Ordinal ))
            && entry.Path.Split( '/' ).SkipLast( 1 ).Any( segment =>
                segment.EndsWith( ".miz", StringComparison.OrdinalIgnoreCase ) ) ) == true;
        coachMarks?.UpdateSelection( hasMizTranslation );
    }

    /// <inheritdoc />
    protected override void OnSelectedTabIndexChanged() =>
        NotifyGuardProperties();

    /// <inheritdoc />
    protected override bool IsTreeInteractionEnabledCore() =>
        !IsFetching && !IsDownloading && !IsApplying;

    #endregion
}