using System.IO;

using DcsTranslationTool.Application.Enums;
using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Domain.Models;
using DcsTranslationTool.Presentation.Wpf.Features.Common;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;

namespace DcsTranslationTool.Presentation.Wpf.Features.Upload;

/// <summary>
/// アップロードページの状態、PR対象の検証、作成ダイアログの表示を管理する。
/// </summary>
public sealed class UploadViewModel(
    IApiService apiService,
    IAppSettingsService appSettingsService,
    IDialogService dialogService,
    IDispatcherService dispatcherService,
    IFileEntryService fileEntryService,
    IFileEntryWatcherLifecycle fileEntryWatcherLifecycle,
    IFileEntryTreeService fileEntryTreeService,
    ILoggingService logger,
    ISnackbarService snackbarService,
    ISystemService systemService
) : FileEntryTabsViewModelBase(
    apiService,
    dispatcherService,
    fileEntryService,
    fileEntryWatcherLifecycle,
    fileEntryTreeService,
    logger,
    snackbarService
) {
    /// <inheritdoc />
    protected override string ViewModelName => nameof( UploadViewModel );

    /// <inheritdoc />
    protected override ChangeTypeMode TreeMode => ChangeTypeMode.Upload;

    #region Properties

    /// <summary>
    /// CreatePullRequestDialog を表示可能か
    /// </summary>
    public bool CanShowCreatePullRequestDialog => !IsFetching && HasCheckedEntries();

    /// <summary>
    /// 翻訳ファイルの管理ディレクトリをエクスプローラーで開く。
    /// </summary>
    public void OpenDirectory() {
        Logger.Info( $"翻訳ファイルディレクトリを開く。Directory={appSettingsService.Settings.TranslateFileDir}" );
        systemService.OpenDirectory( appSettingsService.Settings.TranslateFileDir );
    }

    /// <summary>
    /// 選択ファイルの所属先を検証し、削除を含む場合は確認してからPR作成ダイアログを表示する。
    /// </summary>
    public async Task ShowCreatePullRequestDialog() {
        Logger.Info( "Pull Request ダイアログ表示を開始する。" );
        var commitFiles = GetCommitFiles.ToList();
        string subCategory;
        try {
            subCategory = GetSubCategory( commitFiles );
        }
        catch(InvalidOperationException ex) {
            Logger.Warn( ex.Message );
            await DispatcherService.InvokeAsync( () => {
                SnackbarService.Show( ex.Message );
                return Task.CompletedTask;
            } );
            return;
        }

        var dialogParameters = new CreatePullRequestDialogParameters()
        {
            Category = Tabs[SelectedTabIndex].Title,
            SubCategory = subCategory,
            CommitFiles = commitFiles,
        };
        Logger.Info( $"ダイアログ引数を構築した。Category={dialogParameters.Category}, SubCategory={dialogParameters.SubCategory}, FileCount={dialogParameters.CommitFiles.Count()}" );

        // 削除するファイルが含まれる場合確認ダイアログを表示し、Yesでない場合即座に中止する。
        if(dialogParameters.CommitFiles.Any( cf => cf.Operation == CommitOperationType.Delete )) {
            Logger.Warn( "削除予定のファイルが含まれているため確認ダイアログを表示する。" );
            var confirmed = await dialogService.ContinueCancelDialogShowAsync(
                new ConfirmationDialogParameters
                {
                    Title = "削除確認",
                    Message = commitFiles.All( cf => cf.Operation == CommitOperationType.Delete )
                        ? $"対象: {dialogParameters.Category} / {subCategory}\n削除予定: {commitFiles.Count} ファイル\n削除だけのPRを作成します。続行しますか？"
                        : "削除予定のファイルが含まれます。続行しますか？",
                    ConfirmButtonText = "続行",
                    CancelButtonText = "中止",
                } );

            if(!confirmed) {
                Logger.Warn( "削除確認でキャンセルが選択されたため処理を終了する。" );
                return;
            }
        }


        // PR作成の処理結果と任意のアクションをUIスレッドで通知する。
        async Task ShowAsync( string message, string? action = null, System.Action? handler = null ) =>
            await DispatcherService.InvokeAsync( () => {
                SnackbarService.Show( message, action, handler );
                return Task.CompletedTask;
            } );

        try {
            var result = await dialogService.CreatePullRequestDialogShowAsync( dialogParameters );
            Logger.Info( $"Pull Request ダイアログが完了した。IsOk={result.IsOk}" );

            var (message, actionContent, actionHandler) = result switch
            {
                {
                    IsOk: true,
                    PrUrl: string { Length: > 0 } prUrl
                } => ("Pull Request の作成に成功しました", "開く", (System.Action)(() => systemService.OpenInWebBrowser( prUrl ))),

                { IsOk: true }
                    => ("Pull Request の作成に成功しました", null, null),

                { Errors: not null } when result.Errors.Any( e => e is OperationCanceledException )
                    => ("Pull Request の作成をキャンセルしました", null, null),

                { Errors: [{ Message: var m }] } when !string.IsNullOrWhiteSpace( m )
                    => ($"Pull Request の作成に失敗しました: {m}", null, null),

                _ => ("Pull Request の作成に失敗しました", null, null)
            };

            await ShowAsync( message, actionContent, actionHandler );
        }
        catch(OperationCanceledException) {
            Logger.Warn( "Pull Request ダイアログがキャンセルされた。" );
            await ShowAsync( "Pull Request の作成をキャンセルしました" );
        }
        catch(Exception ex) {
            Logger.Error( ex.Message, ex );
            Logger.Warn( "Pull Request ダイアログ処理で例外が発生した。" );
            await ShowAsync( $"Pull Request ダイアログで例外が発生しました: {ex.Message}" );
        }
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// 送信対象の全ファイルが単一の機体、キャンペーン、またはミッションに属することを検証し、その名前を取得する。
    /// </summary>
    /// <param name="commitFiles">PRに含める変更ファイル。</param>
    /// <returns>全ファイルの所属先となるサブカテゴリー名。</returns>
    /// <exception cref="InvalidOperationException">
    /// 変更ファイルがない場合、所属先を特定できない場合、または複数の所属先にまたがる場合。
    /// </exception>
    private string GetSubCategory( IReadOnlyList<CommitFile> commitFiles ) {
        if(commitFiles.Count == 0) {
            throw new InvalidOperationException( "PRの対象となる変更ファイルがありません。" );
        }

        // ディレクトリの集約された変更種別ではなく、実際に送信する全ファイルの所属先を検証する。
        var tab = Tabs[SelectedTabIndex];
        var targets = tab.Root.Children
            .Where( child => child.IsDirectory && commitFiles.Any( file =>
                file.RepoPath.StartsWith( child.Path + "/", StringComparison.Ordinal ) ) )
            .ToList();
        if(commitFiles.Any( file => !targets.Any( child =>
            file.RepoPath.StartsWith( child.Path + "/", StringComparison.Ordinal ) ) )) {
            throw new InvalidOperationException( "対象の所属先を確認できません。選択し直してください。" );
        }

        if(targets.Count != 1) {
            var missionCount = targets.Count( child =>
                child.Name.EndsWith( ".miz", StringComparison.OrdinalIgnoreCase ) ||
                child.Name.EndsWith( ".trk", StringComparison.OrdinalIgnoreCase ) );
            var message = tab.TabType switch
            {
                CategoryType.Aircraft => "機体は1つだけ選択してください。",
                CategoryType.DlcCampaigns => "DLCキャンペーンは1つだけ選択してください。",
                CategoryType.UserMissions when missionCount == targets.Count => "ユーザーミッションは1つだけ選択してください。",
                CategoryType.UserMissions when missionCount == 0 => "ユーザーキャンペーンは1つだけ選択してください。",
                _ => "ミッションとキャンペーンは別々に選択してください。"
            };
            throw new InvalidOperationException( message );
        }

        var name = targets[0].Name;
        Logger.Info( $"サブカテゴリーを算出した。Name={name}" );
        return name;
    }

    /// <summary>
    /// 選択中のタブでチェックされた差分ファイルを収集し、追加・更新または削除のコミット操作に変換する。
    /// </summary>
    private IEnumerable<CommitFile> GetCommitFiles {
        get {
            if(SelectedTabIndex < 0 || SelectedTabIndex >= Tabs.Count) {
                Logger.Warn( $"コミット対象ファイル収集を中断する。SelectedTabIndex={SelectedTabIndex}, TabCount={Tabs.Count}" );
                return [];
            }

            var files = Tabs[SelectedTabIndex]
            .GetCheckedEntries()
            .Where( e => !e.IsDirectory && e.RepoSha != e.LocalSha )
            .Select( entry => new CommitFile()
            {
                Operation = (entry.RepoSha, entry.LocalSha) switch
                {
                    (string _, null ) => CommitOperationType.Delete,
                    _ => CommitOperationType.Upsert,
                },
                LocalPath = Path.Combine( appSettingsService.Settings.TranslateFileDir, entry.Path ),
                RepoPath = entry.Path,
            } )
            .ToList();
            Logger.Info( $"コミット対象ファイルを収集した。件数={files.Count}" );
            return files;
        }
    }

    /// <inheritdoc />
    protected override void NotifyGuardProperties() =>
        NotifyOfPropertyChange( nameof( CanShowCreatePullRequestDialog ) );

    /// <inheritdoc />
    protected override void OnSelectedTabIndexChanged() =>
        NotifyGuardProperties();

    /// <inheritdoc />
    protected override void OnIsFetchingChanged() =>
        NotifyGuardProperties();

    #endregion

}