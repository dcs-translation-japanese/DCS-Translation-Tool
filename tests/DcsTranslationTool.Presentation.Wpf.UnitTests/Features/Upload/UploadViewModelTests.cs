using System.Reflection;
using System.Runtime.ExceptionServices;

using Caliburn.Micro;

using DcsTranslationTool.Application.Enums;
using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Application.Results;
using DcsTranslationTool.Domain.Models;
using DcsTranslationTool.Presentation.Wpf.Features.Upload;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Results;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;
using DcsTranslationTool.Shared.Models;

using FluentResults;

using MaterialDesignThemes.Wpf;

using Moq;

using Action = System.Action;

namespace DcsTranslationTool.Presentation.Wpf.UnitTests.Features.Upload;

/// <summary>UploadViewModel の動作を検証するテストを提供する。</summary>
public sealed class UploadViewModelTests : IDisposable {
    private readonly string _tempDir;
    public UploadViewModelTests() {
        _tempDir = Path.Combine( Path.GetTempPath(), $"DownloadViewModelTests_{Guid.NewGuid():N}" );
        Directory.CreateDirectory( _tempDir );
    }

    public void Dispose() {
        if(Directory.Exists( _tempDir )) {
            Directory.Delete( _tempDir, true );
        }
    }

    /// <summary>ActivateAsync で監視購読が有効化され EntriesChanged に応答することを確認する。</summary>
    [StaFact]
    public async Task ActivateAsyncを呼び出すとファイル監視と購読が開始される() {
        var context = new UploadViewModelTestContext( _tempDir );

        var repoEntries = new FileEntry[]
        {
            new RepoFileEntry( "Example.lua", "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua", false, "repo-sha" )
        };
        var localEntries = new FileEntry[]
        {
            new LocalFileEntry( "Example.lua", "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua", false, "local-sha" )
        };

        context.ApiServiceMock
            .Setup( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( repoEntries ) );

        var viewModel = context.CreateViewModel();

        await viewModel.ActivateAsync( CancellationToken.None );

        Assert.Contains( _tempDir, context.WatchedPaths );
        Assert.Single( context.WatchedPaths );

        await context.RaiseEntriesChangedAsync( localEntries );

        Assert.Equal( localEntries.Select( entry => entry.Path ), viewModel.LocalEntries.Select( entry => entry.Path ) );
        var aircraftTab = viewModel.Tabs.First( tab => tab.TabType == CategoryType.Aircraft );
        Assert.Contains( aircraftTab.Root.Children, child => string.Equals( child?.Name, "A10C", StringComparison.Ordinal ) );
    }

    ///// <summary>ShowCreatePullRequestDialog を呼び出すと PR 成功時にブラウザ起動アクションをスナックバーへ通知することを確認する。</summary>
    //[StaFact]
    //public async Task ShowCreatePullRequestDialogを呼び出すと成功時にブラウザ起動アクションを提供する() {
    //    var context = new UploadViewModelTestContext( _tempDir );
    //    var repoEntries = new FileEntry[]
    //    {
    //        new RepoFileEntry( "Example.lua", "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua", false, "repo-sha" )
    //    };
    //    var localEntries = new FileEntry[]
    //    {
    //        new LocalFileEntry( "Example.lua", "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua", false, "local-sha" )
    //    };

    //    context.ApiServiceMock
    //        .Setup( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ) )
    //        .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( repoEntries ) );

    //    var prUrl = new Uri( "https://github.com/dcs-translation-japanese/translations/pull/123", UriKind.Absolute );
    //    context.ApiServiceMock
    //        .Setup( api => api.CreatePullRequestAsync(
    //            It.IsAny<ApiCreatePullRequestRequest>(),
    //            It.IsAny<CancellationToken>() ) )
    //        .ReturnsAsync( Result.Ok(
    //            new ApiCreatePullRequestOutcome(
    //                true,
    //                "ok",
    //                [
    //                    new ApiCreatePullRequestEntry(
    //                        "feat/aircraft/A10C",
    //                        "commit-sha",
    //                        123,
    //                        prUrl,
    //                        null )
    //                ] ) ) );

    //    context.FileContentInspectorMock
    //        .Setup( inspector => inspector.Inspect( It.IsAny<byte[]>() ) )
    //        .Returns<byte[]>( bytes => new FileContentInfo(
    //            false,
    //            Encoding.UTF8,
    //            1.0,
    //            Encoding.UTF8.GetString( bytes ),
    //            bytes.Length ) );

    //    context.SystemServiceMock
    //        .Setup( service => service.OpenInWebBrowser( It.IsAny<string>() ) );

    //    var viewModel = context.CreateViewModel();

    //    var absolutePath = Path.Combine(
    //        context.TranslateDirectory,
    //        localEntries[0].Path.Replace( "/", Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal ) );
    //    Directory.CreateDirectory( Path.GetDirectoryName( absolutePath )! );
    //    File.WriteAllText( absolutePath, "translated content" );

    //    await viewModel.ActivateAsync( CancellationToken.None );
    //    await context.RaiseEntriesChangedAsync( localEntries );

    //    var aircraftTab = viewModel.Tabs.First( tab => tab.TabType == CategoryType.Aircraft );
    //    var moduleNode = aircraftTab.Root.Children.First( child => string.Equals( child?.Name, "A10C", StringComparison.Ordinal ) );
    //    moduleNode.CheckState = true;

    //    //var dialogSource = new TaskCompletionSource<CreatePullRequestViewModel>();
    //    var dialogSource = new TaskCompletionSource<CreatePullRequestViewModel>(TaskCreationOptions.RunContinuationsAsynchronously);
    //    context.WindowManagerMock
    //        .Setup( manager => manager.ShowDialogAsync(
    //            It.IsAny<object>(),
    //            It.IsAny<object?>(),
    //            It.IsAny<IDictionary<string, object>?>() ) )
    //        .Callback<object, object?, IDictionary<string, object>?>( ( model, _, _ ) => dialogSource.TrySetResult( (CreatePullRequestViewModel)model ) )
    //        .ReturnsAsync( (bool?)true );

    //    var showTask = viewModel.ShowCreatePullRequestDialog();

    //    //var dialogViewModel = await dialogSource.Task;
    //    // ShowDialogAsync が呼ばれない場合にハングさせない
    //    var completed = await Task.WhenAny( dialogSource.Task, Task.Delay( TimeSpan.FromSeconds( 5 ) ) );
    //    Assert.True( ReferenceEquals( completed, dialogSource.Task ), "ShowDialogAsync が呼ばれずダイアログが表示されませんでした。前提条件を見直してください。" );
    //    var dialogViewModel = await dialogSource.Task;

    //    await dialogViewModel.ActivateAsync( CancellationToken.None );
    //    dialogViewModel.PullRequestChangeKinds[0].IsChecked = true;
    //    dialogViewModel.AgreementItems[0].IsAgreed = true;

    //    await dialogViewModel.CreatePullRequest();
    //    await showTask;

    //    // ダイアログが実際に呼ばれたことを保証
    //    context.WindowManagerMock.Verify( m => m.ShowDialogAsync(
    //        It.IsAny<object>(), It.IsAny<object?>(), It.IsAny<IDictionary<string, object>?>() ),
    //        Times.Once );

    //    var notification = context.SnackbarNotifications.Single( tuple => tuple.Message == "Pull Request の作成に成功しました" );
    //    Assert.Equal( "開く", notification.ActionContent );
    //    Assert.NotNull( notification.Handler );

    //    notification.Handler!.Invoke();

    //    context.SystemServiceMock.Verify(
    //        service => service.OpenInWebBrowser( It.Is<string>( url => string.Equals( url, prUrl.ToString(), StringComparison.Ordinal ) ) ),
    //        Times.Once );
    //    context.ApiServiceMock.Verify(
    //        api => api.CreatePullRequestAsync(
    //            It.IsAny<ApiCreatePullRequestRequest>(),
    //            It.IsAny<CancellationToken>() ),
    //        Times.Once );
    //}

    /// <summary>ActivateAsync を再実行しても購読が重複しないことを確認する。</summary>
    [StaFact]
    public async Task ActivateAsyncを再度呼び出すと購読が重複しない() {
        var context = new UploadViewModelTestContext( _tempDir );
        context.ApiServiceMock
            .Setup( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( [] ) );

        var viewModel = context.CreateViewModel();

        await viewModel.ActivateAsync( CancellationToken.None );
        var firstHandler = context.EntriesChangedHandlers.Single();
        Assert.Equal( 1, context.EntriesChangedSubscribeCount );
        Assert.Equal( 0, context.EntriesChangedUnsubscribeCount );

        await viewModel.ActivateAsync( CancellationToken.None );
        var secondHandler = context.EntriesChangedHandlers.Single();
        Assert.NotSame( firstHandler, secondHandler );
        Assert.Equal( 2, context.EntriesChangedSubscribeCount );
        Assert.Equal( 1, context.EntriesChangedUnsubscribeCount );
    }

    #region Fetch例外処理テスト

    /// <summary>Fetch 成功時にタブ構造と通知が更新されることを確認する。</summary>
    [StaFact]
    public async Task Fetchを呼び出すと正常取得時にタブと通知を更新する() {
        var context = new UploadViewModelTestContext( _tempDir );
        var repoEntries = new FileEntry[]
        {
            new RepoFileEntry( "Example.lua", "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua", false, "repo-sha" )
        };

        context.ApiServiceMock
            .Setup( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( repoEntries ) );

        var viewModel = context.CreateViewModel();

        await viewModel.Fetch();

        Assert.Contains( viewModel.RepoEntries, entry => entry.Path == repoEntries[0].Path );
        var aircraftTab = viewModel.Tabs.First( tab => tab.TabType == CategoryType.Aircraft );
        Assert.Contains( aircraftTab.Root.Children, child => string.Equals( child?.Name, "A10C", StringComparison.Ordinal ) );
        Assert.Contains( "リポジトリ一覧の取得が完了しました", context.SnackbarMessages );
        Assert.False( viewModel.IsFetching );

        context.ApiServiceMock.Verify( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ), Times.Once );
        context.SnackbarServiceMock.Verify(
            service => service.Show(
                "リポジトリ一覧の取得が完了しました",
                It.IsAny<string?>(),
                It.IsAny<Action?>(),
                It.IsAny<object?>(),
                It.IsAny<TimeSpan?>() ),
            Times.Once );
    }

    /// <summary>Fetch 失敗時にエラーメッセージが提示されることを確認する。</summary>
    [StaFact]
    public async Task Fetchを呼び出すと取得失敗時にエラーメッセージを通知する() {
        var context = new UploadViewModelTestContext( "C:\\Dummy" );

        context.ApiServiceMock
            .Setup( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( Result.Fail<IReadOnlyList<FileEntry>>( ResultErrorFactory.NotFound( "network error", "TEST_NOT_FOUND" ) ) );

        var viewModel = context.CreateViewModel();

        await viewModel.Fetch();

        Assert.Empty( viewModel.RepoEntries );
        Assert.Empty( viewModel.Tabs );
        Assert.Contains( "リポジトリファイル一覧が見つかりませんでした", context.SnackbarMessages );
        Assert.False( viewModel.IsFetching );

        context.ApiServiceMock.Verify( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ), Times.Once );
    }

    /// <summary>Fetchを呼び出すと例外発生時にスナックバーへ例外メッセージを表示することを確認する。</summary>
    [StaFact]
    public async Task Fetchを呼び出すと例外発生時にスナックバーへ例外メッセージを表示する() {
        var context = new UploadViewModelTestContext( _tempDir );
        context.ApiServiceMock
            .Setup( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ThrowsAsync( new InvalidOperationException( "network error" ) );

        var viewModel = context.CreateViewModel();

        await viewModel.Fetch();

        Assert.Contains( "取得処理で例外が発生しました", context.SnackbarMessages );
        Assert.False( viewModel.IsFetching );

        context.ApiServiceMock.Verify( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ), Times.Once );
        context.SnackbarServiceMock.Verify(
            service => service.Show(
                "取得処理で例外が発生しました",
                It.IsAny<string?>(),
                It.IsAny<Action?>(),
                It.IsAny<object?>(),
                It.IsAny<TimeSpan?>() ),
            Times.Once );
    }

    /// <summary>IsFetching を変更するとガード関連プロパティの PropertyChanged を通知することを確認する。</summary>
    [StaFact]
    public void IsFetchingを変更するとガード関連のPropertyChangedが発火する() {
        var context = new UploadViewModelTestContext( _tempDir );
        var viewModel = context.CreateViewModel();

        List<string> notified = [];
        viewModel.PropertyChanged += ( _, e ) => {
            if(!string.IsNullOrEmpty( e.PropertyName )) notified.Add( e.PropertyName );
        };

        viewModel.IsFetching = true;

        Assert.Contains( nameof( UploadViewModel.IsTreeInteractionEnabled ), notified );
        Assert.Contains( nameof( UploadViewModel.CanShowCreatePullRequestDialog ), notified );
    }

    #endregion

    #region PropertyChanged/ガード

    /// <summary>ルートのチェック状態を変更すると CanShowCreatePullRequestDialog の PropertyChanged を通知することを確認する。</summary>
    [StaFact]
    public async Task Rootチェック状態を変更するとCanShowCreatePullRequestDialogのPropertyChangedが発火する() {
        var context = new UploadViewModelTestContext( _tempDir );
        context.ApiServiceMock
            .Setup( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( CreateAircraftRepoEntries() ) );

        var viewModel = context.CreateViewModel();
        await viewModel.ActivateAsync( CancellationToken.None );
        await WaitUntilAsync(
            () => context.FileEntryServiceMock.Invocations.Any( invocation => invocation.Method.Name == nameof( IFileEntryService.GetEntriesAsync ) ),
            "バックグラウンドのローカル再取得が開始されない。" );

        List<string> notified = [];
        viewModel.PropertyChanged += ( _, e ) => {
            if(!string.IsNullOrEmpty( e.PropertyName )) notified.Add( e.PropertyName );
        };

        SetAircraftRootCheckState( viewModel, true );

        await WaitUntilAsync(
            () => notified.Contains( nameof( UploadViewModel.CanShowCreatePullRequestDialog ), StringComparer.Ordinal ),
            "CanShowCreatePullRequestDialog の PropertyChanged が発火しない。" );
    }

    /// <summary>CanShowCreatePullRequestDialog が状態に応じて真偽を切り替えることを確認する。</summary>
    [StaFact]
    public async Task CanShowCreatePullRequestDialogが状態に応じて真偽を切り替わる() {
        var context = new UploadViewModelTestContext( _tempDir );
        context.ApiServiceMock
            .Setup( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( CreateAircraftRepoEntries() ) );
        var localEntries = new FileEntry[]
        {
            new LocalFileEntry( "Example.lua", AircraftExamplePath, false, "local-sha" )
        };

        var viewModel = context.CreateViewModel();
        context.FileEntryServiceMock
            .Setup( service => service.GetEntriesAsync() )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( localEntries ) );
        await viewModel.ActivateAsync( CancellationToken.None );
        await context.RaiseEntriesChangedAsync( localEntries );

        await WaitUntilAsync( () => {
            SetAircraftModuleCheckState( viewModel, true );
            return viewModel.CanShowCreatePullRequestDialog;
        }, "CreatePullRequestDialog の表示可否が true へ遷移しない。" );

        viewModel.IsFetching = true;
        Assert.False( viewModel.CanShowCreatePullRequestDialog );

        viewModel.IsFetching = false;
        SetAircraftModuleCheckState( viewModel, false );
        await WaitUntilAsync( () => !viewModel.CanShowCreatePullRequestDialog, "CreatePullRequestDialog の表示可否が false へ遷移しない。" );
    }


    //PropertyChanged・ガード系
    // public void Tabsが空またはIndex不正のときCanShowCreatePullRequestDialogがfalseを返す(){}
    #endregion

    #region ライフサイクル処理

    /// <summary>OnDeactivateAsyncを呼び出すと購読解除とリソース解放が行われることを確認する。</summary>
    [StaFact]
    public async Task OnDeactivateAsyncを呼び出すと購読解除とリソース解放が行われる() {
        var context = new UploadViewModelTestContext( _tempDir );
        context.ApiServiceMock
            .Setup( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( [] ) );

        var viewModel = context.CreateViewModel();
        var filter = new TestFilterViewModel();
        viewModel.Filter = filter;

        await viewModel.ActivateAsync( CancellationToken.None );

        Assert.Equal( 1, context.EntriesChangedSubscribeCount );
        Assert.Equal( 1, filter.FiltersChangedAddCount );

        context.FileEntryServiceMock.Invocations.Clear();
        context.SnackbarServiceMock.Invocations.Clear();

        await InvokeOnDeactivateAsync( viewModel, close: true );

        Assert.Equal( context.EntriesChangedSubscribeCount, context.EntriesChangedUnsubscribeCount );
        Assert.Empty( context.EntriesChangedHandlers );
        Assert.Equal( filter.FiltersChangedAddCount, filter.FiltersChangedRemoveCount );

        context.FileEntryServiceMock.Verify( service => service.Dispose(), Times.Once );
        context.SnackbarServiceMock.Verify( service => service.Clear(), Times.Once );
    }

    #endregion

    #region タブ構築

    /// <summary>RefreshTabsを呼び出すと旧タブのイベント購読が解除され新タブへ再登録されることを確認する。</summary>
    [StaFact]
    public async Task RefreshTabsを呼び出すと旧タブ購読を解除して新タブに再登録する() {
        var context = new UploadViewModelTestContext( _tempDir );
        var firstEntries = CreateAircraftRepoEntries();
        var secondEntries = new FileEntry[]
        {
            new RepoFileEntry( "Changed.lua", "DCSWorld/Mods/aircraft/A10C/L10N/Changed.lua", false, "repo-sha-2" )
        };

        context.ApiServiceMock
            .SetupSequence( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( firstEntries ) )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( secondEntries ) );

        var viewModel = context.CreateViewModel();
        await viewModel.Fetch();
        var oldRoot = viewModel.Tabs.First().Root;

        await viewModel.Fetch();
        var newRoot = viewModel.Tabs.First().Root;

        Assert.NotSame( oldRoot, newRoot );

        List<string> notifications = [];
        viewModel.PropertyChanged += ( _, e ) => {
            if(!string.IsNullOrEmpty( e.PropertyName )) notifications.Add( e.PropertyName );
        };

        notifications.Clear();
        oldRoot.CheckState = true;
        Assert.DoesNotContain( nameof( UploadViewModel.CanShowCreatePullRequestDialog ), notifications );

        notifications.Clear();
        newRoot.CheckState = true;
        Assert.Contains( nameof( UploadViewModel.CanShowCreatePullRequestDialog ), notifications );
    }

    /// <summary>RefreshTabsを呼び出すとSelectedTabIndexが範囲内で維持されることを確認する。</summary>
    [StaFact]
    public async Task RefreshTabsを呼び出すとSelectedTabIndexが範囲内で維持される() {
        var context = new UploadViewModelTestContext( _tempDir );
        context.ApiServiceMock
            .SetupSequence( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( CreateAircraftRepoEntries() ) )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( CreateAircraftRepoEntries() ) );
        context.FileEntryServiceMock
            .Setup( service => service.GetEntriesAsync() )
            .ReturnsAsync( Result.Fail<IReadOnlyList<FileEntry>>( "test-local-refresh-disabled" ) );

        var viewModel = context.CreateViewModel();
        viewModel.SelectedTabIndex = -3;

        await viewModel.Fetch();
        Assert.Equal( 0, viewModel.SelectedTabIndex );

        viewModel.SelectedTabIndex = 99;
        await viewModel.Fetch();
        Assert.Equal( viewModel.Tabs.Count - 1, viewModel.SelectedTabIndex );
    }

    /// <summary>RefreshTabsを呼び出すとApplyFilterが実行されることを確認する。</summary>
    [StaFact]
    public async Task RefreshTabsを呼び出すとApplyFilterが実行される() {
        var context = new UploadViewModelTestContext( _tempDir );
        context.ApiServiceMock
            .Setup( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( CreateAircraftRepoEntries() ) );

        var filterMock = new Mock<IFilterViewModel>();
        filterMock
            .Setup( filter => filter.GetActiveTypes() )
            .Returns( [FileChangeType.Modified] );

        var viewModel = context.CreateViewModel();
        viewModel.Filter = filterMock.Object;

        await viewModel.Fetch();

        filterMock.Verify( filter => filter.GetActiveTypes(), Times.AtLeastOnce );
    }

    #endregion

    #region フィルタ適用

    /// <summary>FiltersChangedイベント発火時にApplyFilterが実行されCanShowCreatePullRequestDialogのPropertyChangedが発火することを確認する。</summary>
    [StaFact]
    public async Task FiltersChangedイベントを受信するとフィルターを再適用してガードを通知する() {
        var context = new UploadViewModelTestContext( _tempDir );
        context.ApiServiceMock
            .Setup( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( CreateAircraftRepoEntries() ) );

        var filter = new TestFilterViewModel( FileChangeType.Modified );
        var viewModel = context.CreateViewModel();
        viewModel.Filter = filter;

        var notifiedGate = new object();
        List<string> notified = [];
        viewModel.PropertyChanged += ( _, e ) => {
            if(string.IsNullOrEmpty( e.PropertyName )) {
                return;
            }

            lock(notifiedGate) {
                notified.Add( e.PropertyName );
            }
        };

        await viewModel.ActivateAsync( CancellationToken.None );
        lock(notifiedGate) {
            notified.Clear();
        }
        var before = filter.GetActiveTypesCallCount;

        filter.SetActiveTypes( FileChangeType.LocalOnly );
        filter.RaiseFiltersChanged();

        Assert.True( filter.GetActiveTypesCallCount > before );
        await WaitUntilAsync( () => {
            lock(notifiedGate) {
                return notified.Contains( nameof( UploadViewModel.CanShowCreatePullRequestDialog ), StringComparer.Ordinal );
            }
        }, "CanShowCreatePullRequestDialog の PropertyChanged が発火しない。" );
    }

    /// <summary>ApplyFilterを呼び出すと可視な種別のみIsVisibleがtrueになり、それ以外はfalseになることを確認する。</summary>
    [StaFact]
    public async Task ApplyFilterを呼び出すと対象種別のみ可視化する() {
        var context = new UploadViewModelTestContext( _tempDir );
        context.ApiServiceMock
            .Setup( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( CreateAircraftRepoEntriesWithRepoOnly() ) );

        var filter = new TestFilterViewModel( FileChangeType.Modified, FileChangeType.LocalOnly, FileChangeType.RepoOnly );
        var viewModel = context.CreateViewModel();
        viewModel.Filter = filter;

        await viewModel.ActivateAsync( CancellationToken.None );

        var localEntries = new FileEntry[]
        {
            new LocalFileEntry( "Example.lua", AircraftExamplePath, false, "local-sha" ),
            new LocalFileEntry( "LocalOnly.lua", AircraftLocalOnlyPath, false, "local-only-sha" )
        };
        await context.RaiseEntriesChangedAsync( localEntries );

        var aircraftTab = viewModel.Tabs.First( tab => tab.TabType == CategoryType.Aircraft );
        var modifiedNode = FindNodeByPath( aircraftTab.Root, AircraftExamplePath );
        var localOnlyNode = FindNodeByPath( aircraftTab.Root, AircraftLocalOnlyPath );
        var repoOnlyNode = FindNodeByPath( aircraftTab.Root, AircraftRepoOnlyPath );

        Assert.NotNull( modifiedNode );
        Assert.NotNull( localOnlyNode );
        Assert.NotNull( repoOnlyNode );

        filter.SetActiveTypes( FileChangeType.LocalOnly );
        filter.RaiseFiltersChanged();

        Assert.True( localOnlyNode!.IsVisible );
        Assert.False( modifiedNode!.IsVisible );
        Assert.False( repoOnlyNode!.IsVisible );

        filter.SetActiveTypes( FileChangeType.RepoOnly );
        filter.RaiseFiltersChanged();

        Assert.True( repoOnlyNode.IsVisible );
        Assert.False( localOnlyNode.IsVisible );
    }

    /// <summary>ApplyFilterを呼び出すと該当子ノードを持つディレクトリも可視化されることを確認する。</summary>
    [StaFact]
    public async Task ApplyFilterを呼び出すと子孫一致ディレクトリも可視化する() {
        var context = new UploadViewModelTestContext( _tempDir );
        context.ApiServiceMock
            .Setup( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( CreateAircraftRepoEntriesWithRepoOnly() ) );

        var filter = new TestFilterViewModel( FileChangeType.Modified, FileChangeType.LocalOnly );
        var viewModel = context.CreateViewModel();
        viewModel.Filter = filter;

        await viewModel.ActivateAsync( CancellationToken.None );

        var localEntries = new FileEntry[]
        {
            new LocalFileEntry( "Example.lua", AircraftExamplePath, false, "local-sha" ),
            new LocalFileEntry( "LocalOnly.lua", AircraftLocalOnlyPath, false, "local-only-sha" )
        };
        await context.RaiseEntriesChangedAsync( localEntries );

        var aircraftTab = viewModel.Tabs.First( tab => tab.TabType == CategoryType.Aircraft );
        var targetDirectory = FindNodeByPath( aircraftTab.Root, AircraftLocalizationDirectoryPath );

        Assert.NotNull( targetDirectory );

        filter.SetActiveTypes( FileChangeType.LocalOnly );
        filter.RaiseFiltersChanged();

        Assert.True( targetDirectory!.IsVisible );

        filter.SetActiveTypes();
        filter.RaiseFiltersChanged();

        Assert.False( targetDirectory.IsVisible );
    }

    #endregion

    #region サブカテゴリ取得

    /// <summary>GetSubCategoryを呼び出すと条件に一致するノードのNameを返すことを確認する。</summary>
    [StaFact]
    public async Task GetSubCategoryを呼び出すと条件に一致するノードのNameを返す() {
        var context = new UploadViewModelTestContext( _tempDir );
        context.ApiServiceMock
            .Setup( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( CreateAircraftRepoEntries() ) );

        var viewModel = context.CreateViewModel();
        await viewModel.ActivateAsync( CancellationToken.None );

        var localEntries = new FileEntry[] {
            new LocalFileEntry( "Example.lua", AircraftExamplePath, false, "local-sha" )
        };
        await context.RaiseEntriesChangedAsync( localEntries );

        var aircraftTab = viewModel.Tabs.First( tab => tab.TabType == CategoryType.Aircraft );
        var moduleNode = aircraftTab.Root.Children.First( child => string.Equals( child?.Name, "A10C", StringComparison.Ordinal ) );
        Assert.NotNull( moduleNode );
        moduleNode!.CheckState = true;

        var subCategory = InvokeGetSubCategory( viewModel );

        Assert.Equal( "A10C", subCategory );
    }

    /// <summary>GetSubCategoryを呼び出すと対象ノードが0件のとき例外を送出することを確認する。</summary>
    [StaFact]
    public async Task GetSubCategoryを呼び出すと対象ノードが0件のとき例外を送出する() {
        var context = new UploadViewModelTestContext( _tempDir );
        context.ApiServiceMock
            .Setup( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( CreateAircraftRepoEntries() ) );

        var viewModel = context.CreateViewModel();
        await viewModel.ActivateAsync( CancellationToken.None );

        var localEntries = new FileEntry[] {
            new LocalFileEntry( "Example.lua", AircraftExamplePath, false, "local-sha" )
        };
        await context.RaiseEntriesChangedAsync( localEntries );

        Assert.Throws<InvalidOperationException>( () => InvokeGetSubCategory( viewModel ) );
    }

    /// <summary>GetSubCategoryを呼び出すと対象ノードが複数件のとき例外を送出することを確認する。</summary>
    [StaFact]
    public async Task GetSubCategoryを呼び出すと対象ノードが複数件のとき例外を送出する() {
        var context = new UploadViewModelTestContext( _tempDir );
        context.ApiServiceMock
            .Setup( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( CreateAircraftRepoEntries() ) );

        var viewModel = context.CreateViewModel();
        await viewModel.ActivateAsync( CancellationToken.None );

        var localEntries = new FileEntry[] {
            new LocalFileEntry( "Example.lua", AircraftExamplePath, false, "local-sha" ),
            new LocalFileEntry( "Alt.lua", AircraftSecondModulePath, false, "local-alt-sha" )
        };
        await context.RaiseEntriesChangedAsync( localEntries );

        var aircraftTab = viewModel.Tabs.First( tab => tab.TabType == CategoryType.Aircraft );
        var moduleNode = aircraftTab.Root.Children.First( child => string.Equals( child?.Name, "A10C", StringComparison.Ordinal ) );
        var secondNode = aircraftTab.Root.Children.First( child => string.Equals( child?.Name, AircraftSecondModuleName, StringComparison.Ordinal ) );
        Assert.NotNull( moduleNode );
        Assert.NotNull( secondNode );
        moduleNode!.CheckState = true;
        secondNode!.CheckState = true;

        Assert.Throws<InvalidOperationException>( () => InvokeGetSubCategory( viewModel ) );
    }

    #endregion

    #region コミットファイル抽出

    /// <summary>GetCommitFilesを取得するとUpsert対象が正しく含まれLocalPathがTranslateFileDir基点になることを確認する。</summary>
    [StaFact]
    public async Task GetCommitFilesを取得するとUpsert対象が含まれLocalPathが基点パスで生成される() {
        var context = new UploadViewModelTestContext( _tempDir );
        context.ApiServiceMock
            .Setup( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( CreateAircraftRepoEntries() ) );

        var viewModel = context.CreateViewModel();
        await viewModel.ActivateAsync( CancellationToken.None );

        var localEntries = new FileEntry[]
        {
            new LocalFileEntry( "Example.lua", AircraftExamplePath, false, "local-upsert-sha" )
        };
        await context.RaiseEntriesChangedAsync( localEntries );

        var aircraftTab = viewModel.Tabs.First( tab => tab.TabType == CategoryType.Aircraft );
        viewModel.SelectedTabIndex = GetTabIndexByType( viewModel, CategoryType.Aircraft );
        var fileNode = FindNodeByPath( aircraftTab.Root, AircraftExamplePath );
        Assert.NotNull( fileNode );
        fileNode!.CheckState = true;

        var files = InvokeGetCommitFiles( viewModel );

        var localPath = Path.Combine( context.TranslateDirectory, AircraftExamplePath );
        var commitFile = Assert.Single( files );
        Assert.Equal( CommitOperationType.Upsert, commitFile.Operation );
        Assert.Equal( AircraftExamplePath, commitFile.RepoPath );
        Assert.Equal( localPath, commitFile.LocalPath );
    }

    /// <summary>GetCommitFilesを取得するとDelete対象が正しく含まれることを確認する。</summary>
    [StaFact]
    public async Task GetCommitFilesを取得するとDelete対象が含まれる() {
        var context = new UploadViewModelTestContext( _tempDir );
        context.ApiServiceMock
            .Setup( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( CreateAircraftRepoEntries() ) );

        var viewModel = context.CreateViewModel();
        await viewModel.ActivateAsync( CancellationToken.None );
        await WaitUntilAsync(
            () => context.FileEntryServiceMock.Invocations.Any( invocation => invocation.Method.Name == nameof( IFileEntryService.GetEntriesAsync ) ),
            "バックグラウンドのローカル再取得が開始されない。" );
        await context.RaiseEntriesChangedAsync( [] );

        var aircraftTab = viewModel.Tabs.First( tab => tab.TabType == CategoryType.Aircraft );
        viewModel.SelectedTabIndex = GetTabIndexByType( viewModel, CategoryType.Aircraft );
        var fileNode = FindNodeByPath( aircraftTab.Root, AircraftExamplePath );
        Assert.NotNull( fileNode );
        fileNode!.CheckState = true;

        var files = InvokeGetCommitFiles( viewModel );

        var commitFile = Assert.Single( files );
        Assert.Equal( CommitOperationType.Delete, commitFile.Operation );
        Assert.Equal( AircraftExamplePath, commitFile.RepoPath );
    }

    /// <summary>GetCommitFilesを取得するとRepoShaとLocalShaが同一のファイルは含まれないことを確認する。</summary>
    [StaFact]
    public async Task GetCommitFilesを取得すると同一SHAのファイルは含まれない() {
        var context = new UploadViewModelTestContext( _tempDir );
        context.ApiServiceMock
            .Setup( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( CreateAircraftRepoEntries() ) );

        var viewModel = context.CreateViewModel();
        await viewModel.ActivateAsync( CancellationToken.None );

        var localEntries = new FileEntry[]
        {
            new LocalFileEntry( "Example.lua", AircraftExamplePath, false, "repo-sha" )
        };
        await context.RaiseEntriesChangedAsync( localEntries );

        var aircraftTab = viewModel.Tabs.First( tab => tab.TabType == CategoryType.Aircraft );
        viewModel.SelectedTabIndex = GetTabIndexByType( viewModel, CategoryType.Aircraft );
        var fileNode = FindNodeByPath( aircraftTab.Root, AircraftExamplePath );
        Assert.NotNull( fileNode );
        fileNode!.CheckState = true;
        Assert.False( fileNode.CheckState );

        var files = InvokeGetCommitFiles( viewModel );

        Assert.Empty( files );
    }

    /// <summary>GetCommitFilesを取得するとディレクトリは除外されることを確認する。</summary>
    [StaFact]
    public async Task GetCommitFilesを取得するとディレクトリは除外される() {
        var context = new UploadViewModelTestContext( _tempDir );
        context.ApiServiceMock
            .Setup( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( CreateAircraftRepoEntriesWithRepoOnly() ) );

        var viewModel = context.CreateViewModel();
        await viewModel.ActivateAsync( CancellationToken.None );

        var localEntries = new FileEntry[]
        {
            new LocalFileEntry( "Example.lua", AircraftExamplePath, false, "modified-sha" )
        };
        await context.RaiseEntriesChangedAsync( localEntries );

        var aircraftTab = viewModel.Tabs.First( tab => tab.TabType == CategoryType.Aircraft );
        viewModel.SelectedTabIndex = GetTabIndexByType( viewModel, CategoryType.Aircraft );

        var directoryNode = FindNodeByPath( aircraftTab.Root, AircraftLocalizationDirectoryPath );
        Assert.NotNull( directoryNode );
        directoryNode!.CheckState = true;

        var files = InvokeGetCommitFiles( viewModel );

        Assert.NotEmpty( files );
        Assert.DoesNotContain( files, file => string.Equals( file.RepoPath, AircraftLocalizationDirectoryPath, StringComparison.Ordinal ) );
    }

    #endregion

    #region ツリー構築ユーティリティ

    /// <summary>FileEntryTreeServiceが多段ディレクトリ構造を正しく構築することを確認する。</summary>
    [StaFact]
    public void FileEntryTreeServiceを呼び出すと多段ディレクトリ構造を正しく作成する() {
        var logger = new Mock<ILoggingService>().Object;
        var sut = new FileEntryTreeService( logger );
        var entry = new RepoFileEntry( "Example.lua", AircraftExamplePath, false, "repo-sha" );
        var tabs = sut.BuildTabs( [], [entry], ChangeTypeMode.Upload );
        var aircraftTab = tabs.First( tab => tab.TabType == CategoryType.Aircraft );
        var root = aircraftTab.Root;

        var localizationNode = FindNodeByPath( root, AircraftLocalizationDirectoryPath );
        Assert.NotNull( localizationNode );
        Assert.True( localizationNode!.IsDirectory );

        var moduleNode = FindNodeByPath( root, AircraftModuleDirectoryPath );
        Assert.NotNull( moduleNode );
        Assert.True( moduleNode!.IsDirectory );

        var fileNode = FindNodeByPath( root, AircraftExamplePath );
        Assert.NotNull( fileNode );
        Assert.False( fileNode!.IsDirectory );
        Assert.Equal( AircraftExamplePath, fileNode.Path );
    }

    /// <summary>FileEntryTreeServiceが同一パスノードを重複作成しないことを確認する。</summary>
    [StaFact]
    public void FileEntryTreeServiceを呼び出すと既存ノードを重複作成しない() {
        var loggerMock = new Mock<ILoggingService>();
        var sut = new FileEntryTreeService( loggerMock.Object );
        var repoEntry = new RepoFileEntry( "Example.lua", AircraftExamplePath, false, "repo-sha" );
        var localEntry = new LocalFileEntry( "Example.lua", AircraftExamplePath, false, "local-sha" );
        var tabs = sut.BuildTabs( [localEntry], [repoEntry], ChangeTypeMode.Upload );
        var aircraftTab = tabs.First( tab => tab.TabType == CategoryType.Aircraft );
        var root = aircraftTab.Root;

        var samePathNodes = EnumerateNodes( root )
            .Where( node => string.Equals( node.Path, AircraftExamplePath, StringComparison.Ordinal ) )
            .ToList();
        Assert.Single( samePathNodes );
    }

    #endregion

    #region ダイアログ起動（ShowCreatePullRequestDialog）

    /// <summary>単一対象の削除だけのPRは確認に承認した場合だけダイアログを開く。</summary>
    [StaTheory]
    [InlineData( CategoryType.Aircraft, "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua", "A10C", true )]
    [InlineData( CategoryType.DlcCampaigns, "DCSWorld/Mods/campaigns/Campaign1/Mission1.miz/l10n/JP/dictionary", "Campaign1", true )]
    [InlineData( CategoryType.UserMissions, "UserMissions/Mission1.miz/l10n/JP/dictionary", "Mission1.miz", true )]
    [InlineData( CategoryType.UserMissions, "UserMissions/Campaign1/Mission1.miz/l10n/JP/dictionary", "Campaign1", true )]
    [InlineData( CategoryType.Aircraft, "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua", "A10C", false )]
    public async Task 削除だけのPRは単一対象を確認してから進む( CategoryType category, string path, string subCategory, bool confirmed ) {
        var context = new UploadViewModelTestContext( _tempDir );
        var viewModel = await CreateDeletionUploadViewModelAsync( context, category, [path] );
        var order = new List<string>();
        context.DialogServiceMock
            .Setup( service => service.ContinueCancelDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
            .Callback<ConfirmationDialogParameters>( parameters => {
                Assert.Contains( "削除だけ", parameters.Message );
                Assert.Contains( subCategory, parameters.Message );
                order.Add( "confirm" );
            } )
            .ReturnsAsync( confirmed );
        context.DialogServiceMock
            .Setup( service => service.CreatePullRequestDialogShowAsync(
                It.IsAny<CreatePullRequestDialogParameters>(), It.IsAny<CancellationToken>() ) )
            .Callback<CreatePullRequestDialogParameters, CancellationToken>( ( parameters, _ ) => {
                Assert.Equal( subCategory, parameters.SubCategory );
                Assert.All( parameters.CommitFiles, file => Assert.Equal( CommitOperationType.Delete, file.Operation ) );
                order.Add( "create" );
            } )
            .ReturnsAsync( new CreatePullRequestResult() );

        await viewModel.ShowCreatePullRequestDialog();

        Assert.Equal( confirmed ? ["confirm", "create"] : new[] { "confirm" }, order );
    }

    /// <summary>複数の対象にまたがる削除は確認もPRダイアログも開かず理由を通知する。</summary>
    [StaTheory]
    [InlineData( CategoryType.Aircraft, "DCSWorld/Mods/aircraft/A10C/L10N/a.lua", "DCSWorld/Mods/aircraft/F16C/L10N/b.lua", "機体は1つだけ選択してください。" )]
    [InlineData( CategoryType.DlcCampaigns, "DCSWorld/Mods/campaigns/Campaign1/Mission1.miz/l10n/JP/dictionary", "DCSWorld/Mods/campaigns/Campaign2/Mission1.miz/l10n/JP/dictionary", "DLCキャンペーンは1つだけ選択してください。" )]
    [InlineData( CategoryType.UserMissions, "UserMissions/Mission1.miz/l10n/JP/dictionary", "UserMissions/Mission2.miz/l10n/JP/dictionary", "ユーザーミッションは1つだけ選択してください。" )]
    [InlineData( CategoryType.UserMissions, "UserMissions/Mission1.MIZ/l10n/JP/dictionary", "UserMissions/Mission2.TRK/l10n/JP/dictionary", "ユーザーミッションは1つだけ選択してください。" )]
    [InlineData( CategoryType.UserMissions, "UserMissions/Campaign1/Mission1.miz/l10n/JP/dictionary", "UserMissions/Campaign2/Mission1.miz/l10n/JP/dictionary", "ユーザーキャンペーンは1つだけ選択してください。" )]
    [InlineData( CategoryType.UserMissions, "UserMissions/Campaign1/Mission1.miz/l10n/JP/dictionary", "UserMissions/Mission1.miz/l10n/JP/dictionary", "ミッションとキャンペーンは別々に選択してください。" )]
    [InlineData( CategoryType.UserMissions, "UserMissions/dictionary", "UserMissions/Mission1.miz/l10n/JP/dictionary", "対象の所属先を確認できません。選択し直してください。" )]
    public async Task 複数対象の削除だけのPRは禁止理由を表示して中断する( CategoryType category, string firstPath, string secondPath, string expectedMessage ) {
        var context = new UploadViewModelTestContext( _tempDir );
        var viewModel = await CreateDeletionUploadViewModelAsync( context, category, [firstPath, secondPath] );
        var initialCount = context.SnackbarMessages.Count;

        await viewModel.ShowCreatePullRequestDialog();

        Assert.Equal( initialCount + 1, context.SnackbarMessages.Count );
        Assert.Equal( expectedMessage, context.SnackbarMessages[^1] );
        context.DialogServiceMock.Verify( service => service.ContinueCancelDialogShowAsync(
            It.IsAny<ConfirmationDialogParameters>() ), Times.Never );
        context.DialogServiceMock.Verify( service => service.CreatePullRequestDialogShowAsync(
            It.IsAny<CreatePullRequestDialogParameters>(), It.IsAny<CancellationToken>() ), Times.Never );
    }

    /// <summary>同じキャンペーン内の複数ミッションの削除は単一対象として扱う。</summary>
    [StaFact]
    public async Task 同じユーザーキャンペーンの複数ミッションを削除できる() {
        var context = new UploadViewModelTestContext( _tempDir );
        var paths = new[] {
            "UserMissions/Campaign1/Mission1.miz/l10n/JP/dictionary",
            "UserMissions/Campaign1/Mission2.miz/l10n/JP/dictionary"
        };
        var viewModel = await CreateDeletionUploadViewModelAsync( context, CategoryType.UserMissions, paths );

        await viewModel.ShowCreatePullRequestDialog();

        context.DialogServiceMock.Verify( service => service.CreatePullRequestDialogShowAsync(
            It.Is<CreatePullRequestDialogParameters>( parameters => parameters.SubCategory == "Campaign1" && parameters.CommitFiles.Count() == 2 ),
            It.IsAny<CancellationToken>() ), Times.Once );
    }

    /// <summary>所属先の検証と送信は選択された実ファイルだけを対象にする。</summary>
    [StaFact]
    public async Task 別の機体に未選択の削除があっても選択した機体だけを送信する() {
        var context = new UploadViewModelTestContext( _tempDir );
        var viewModel = await CreateDeletionUploadViewModelAsync(
            context, CategoryType.Aircraft, [AircraftExamplePath, AircraftSecondModulePath] );
        var root = viewModel.Tabs[viewModel.SelectedTabIndex].Root;
        root.CheckState = false;
        FindNodeByPath( root, AircraftExamplePath )!.CheckState = true;

        await viewModel.ShowCreatePullRequestDialog();

        context.DialogServiceMock.Verify( service => service.CreatePullRequestDialogShowAsync(
            It.Is<CreatePullRequestDialogParameters>( parameters => parameters.SubCategory == "A10C" &&
                parameters.CommitFiles.Single().RepoPath == AircraftExamplePath ),
            It.IsAny<CancellationToken>() ), Times.Once );
    }

    /// <summary>送信対象がない場合は理由を表示し確認もPR作成も行わない。</summary>
    [StaFact]
    public async Task 選択が空のときは理由を表示して中断する() {
        var context = new UploadViewModelTestContext( _tempDir );
        var viewModel = await CreateDeletionUploadViewModelAsync( context, CategoryType.Aircraft, [AircraftExamplePath] );
        viewModel.Tabs[viewModel.SelectedTabIndex].Root.CheckState = false;

        await viewModel.ShowCreatePullRequestDialog();

        Assert.Contains( "変更ファイルがありません", context.SnackbarMessages[^1] );
        context.DialogServiceMock.Verify( service => service.ContinueCancelDialogShowAsync(
            It.IsAny<ConfirmationDialogParameters>() ), Times.Never );
        context.DialogServiceMock.Verify( service => service.CreatePullRequestDialogShowAsync(
            It.IsAny<CreatePullRequestDialogParameters>(), It.IsAny<CancellationToken>() ), Times.Never );
    }

    /// <summary>ShowCreatePullRequestDialogを呼び出すと削除ファイルを含む場合確認ダイアログを表示しキャンセルで中断することを確認する。</summary>
    [StaFact]
    public async Task ShowCreatePullRequestDialogを呼び出すと削除ファイルを含む場合確認ダイアログを表示しキャンセルで中断する() {
        var context = new UploadViewModelTestContext( _tempDir );
        var repoEntries = new FileEntry[]
        {
            new RepoFileEntry( "RepoOnly.lua", AircraftRepoOnlyPath, false, "repo-only-sha" )
        };
        var localEntries = new FileEntry[]
        {
            new LocalFileEntry( "LocalOnly.lua", AircraftLocalOnlyPath, false, "local-sha" )
        };

        context.ApiServiceMock
            .Setup( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( repoEntries ) );

        var viewModel = context.CreateViewModel();

        context.DialogServiceMock
            .Setup( service => service.ContinueCancelDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
            .ReturnsAsync( false );

        await viewModel.ActivateAsync( CancellationToken.None );
        await context.RaiseEntriesChangedAsync( localEntries );

        var aircraftTab = viewModel.Tabs.First( tab => tab.TabType == CategoryType.Aircraft );
        viewModel.SelectedTabIndex = GetTabIndexByType( viewModel, CategoryType.Aircraft );
        var moduleNode = FindNodeByPath( aircraftTab.Root, AircraftModuleDirectoryPath )
            ?? throw new InvalidOperationException( "対象モジュールが見つからない。" );
        moduleNode.CheckState = true;

        var initialNotifications = context.SnackbarNotifications.Count;

        await viewModel.ShowCreatePullRequestDialog();

        context.DialogServiceMock.Verify(
            service => service.ContinueCancelDialogShowAsync(
                It.Is<ConfirmationDialogParameters>( parameters =>
                    parameters.Title == "削除確認" &&
                    parameters.Message.Contains( "削除予定", StringComparison.Ordinal ) ) ),
            Times.Once );

        context.DialogServiceMock.Verify(
            service => service.CreatePullRequestDialogShowAsync(
                It.IsAny<CreatePullRequestDialogParameters>(),
                It.IsAny<CancellationToken>() ),
            Times.Never );

        Assert.Equal( initialNotifications, context.SnackbarNotifications.Count );
    }

    /// <summary>ShowCreatePullRequestDialogを呼び出すとPullRequest成功時にスナックバーへ成功メッセージを表示することを確認する。</summary>
    [StaFact]
    public async Task ShowCreatePullRequestDialogを呼び出すとPullRequest成功時にスナックバーへ成功メッセージを表示する() {
        var context = new UploadViewModelTestContext( _tempDir );
        var viewModel = await CreateCheckedUploadViewModelAsync(
            context,
            CreateAircraftRepoEntries(),
            [new LocalFileEntry( "Example.lua", AircraftExamplePath, false, "local-sha" )] );

        context.DialogServiceMock
            .Setup( service => service.CreatePullRequestDialogShowAsync(
                It.IsAny<CreatePullRequestDialogParameters>(),
                It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( new CreatePullRequestResult { IsOk = true } );

        var initialCount = context.SnackbarNotifications.Count;

        await viewModel.ShowCreatePullRequestDialog();

        Assert.Equal( initialCount + 1, context.SnackbarNotifications.Count );
        var notification = context.SnackbarNotifications[^1];
        Assert.Equal( "Pull Request の作成に成功しました", notification.Message );
    }

    /// <summary>ShowCreatePullRequestDialogを呼び出すと成功時にURLありでブラウザ起動アクションを提供することを確認する。</summary>
    [StaFact]
    public async Task ShowCreatePullRequestDialogを呼び出すと成功時にURLありでブラウザ起動アクションを提供する() {
        var context = new UploadViewModelTestContext( _tempDir );
        var viewModel = await CreateCheckedUploadViewModelAsync(
            context,
            CreateAircraftRepoEntries(),
            [new LocalFileEntry( "Example.lua", AircraftExamplePath, false, "local-sha" )] );

        const string prUrl = "https://github.com/dcs-translation-japanese/translations/pull/1";
        context.DialogServiceMock
            .Setup( service => service.CreatePullRequestDialogShowAsync(
                It.IsAny<CreatePullRequestDialogParameters>(),
                It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( new CreatePullRequestResult { IsOk = true, PrUrl = prUrl } );

        var initialCount = context.SnackbarNotifications.Count;

        await viewModel.ShowCreatePullRequestDialog();

        Assert.Equal( initialCount + 1, context.SnackbarNotifications.Count );
        var notification = context.SnackbarNotifications[^1];
        Assert.Equal( "Pull Request の作成に成功しました", notification.Message );
        Assert.Equal( "開く", notification.ActionContent );
        Assert.NotNull( notification.Handler );

        notification.Handler!.Invoke();

        context.SystemServiceMock.Verify(
            service => service.OpenInWebBrowser(
                It.Is<string>( url => string.Equals( url, prUrl, StringComparison.Ordinal ) ) ),
            Times.Once );
    }

    /// <summary>ShowCreatePullRequestDialogを呼び出すと成功時にURLなしでアクションなしの通知を表示することを確認する。</summary>
    [StaFact]
    public async Task ShowCreatePullRequestDialogを呼び出すと成功時にURLなしでアクションなしの通知を表示する() {
        var context = new UploadViewModelTestContext( _tempDir );
        var viewModel = await CreateCheckedUploadViewModelAsync(
            context,
            CreateAircraftRepoEntries(),
            [new LocalFileEntry( "Example.lua", AircraftExamplePath, false, "local-sha" )] );

        context.DialogServiceMock
            .Setup( service => service.CreatePullRequestDialogShowAsync(
                It.IsAny<CreatePullRequestDialogParameters>(),
                It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( new CreatePullRequestResult { IsOk = true } );

        var initialCount = context.SnackbarNotifications.Count;

        await viewModel.ShowCreatePullRequestDialog();

        Assert.Equal( initialCount + 1, context.SnackbarNotifications.Count );
        var notification = context.SnackbarNotifications[^1];
        Assert.Null( notification.ActionContent );
        Assert.Null( notification.Handler );
    }

    /// <summary>ShowCreatePullRequestDialogを呼び出すとキャンセル結果でキャンセルメッセージを表示することを確認する。</summary>
    [StaFact]
    public async Task ShowCreatePullRequestDialogを呼び出すとキャンセル結果でキャンセルメッセージを表示する() {
        var context = new UploadViewModelTestContext( _tempDir );
        var viewModel = await CreateCheckedUploadViewModelAsync(
            context,
            CreateAircraftRepoEntries(),
            [new LocalFileEntry( "Example.lua", AircraftExamplePath, false, "local-sha" )] );

        context.DialogServiceMock
            .Setup( service => service.CreatePullRequestDialogShowAsync(
                It.IsAny<CreatePullRequestDialogParameters>(),
                It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( new CreatePullRequestResult
            {
                Errors = [new OperationCanceledException()]
            } );

        var initialCount = context.SnackbarNotifications.Count;

        await viewModel.ShowCreatePullRequestDialog();

        Assert.Equal( initialCount + 1, context.SnackbarNotifications.Count );
        var notification = context.SnackbarNotifications[^ 1];
        Assert.Equal( "Pull Request の作成をキャンセルしました", notification.Message );
    }

    /// <summary>ShowCreatePullRequestDialogを呼び出すと失敗メッセージ付き結果で失敗通知を表示することを確認する。</summary>
    [StaFact]
    public async Task ShowCreatePullRequestDialogを呼び出すと失敗メッセージ付き結果で失敗通知を表示する() {
        var context = new UploadViewModelTestContext( _tempDir );
        var viewModel = await CreateCheckedUploadViewModelAsync(
            context,
            CreateAircraftRepoEntries(),
            [new LocalFileEntry( "Example.lua", AircraftExamplePath, false, "local-sha" )] );

        context.DialogServiceMock
            .Setup( service => service.CreatePullRequestDialogShowAsync(
                It.IsAny<CreatePullRequestDialogParameters>(),
                It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( new CreatePullRequestResult
            {
                Errors = [new InvalidOperationException( "backend error" )]
            } );

        var initialCount = context.SnackbarNotifications.Count;

        await viewModel.ShowCreatePullRequestDialog();

        Assert.Equal( initialCount + 1, context.SnackbarNotifications.Count );
        var notification = context.SnackbarNotifications[^ 1];
        Assert.Equal( "Pull Request の作成に失敗しました: backend error", notification.Message );
    }

    /// <summary>ShowCreatePullRequestDialogを呼び出すと失敗メッセージなし結果で汎用失敗通知を表示することを確認する。</summary>
    [StaFact]
    public async Task ShowCreatePullRequestDialogを呼び出すと失敗メッセージなし結果で汎用失敗通知を表示する() {
        var context = new UploadViewModelTestContext( _tempDir );
        var viewModel = await CreateCheckedUploadViewModelAsync(
            context,
            CreateAircraftRepoEntries(),
            [new LocalFileEntry( "Example.lua", AircraftExamplePath, false, "local-sha" )] );

        context.DialogServiceMock
            .Setup( service => service.CreatePullRequestDialogShowAsync(
                It.IsAny<CreatePullRequestDialogParameters>(),
                It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( new CreatePullRequestResult
            {
                Errors = []
            } );

        var initialCount = context.SnackbarNotifications.Count;

        await viewModel.ShowCreatePullRequestDialog();

        Assert.Equal( initialCount + 1, context.SnackbarNotifications.Count );
        var notification = context.SnackbarNotifications[^ 1];
        Assert.Equal( "Pull Request の作成に失敗しました", notification.Message );
    }

    /// <summary>ShowCreatePullRequestDialogを呼び出すとOperationCanceledException発生時にキャンセル通知を表示することを確認する。</summary>
    [StaFact]
    public async Task ShowCreatePullRequestDialogを呼び出すとOperationCanceledException発生時にキャンセル通知を表示する() {
        var context = new UploadViewModelTestContext( _tempDir );
        var viewModel = await CreateCheckedUploadViewModelAsync(
            context,
            CreateAircraftRepoEntries(),
            [new LocalFileEntry( "Example.lua", AircraftExamplePath, false, "local-sha" )] );

        context.DialogServiceMock
            .Setup( service => service.CreatePullRequestDialogShowAsync(
                It.IsAny<CreatePullRequestDialogParameters>(),
                It.IsAny<CancellationToken>() ) )
            .ThrowsAsync( new OperationCanceledException() );

        var initialCount = context.SnackbarNotifications.Count;

        await viewModel.ShowCreatePullRequestDialog();

        Assert.Equal( initialCount + 1, context.SnackbarNotifications.Count );
        var notification = context.SnackbarNotifications[^ 1];
        Assert.Equal( "Pull Request の作成をキャンセルしました", notification.Message );
    }

    /// <summary>ShowCreatePullRequestDialogを呼び出すと一般例外発生時に例外通知を表示することを確認する。</summary>
    [StaFact]
    public async Task ShowCreatePullRequestDialogを呼び出すと一般例外発生時に例外通知を表示する() {
        var context = new UploadViewModelTestContext( _tempDir );
        var viewModel = await CreateCheckedUploadViewModelAsync(
            context,
            CreateAircraftRepoEntries(),
            [new LocalFileEntry( "Example.lua", AircraftExamplePath, false, "local-sha" )] );

        context.DialogServiceMock
            .Setup( service => service.CreatePullRequestDialogShowAsync(
                It.IsAny<CreatePullRequestDialogParameters>(),
                It.IsAny<CancellationToken>() ) )
            .ThrowsAsync( new InvalidOperationException( "dialog error" ) );

        var initialCount = context.SnackbarNotifications.Count;

        await viewModel.ShowCreatePullRequestDialog();

        Assert.Equal( initialCount + 1, context.SnackbarNotifications.Count );
        var notification = context.SnackbarNotifications[^ 1];
        Assert.Equal( "Pull Request ダイアログで例外が発生しました: dialog error", notification.Message );
    }

    /// <summary>ShowCreatePullRequestDialogを呼び出すと全ての通知がDispatcherService経由で呼ばれることを確認する。</summary>
    [StaFact]
    public async Task ShowCreatePullRequestDialogを呼び出すと全ての通知がDispatcherService経由で呼ばれる() {
        var context = new UploadViewModelTestContext( _tempDir );

        var dispatcherScope = false;
        context.DispatcherServiceMock
            .Setup( service => service.InvokeAsync( It.IsAny<Func<Task>>() ) )
            .Returns<Func<Task>>( func => InvokeThroughScopeAsync( func ) );

        context.SnackbarServiceMock
            .Setup( service => service.Show(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Action?>(),
                It.IsAny<object?>(),
                It.IsAny<TimeSpan?>() ) )
            .Callback<string, string?, Action?, object?, TimeSpan?>(
                ( message, action, handler, _, _ ) => {
                    Assert.True( dispatcherScope, "Snackbar は DispatcherService 経由で呼ばれるべき。" );
                    context.SnackbarMessages.Add( message );
                    context.SnackbarNotifications.Add( (message, action, handler) );
                } );

        var viewModel = await CreateCheckedUploadViewModelAsync(
            context,
            CreateAircraftRepoEntries(),
            [new LocalFileEntry( "Example.lua", AircraftExamplePath, false, "local-sha" )] );

        context.DialogServiceMock
            .Setup( service => service.CreatePullRequestDialogShowAsync(
                It.IsAny<CreatePullRequestDialogParameters>(),
                It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( new CreatePullRequestResult { IsOk = true } );

        await viewModel.ShowCreatePullRequestDialog();

        Assert.Contains( "Pull Request の作成に成功しました", context.SnackbarMessages );

        context.DispatcherServiceMock.Verify(
            service => service.InvokeAsync( It.IsAny<Func<Task>>() ),
            Times.AtLeastOnce );

        Task InvokeThroughScopeAsync( Func<Task> func ) {
            dispatcherScope = true;
            return RunAsync();

            async Task RunAsync() {
                try {
                    await func();
                }
                finally {
                    dispatcherScope = false;
                }
            }
        }
    }
    #endregion

    #region その他

    /// <summary>OpenDirectoryを呼び出すとSystemServiceで設定ディレクトリが開かれることを確認する。</summary>
    [StaFact]
    public void OpenDirectoryを呼び出すとSystemServiceで設定ディレクトリが開かれる() {
        var context = new UploadViewModelTestContext( _tempDir );
        var viewModel = context.CreateViewModel();

        viewModel.OpenDirectory();

        context.SystemServiceMock.Verify(
            service => service.OpenDirectory(
                It.Is<string>( path => string.Equals( path, context.TranslateDirectory, StringComparison.Ordinal ) ) ),
            Times.Once );
    }

    #endregion

    #region Helpers

    private const string AircraftExamplePath = "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua";
    private const string AircraftLocalOnlyPath = "DCSWorld/Mods/aircraft/A10C/L10N/LocalOnly.lua";
    private const string AircraftRepoOnlyPath = "DCSWorld/Mods/aircraft/A10C/L10N/RepoOnly.lua";
    private const string AircraftLocalizationDirectoryPath = "DCSWorld/Mods/aircraft/A10C/L10N";
    private const string AircraftModuleDirectoryPath = "DCSWorld/Mods/aircraft/A10C";
    private const string AircraftSecondModulePath = "DCSWorld/Mods/aircraft/F16C/L10N/Alt.lua";
    private const string AircraftSecondModuleName = "F16C";

    private static IReadOnlyList<FileEntry> CreateAircraftRepoEntries() => [
            new RepoFileEntry( "Example.lua", AircraftExamplePath, false, "repo-sha" )
        ];

    private static IReadOnlyList<FileEntry> CreateAircraftRepoEntriesWithRepoOnly() => [
            new RepoFileEntry( "Example.lua", AircraftExamplePath, false, "repo-sha" ),
            new RepoFileEntry( "RepoOnly.lua", AircraftRepoOnlyPath, false, "repo-only-sha" )
        ];

    private static IFileEntryViewModel? FindNodeByPath( IFileEntryViewModel root, string path ) =>
        EnumerateNodes( root ).FirstOrDefault( node => string.Equals( node.Path, path, StringComparison.Ordinal ) );

    private static IEnumerable<IFileEntryViewModel> EnumerateNodes( IFileEntryViewModel node ) {
        yield return node;
        foreach(var child in node.Children) {
            if(child is null) continue;
            foreach(var descendant in EnumerateNodes( child )) yield return descendant;
        }
    }

    /// <summary>テスト用に Aircraft タブをチェック済みにした UploadViewModel を生成する。</summary>
    private static async Task<UploadViewModel> CreateCheckedUploadViewModelAsync(
        UploadViewModelTestContext context,
        IReadOnlyList<FileEntry> repoEntries,
        IReadOnlyList<FileEntry> localEntries
    ) {
        context.ApiServiceMock
            .Setup( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( repoEntries ) );

        var viewModel = context.CreateViewModel();
        await viewModel.ActivateAsync( CancellationToken.None );
        await context.RaiseEntriesChangedAsync( localEntries );

        var aircraftTab = viewModel.Tabs.First( tab => tab.TabType == CategoryType.Aircraft );
        viewModel.SelectedTabIndex = GetTabIndexByType( viewModel, CategoryType.Aircraft );
        var moduleNode = FindNodeByPath( aircraftTab.Root, AircraftModuleDirectoryPath )
            ?? throw new InvalidOperationException( "対象モジュールが見つからない。" );
        moduleNode.CheckState = true;
        return viewModel;
    }

    /// <summary>リポジトリにのみ存在するファイルを選択したUploadを構築する。</summary>
    private static async Task<UploadViewModel> CreateDeletionUploadViewModelAsync(
        UploadViewModelTestContext context, CategoryType category, IReadOnlyList<string> paths
    ) {
        context.ApiServiceMock
            .Setup( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( paths
                .Select( path => (FileEntry)new RepoFileEntry( Path.GetFileName( path ), path, false, "repo-sha" ) ).ToArray() ) );
        var viewModel = context.CreateViewModel();
        await viewModel.ActivateAsync( CancellationToken.None );
        await WaitUntilAsync(
            () => context.FileEntryServiceMock.Invocations.Any( invocation => invocation.Method.Name == nameof( IFileEntryService.GetEntriesAsync ) ),
            "バックグラウンドのローカル再取得が開始されない。" );
        await context.RaiseEntriesChangedAsync( [] );
        viewModel.SelectedTabIndex = GetTabIndexByType( viewModel, category );
        viewModel.Tabs[viewModel.SelectedTabIndex].Root.CheckState = true;
        return viewModel;
    }

    /// <summary>指定条件が満たされるまで待機する。</summary>
    private static async Task WaitUntilAsync( Func<bool> predicate, string timeoutMessage, int timeoutMs = 3000 ) {
        var startedAt = DateTime.UtcNow;
        while((DateTime.UtcNow - startedAt).TotalMilliseconds < timeoutMs) {
            if(predicate()) {
                return;
            }

            await Task.Delay( 20 );
        }

        throw new TimeoutException( timeoutMessage );
    }

    /// <summary>指定カテゴリのタブインデックスを取得する。</summary>
    private static int GetTabIndexByType( UploadViewModel viewModel, CategoryType tabType ) {
        var index = viewModel.Tabs
            .Select( (tab, i) => (tab, i) )
            .Where( tuple => tuple.tab.TabType == tabType )
            .Select( tuple => tuple.i )
            .DefaultIfEmpty( -1 )
            .First();
        Assert.True( index >= 0, $"対象タブが見つからない。Type={tabType}" );
        return index;
    }

    /// <summary>現在の Aircraft タブで対象モジュールのチェック状態を設定する。</summary>
    private static void SetAircraftModuleCheckState( UploadViewModel viewModel, bool? checkState ) {
        viewModel.SelectedTabIndex = GetTabIndexByType( viewModel, CategoryType.Aircraft );
        var aircraftTab = viewModel.Tabs[viewModel.SelectedTabIndex];
        var moduleNode = FindNodeByPath( aircraftTab.Root, AircraftModuleDirectoryPath )
            ?? throw new InvalidOperationException( "対象モジュールが見つからない。" );
        moduleNode.CheckState = checkState;
    }

    /// <summary>現在の Aircraft タブのルートチェック状態を設定する。</summary>
    private static void SetAircraftRootCheckState( UploadViewModel viewModel, bool? checkState ) {
        viewModel.SelectedTabIndex = GetTabIndexByType( viewModel, CategoryType.Aircraft );
        var aircraftTab = viewModel.Tabs[viewModel.SelectedTabIndex];
        aircraftTab.Root.CheckState = checkState;
    }

    private sealed class TestFilterViewModel : IFilterViewModel {
        private List<FileChangeType?> _activeTypes;
        private EventHandler? _filtersChanged;

        public TestFilterViewModel( params FileChangeType?[] initialTypes ) {
            _activeTypes = initialTypes is { Length: > 0 }
                ? [.. initialTypes]
                : [FileChangeType.Modified];
        }

        public bool All { get; set; }
        public bool Unchanged { get; set; }
        public bool RepoOnly { get; set; }
        public bool LocalOnly { get; set; }
        public bool Modified { get; set; }

        public int FiltersChangedAddCount { get; private set; }
        public int FiltersChangedRemoveCount { get; private set; }
        public event EventHandler? FiltersChanged {
            add {
                FiltersChangedAddCount++;
                _filtersChanged += value;
            }
            remove {
                FiltersChangedRemoveCount++;
                _filtersChanged -= value;
            }
        }

        public int GetActiveTypesCallCount { get; private set; }

        public IEnumerable<FileChangeType?> GetActiveTypes() {
            GetActiveTypesCallCount++;
            return _activeTypes;
        }

        public void SetActiveTypes( params FileChangeType?[] types ) {
            _activeTypes = types is { Length: > 0 }
                ? [.. types]
                : [];
        }

        public void RaiseFiltersChanged() => _filtersChanged?.Invoke( this, EventArgs.Empty );
    }

    /// <summary>GetCommitFilesをリフレクション経由で取得する。</summary>
    private static IReadOnlyList<CommitFile> InvokeGetCommitFiles( UploadViewModel viewModel ) {
        var property = typeof( UploadViewModel ).GetProperty( "GetCommitFiles", BindingFlags.Instance | BindingFlags.NonPublic )
            ?? throw new InvalidOperationException( "GetCommitFiles が見つからない。" );
        if(property.GetValue( viewModel ) is not IEnumerable<CommitFile> files) {
            throw new InvalidOperationException( "GetCommitFiles の結果を取得できない。" );
        }
        return [.. files];
    }

    /// <summary>GetSubCategoryをリフレクション経由で呼び出す。</summary>
    private static string InvokeGetSubCategory( UploadViewModel viewModel ) {
        var method = typeof( UploadViewModel ).GetMethod( "GetSubCategory", BindingFlags.Instance | BindingFlags.NonPublic )
            ?? throw new InvalidOperationException( "GetSubCategory が見つからない。" );
        try {
            return (string)(method.Invoke( viewModel, [InvokeGetCommitFiles( viewModel )] ) ?? throw new InvalidOperationException( "GetSubCategory が null を返した。" ));
        }
        catch(TargetInvocationException ex) when(ex.InnerException is not null) {
            ExceptionDispatchInfo.Capture( ex.InnerException ).Throw();
            throw;
        }
    }

    /// <summary>OnDeactivateAsyncをリフレクション経由で実行する。</summary>
    private static Task InvokeOnDeactivateAsync( UploadViewModel viewModel, bool close ) {
        var method = typeof( UploadViewModel ).GetMethod( "OnDeactivateAsync", BindingFlags.Instance | BindingFlags.NonPublic )
            ?? throw new InvalidOperationException( "OnDeactivateAsync が見つからない。" );
        return (Task)(method.Invoke( viewModel, [close, CancellationToken.None] ) ?? Task.CompletedTask);
    }

    /// <summary>UploadViewModel の依存関係を構築するコンテキストを表す。</summary>
    private sealed class UploadViewModelTestContext {
        private readonly AppSettings _appSettings;
        private readonly List<Func<IReadOnlyList<FileEntry>, Task>> _entriesChangedHandlers = [];

        public Mock<IApiService> ApiServiceMock { get; } = new( MockBehavior.Strict );
        public Mock<IAppSettingsService> AppSettingsServiceMock { get; } = new();
        public Mock<IDispatcherService> DispatcherServiceMock { get; } = new();
        public Mock<IDialogService> DialogServiceMock { get; } = new();
        public Mock<IFileContentInspector> FileContentInspectorMock { get; } = new();
        public Mock<IFileEntryService> FileEntryServiceMock { get; } = new();
        public Mock<ILoggingService> LoggingServiceMock { get; } = new();
        public Mock<ISnackbarService> SnackbarServiceMock { get; } = new();
        public Mock<ISystemService> SystemServiceMock { get; } = new();
        public Mock<IWindowManager> WindowManagerMock { get; } = new();
        public Mock<ISnackbarMessageQueue> SnackbarMessageQueueMock { get; } = new();

        public string TranslateDirectory => _appSettings.TranslateFileDir;
        public List<string> SnackbarMessages { get; } = [];
        public List<(string Message, string? ActionContent, Action? Handler)> SnackbarNotifications { get; } = [];
        public List<string> WatchedPaths { get; } = [];
        public IReadOnlyList<Func<IReadOnlyList<FileEntry>, Task>> EntriesChangedHandlers => _entriesChangedHandlers;
        public int EntriesChangedSubscribeCount { get; private set; }
        public int EntriesChangedUnsubscribeCount { get; private set; }

        public UploadViewModelTestContext( string translateDirectory ) {
            _appSettings = new AppSettings { TranslateFileDir = translateDirectory };
            ConfigureDefaultMocks();
        }

        private void ConfigureDefaultMocks() {
            AppSettingsServiceMock
                .SetupGet( service => service.Settings )
                .Returns( _appSettings );

            DispatcherServiceMock
                .Setup( service => service.InvokeAsync( It.IsAny<Func<Task>>() ) )
                .Returns<Func<Task>>( func => func() );

            FileEntryServiceMock
                .Setup( service => service.Dispose() );
            FileEntryServiceMock
                .Setup( service => service.GetEntriesAsync() )
                .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( [] ) );
            FileEntryServiceMock
                .Setup( service => service.GetChildrenRecursiveAsync( It.IsAny<string>() ) )
                .ReturnsAsync( Result.Ok<IEnumerable<FileEntry>>( [] ) );
            FileEntryServiceMock
                .Setup( service => service.Watch( It.IsAny<string>() ) )
                .Callback<string>( path => WatchedPaths.Add( path ) );
            FileEntryServiceMock
                .SetupAdd( service => service.EntriesChanged += It.IsAny<Func<IReadOnlyList<FileEntry>, Task>>() )
                .Callback<Func<IReadOnlyList<FileEntry>, Task>>( handler => {
                    EntriesChangedSubscribeCount++;
                    _entriesChangedHandlers.Add( handler );
                } );
            FileEntryServiceMock
                .SetupRemove( service => service.EntriesChanged -= It.IsAny<Func<IReadOnlyList<FileEntry>, Task>>() )
                .Callback<Func<IReadOnlyList<FileEntry>, Task>>( handler => {
                    EntriesChangedUnsubscribeCount++;
                    _entriesChangedHandlers.Remove( handler );
                } );

            SnackbarServiceMock
                .SetupGet( service => service.MessageQueue )
                .Returns( SnackbarMessageQueueMock.Object );
            SnackbarServiceMock
                .Setup( service => service.Show(
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<Action?>(),
                    It.IsAny<object?>(),
                    It.IsAny<TimeSpan?>() ) )
                .Callback<string, string?, Action?, object?, TimeSpan?>(
                    ( message, action, handler, _, _ ) => {
                        SnackbarMessages.Add( message );
                        SnackbarNotifications.Add( (message, action, handler) );
                    } );
            SnackbarServiceMock
                .Setup( service => service.Clear() );

            DialogServiceMock
                .Setup( service => service.ContinueCancelDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
                .ReturnsAsync( true );
            DialogServiceMock
                .Setup( service => service.CreatePullRequestDialogShowAsync(
                    It.IsAny<CreatePullRequestDialogParameters>(),
                    It.IsAny<CancellationToken>() ) )
                .ReturnsAsync( new CreatePullRequestResult() );
        }

        public UploadViewModel CreateViewModel() {
            var fileEntryTreeService = new FileEntryTreeService( LoggingServiceMock.Object );
            var fileEntryWatcherLifecycle = new FileEntryWatcherLifecycle(
                AppSettingsServiceMock.Object,
                FileEntryServiceMock.Object,
                LoggingServiceMock.Object
            );
            return new(
                ApiServiceMock.Object,
                AppSettingsServiceMock.Object,
                DialogServiceMock.Object,
                DispatcherServiceMock.Object,
                FileEntryServiceMock.Object,
                fileEntryWatcherLifecycle,
                fileEntryTreeService,
                LoggingServiceMock.Object,
                SnackbarServiceMock.Object,
                SystemServiceMock.Object );
        }

        public async Task RaiseEntriesChangedAsync( IReadOnlyList<FileEntry> entries ) {
            if(_entriesChangedHandlers.Count == 0) {
                throw new InvalidOperationException( "EntriesChanged の購読が設定されていない。" );
            }

            foreach(var handler in _entriesChangedHandlers.ToArray()) {
                await handler( entries );
            }
        }
    }

    #endregion
}