using System.Text.RegularExpressions;

using Caliburn.Micro;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Application.Results;
using DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Results;
using DcsTranslationTool.Resources;
using DcsTranslationTool.Shared.Models;

using FluentResults;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.UnitTests.Features.TranslationCreation;

/// <summary>
/// TranslationCreationViewModel の動作を検証する。
/// </summary>
public sealed partial class TranslationCreationViewModelTests {
    private static readonly DateTimeOffset PoRevisionDate = new( 2026, 3, 13, 14, 25, 0, TimeSpan.FromHours( 9 ) );

    [Fact]
    public void コンストラクタは選択中アーカイブ絶対パスを保持する() {
        var context = new TranslationCreationViewModelTestContext();
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        Assert.Equal( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz", viewModel.ArchiveFullPath );
    }

    [Fact]
    public void コンストラクタは空白のアーカイブ絶対パスを拒否する() {
        var context = new TranslationCreationViewModelTestContext();

        Assert.Throws<ArgumentException>( () => context.CreateViewModel( string.Empty ) );
    }

    [Fact]
    public void コンストラクタは保存済みウィンドウ状態を初期化する() {
        var context = new TranslationCreationViewModelTestContext( new AppSettings
        {
            TranslateFileDir = @"C:\Translate",
            DcsWorldInstallDir = @"C:\DCSWorld",
            TranslationCreationWindowWidth = 1400,
            TranslationCreationWindowHeight = 920,
            TranslationCreationDictionaryPaneRatio = 3.5,
            TranslationCreationWrapDictionaryDetailsText = false
        } );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        Assert.Equal( 1400, viewModel.WindowWidth );
        Assert.Equal( 920, viewModel.WindowHeight );
        Assert.Equal( 3.5, viewModel.DictionaryPaneRatio );
        Assert.False( viewModel.IsDictionaryDetailsWrapEnabled );
    }

    [Fact]
    public void コンストラクタは不正な保存済みウィンドウ状態を補正する() {
        var context = new TranslationCreationViewModelTestContext( new AppSettings
        {
            TranslateFileDir = @"C:\Translate",
            DcsWorldInstallDir = @"C:\DCSWorld",
            TranslationCreationWindowWidth = double.NaN,
            TranslationCreationWindowHeight = 0,
            TranslationCreationDictionaryPaneRatio = double.NaN
        } );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        Assert.Equal( TranslationCreationViewModel.DefaultWindowWidth, viewModel.WindowWidth );
        Assert.Equal( TranslationCreationViewModel.DefaultWindowHeight, viewModel.WindowHeight );
        Assert.Equal( TranslationCreationViewModel.DefaultDictionaryPaneRatio, viewModel.DictionaryPaneRatio );
    }

    [Fact]
    public void WindowWidth設定は最小値に補正して保存する() {
        var context = new TranslationCreationViewModelTestContext();
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        viewModel.WindowWidth = 320;

        Assert.Equal( TranslationCreationViewModel.MinWindowWidth, viewModel.WindowWidth );
        Assert.Equal( TranslationCreationViewModel.MinWindowWidth, context.Settings.TranslationCreationWindowWidth );
    }

    [Fact]
    public void WindowHeight設定は既定値へ補正して保存する() {
        var context = new TranslationCreationViewModelTestContext();
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        viewModel.WindowHeight = double.NaN;

        Assert.Equal( TranslationCreationViewModel.DefaultWindowHeight, viewModel.WindowHeight );
        Assert.Equal( TranslationCreationViewModel.DefaultWindowHeight, context.Settings.TranslationCreationWindowHeight );
    }

    [Fact]
    public void WindowHeight設定は最小値に補正して保存する() {
        var context = new TranslationCreationViewModelTestContext();
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        viewModel.WindowHeight = 320;

        Assert.Equal( TranslationCreationViewModel.MinWindowHeight, viewModel.WindowHeight );
        Assert.Equal( TranslationCreationViewModel.MinWindowHeight, context.Settings.TranslationCreationWindowHeight );
    }

    [Fact]
    public void DictionaryPaneRatio設定は範囲内へ補正して保存する() {
        var context = new TranslationCreationViewModelTestContext();
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        viewModel.DictionaryPaneRatio = 100;

        Assert.Equal( TranslationCreationViewModel.MaxDictionaryPaneRatio, viewModel.DictionaryPaneRatio );
        Assert.Equal( TranslationCreationViewModel.MaxDictionaryPaneRatio, context.Settings.TranslationCreationDictionaryPaneRatio );
    }

    [Fact]
    public void 初期状態では選択項目詳細は空文字になる() {
        var context = new TranslationCreationViewModelTestContext();
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        Assert.Null( viewModel.SelectedDictionaryItem );
        Assert.Equal( string.Empty, viewModel.SelectedOriginal );
        Assert.Equal( string.Empty, viewModel.SelectedTranslated );
        Assert.False( viewModel.CanEditSelectedTranslated );
        Assert.True( viewModel.IsDictionaryDetailsWrapEnabled );
    }

    [Fact]
    public void IsDictionaryDetailsWrapEnabled設定はAppSettingsへ同期する() {
        var context = new TranslationCreationViewModelTestContext();
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        viewModel.IsDictionaryDetailsWrapEnabled = false;

        Assert.False( viewModel.IsDictionaryDetailsWrapEnabled );
        Assert.False( context.Settings.TranslationCreationWrapDictionaryDetailsText );
    }

    [Fact]
    public void TranslationCreationViewModelはView専用契約を実装する() {
        var context = new TranslationCreationViewModelTestContext();
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        Assert.IsType<ITranslationCreationViewModel>( viewModel, exactMatch: false );
    }

    [Fact]
    public async Task ConfirmCloseAsyncは初期読込直後に確認なしでtrueを返す() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );

        var result = await viewModel.ConfirmCloseAsync();

        Assert.True( result );
        context.DialogServiceMock.Verify( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ), Times.Never );
    }

    [Fact]
    public async Task ConfirmCloseAsyncはSelectedTranslated変更時に確認ダイアログを表示する() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" )
                ] ) );
        context.DialogServiceMock
            .Setup( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
            .ReturnsAsync( ConfirmationDialogResult.Cancel );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.SelectedDictionaryItem = viewModel.DictionaryItems.Single();
        viewModel.SelectedTranslated = "translated";

        var result = await viewModel.ConfirmCloseAsync();

        Assert.False( result );
        context.DialogServiceMock.Verify( service => service.ConfirmationDialogShowAsync(
            It.Is<ConfirmationDialogParameters>( parameters =>
                parameters.Title == Strings_Translation.CreateTranslationCloseConfirmationTitle
                && parameters.Message == Strings_Translation.CreateTranslationCloseConfirmationMessage
                && parameters.ConfirmButtonText == Strings_Translation.CreateTranslationCloseConfirmationConfirmButtonText
                && parameters.CancelButtonText == Strings_Translation.CreateTranslationCloseConfirmationCancelButtonText
                && parameters.DialogIdentifier == TranslationCreationDialogHostIdentifiers.Confirmation ) ), Times.Once );
    }

    [Fact]
    public async Task ConfirmCloseAsyncは保留中のSelectedTranslated編集も差分判定に含める() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" )
                ] ) );
        context.DialogServiceMock
            .Setup( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
            .ReturnsAsync( ConfirmationDialogResult.Confirm );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.SelectedDictionaryItem = viewModel.DictionaryItems.Single();
        viewModel.SelectedTranslated = "translated";

        var result = await viewModel.ConfirmCloseAsync();

        Assert.True( result );
        Assert.Equal( string.Empty, viewModel.DictionaryItems.Single().Translated );
        context.DialogServiceMock.Verify( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ), Times.Once );
    }

    [Fact]
    public async Task ConfirmCloseAsyncはIsEnabled変更時に確認ダイアログを表示する() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" )
                ] ) );
        context.DialogServiceMock
            .Setup( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
            .ReturnsAsync( ConfirmationDialogResult.Confirm );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.DictionaryItems.Single().IsEnabled = false;

        var result = await viewModel.ConfirmCloseAsync();

        Assert.True( result );
        context.DialogServiceMock.Verify( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ), Times.Once );
    }

    [Fact]
    public async Task HasPendingChangesForCloseはSelectedTranslatedを元に戻すとfalseへ戻る() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.SelectedDictionaryItem = viewModel.DictionaryItems.Single();
        viewModel.SelectedTranslated = "translated";

        Assert.True( viewModel.HasPendingChangesForClose() );

        viewModel.SelectedTranslated = string.Empty;

        Assert.False( viewModel.HasPendingChangesForClose() );
    }

    [Fact]
    public async Task HasPendingChangesForCloseは保留中のSelectedTranslatedをモデルへ反映しない() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.SelectedDictionaryItem = viewModel.DictionaryItems.Single();
        viewModel.SelectedTranslated = "translated";

        Assert.True( viewModel.HasPendingChangesForClose() );
        Assert.Equal( string.Empty, viewModel.DictionaryItems.Single().Translated );
    }

    [Fact]
    public async Task HasPendingChangesForCloseは保留中判定だけではFilterRefreshしない() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.ShowOnlyUntranslated = true;
        var visibleVersion = viewModel.VisibleDictionaryItemsVersion;
        viewModel.SelectedDictionaryItem = viewModel.DictionaryItems.Single();
        viewModel.SelectedTranslated = "translated";

        Assert.True( viewModel.HasPendingChangesForClose() );
        Assert.Equal( visibleVersion, viewModel.VisibleDictionaryItemsVersion );
    }

    [Fact]
    public async Task HasPendingChangesForCloseはIsEnabledを元に戻すとfalseへ戻る() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        var row = viewModel.DictionaryItems.Single();
        row.IsEnabled = false;

        Assert.True( viewModel.HasPendingChangesForClose() );

        row.IsEnabled = true;

        Assert.False( viewModel.HasPendingChangesForClose() );
    }

    [Fact]
    public async Task HasPendingChangesForCloseはフィルター状態変更だけではtrueにならない() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.ShowEnabledItems = false;
        viewModel.ShowEnabledItems = true;
        viewModel.ShowOnlyUntranslated = true;
        viewModel.ShowOnlyUntranslated = false;

        Assert.False( viewModel.HasPendingChangesForClose() );
    }

    [Fact]
    public void CopyOriginalToClipboardはOriginalをクリップボードへ設定してSnackbarを表示する() {
        var context = new TranslationCreationViewModelTestContext();
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );
        var row = new TranslationDictionaryItemRowViewModel( new TranslationDictionaryItem( "DictKey_sortie_1", "original" ) );

        viewModel.CopyOriginalToClipboard( row );

        context.SystemServiceMock.Verify( service => service.SetClipboardText( "original" ), Times.Once );
        var notification = Assert.Single( viewModel.MessageQueue.QueuedMessages );
        Assert.Equal( Strings_Translation.CreateTranslationOriginalCopiedMessage, notification.Content );
    }

    [Fact]
    public void CopyOriginalToClipboardはnull入力時に何もしない() {
        var context = new TranslationCreationViewModelTestContext();
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        viewModel.CopyOriginalToClipboard( null );

        context.SystemServiceMock.Verify( service => service.SetClipboardText( It.IsAny<string>() ), Times.Never );
        Assert.Empty( viewModel.MessageQueue.QueuedMessages );
    }

    [Fact]
    public void CopyOriginalToClipboardは空文字のOriginalでは何もしない() {
        var context = new TranslationCreationViewModelTestContext();
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );
        var row = new TranslationDictionaryItemRowViewModel( new TranslationDictionaryItem( "DictKey_sortie_1", string.Empty ) );

        viewModel.CopyOriginalToClipboard( row );

        context.SystemServiceMock.Verify( service => service.SetClipboardText( It.IsAny<string>() ), Times.Never );
        Assert.Empty( viewModel.MessageQueue.QueuedMessages );
    }

    [Fact]
    public void CopyOriginalToClipboardは空白のみのOriginalでは何もしない() {
        var context = new TranslationCreationViewModelTestContext();
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );
        var row = new TranslationDictionaryItemRowViewModel( new TranslationDictionaryItem( "DictKey_sortie_1", " \t " ) );

        viewModel.CopyOriginalToClipboard( row );

        context.SystemServiceMock.Verify( service => service.SetClipboardText( It.IsAny<string>() ), Times.Never );
        Assert.Empty( viewModel.MessageQueue.QueuedMessages );
    }

    [Fact]
    public async Task InitializeAfterShownAsyncはdictionary項目一覧を読み込む() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" ),
                    new TranslationDictionaryItem( "DictKey_sortie_2", "o2" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );

        Assert.Equal( 2, viewModel.DictionaryItems.Count );
        Assert.True( viewModel.HasDictionaryItems );
        Assert.False( viewModel.HasStatusMessage );
        Assert.True( viewModel.ShowEnabledItems );
        Assert.True( viewModel.ShowDisabledItems );
        Assert.False( viewModel.ShowOnlyUntranslated );
        Assert.True( viewModel.HidePossibleNonTranslationTargets );
        Assert.True( viewModel.HideEmptyOriginal );
        Assert.Equal( "o1", viewModel.DictionaryItems[0].Original );
        Assert.Equal( string.Empty, viewModel.DictionaryItems[0].Translated );
        Assert.All( viewModel.DictionaryItems, item => Assert.True( item.IsEnabled ) );
        Assert.True( viewModel.CanExport );
    }

    [Fact]
    public async Task InitializeAfterShownAsyncはVisibleDictionaryItemsVersion変更を通知する() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );
        List<string?> notifiedPropertyNames = [];
        viewModel.PropertyChanged += ( _, e ) => notifiedPropertyNames.Add( e.PropertyName );

        await ActivateAndInitializeAfterShownAsync( viewModel );

        Assert.Contains( nameof( TranslationCreationViewModel.VisibleDictionaryItemsVersion ), notifiedPropertyNames );
        Assert.Equal(
            ["DictKey_sortie_1"],
            [.. GetVisibleDictionaryItems( viewModel ).Select( item => item.Key )] );
    }

    [Fact]
    public async Task SelectedTranslated変更はShowOnlyUntranslatedがfalseならFilterRefreshしない() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        var visibleVersion = viewModel.VisibleDictionaryItemsVersion;
        viewModel.SelectedDictionaryItem = viewModel.DictionaryItems.Single();
        viewModel.SelectedTranslated = "translated";
        viewModel.FlushPendingSelectedTranslatedEdit();

        Assert.Equal( visibleVersion, viewModel.VisibleDictionaryItemsVersion );
    }

    [Fact]
    public async Task IsEnabled変更は表示条件が不変ならFilterRefreshしない() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        var visibleVersion = viewModel.VisibleDictionaryItemsVersion;
        viewModel.DictionaryItems.Single().IsEnabled = false;

        Assert.Equal( visibleVersion, viewModel.VisibleDictionaryItemsVersion );
    }

    [Fact]
    public async Task OnActivatedAsyncは起動時にarchive読込を実行しない() {
        var context = new TranslationCreationViewModelTestContext();
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );

        Assert.False( viewModel.IsLoading );
        Assert.Empty( viewModel.DictionaryItems );
        Assert.False( viewModel.HasDictionaryItems );
        context.TranslationDictionaryServiceMock.Verify( service => service.LoadArchiveDictionaries( It.IsAny<string>() ), Times.Never );
    }

    [Fact]
    public async Task InitializeAfterShownAsyncは起動用集約APIを1回だけ呼び出す() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadArchiveDictionaries( It.IsAny<string>() ) )
            .Returns( Result.Ok( new TranslationArchiveDictionaries(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" )
                ],
                false,
                [] ) ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );
        await viewModel.InitializeAfterShownAsync( TestContext.Current.CancellationToken );

        context.TranslationDictionaryServiceMock.Verify( service => service.LoadArchiveDictionaries( It.IsAny<string>() ), Times.Once );
        context.TranslationDictionaryServiceMock.Verify( service => service.LoadDictionary( It.IsAny<string>() ), Times.Never );
        context.TranslationDictionaryServiceMock.Verify( service => service.HasArchiveEntry( It.IsAny<string>(), It.IsAny<string>() ), Times.Never );
    }

    [Fact]
    public async Task InitializeAfterShownAsyncは重複実行しても1回分として扱う() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadArchiveDictionaries( It.IsAny<string>() ) )
            .Returns( Result.Ok( new TranslationArchiveDictionaries(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" )
                ],
                false,
                [] ) ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );
        await viewModel.InitializeAfterShownAsync( TestContext.Current.CancellationToken );
        await viewModel.InitializeAfterShownAsync( TestContext.Current.CancellationToken );

        context.TranslationDictionaryServiceMock.Verify( service => service.LoadArchiveDictionaries( It.IsAny<string>() ), Times.Once );
    }

    [Fact]
    public async Task HandleWindowLoadedAsyncはJPdictionaryがないとき警告を表示しない() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadArchiveDictionaries( It.IsAny<string>() ) )
            .Returns( Result.Ok( new TranslationArchiveDictionaries(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" )
                ],
                false,
                [] ) ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.HandleWindowLoadedAsync( TestContext.Current.CancellationToken );

        context.DialogServiceMock.Verify( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ), Times.Never );
    }

    [Fact]
    public async Task HandleWindowLoadedAsyncはJPdictionary項目が空のときDEFAULTdictionaryで継続する() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadArchiveDictionaries( It.IsAny<string>() ) )
            .Returns( Result.Ok( new TranslationArchiveDictionaries(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" )
                ],
                true,
                [] ) ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.HandleWindowLoadedAsync( TestContext.Current.CancellationToken );

        Assert.Single( viewModel.DictionaryItems );
        Assert.Equal( string.Empty, viewModel.DictionaryItems.Single().Translated );
        context.DialogServiceMock.Verify( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ), Times.Never );
    }

    [Fact]
    public async Task HandleWindowLoadedAsyncはJPdictionary確認でも追加のarchive読込を行わない() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadArchiveDictionaries( It.IsAny<string>() ) )
            .Returns( Result.Ok( new TranslationArchiveDictionaries(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" )
                ],
                true,
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "jp1" )
                ] ) ) );
        context.DialogServiceMock
            .SetupSequence( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
            .ReturnsAsync( ConfirmationDialogResult.Secondary );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.HandleWindowLoadedAsync( TestContext.Current.CancellationToken );

        context.TranslationDictionaryServiceMock.Verify( service => service.LoadArchiveDictionaries( It.IsAny<string>() ), Times.Once );
        context.TranslationDictionaryServiceMock.Verify( service => service.LoadDictionary( It.IsAny<string>() ), Times.Never );
        context.TranslationDictionaryServiceMock.Verify( service => service.HasArchiveEntry( It.IsAny<string>(), It.IsAny<string>() ), Times.Never );
        context.TranslationDictionaryServiceMock.Verify( service => service.LoadDictionary( It.IsAny<string>(), "l10n/JP/dictionary" ), Times.Never );
    }

    [Fact]
    public async Task ActivateAsyncはJPdictionary起動時選択でCloseを選んだときDEFAULTdictionary読込後に閉じる() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadArchiveDictionaries( It.IsAny<string>() ) )
            .Returns( Result.Ok( new TranslationArchiveDictionaries(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" )
                ],
                true,
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "jp1" )
                ] ) ) );
        context.DialogServiceMock
            .Setup( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
            .ReturnsAsync( ConfirmationDialogResult.Cancel );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.HandleWindowLoadedAsync( TestContext.Current.CancellationToken );

        Assert.True( viewModel.ShouldCloseAfterStartup );
        context.TranslationDictionaryServiceMock.Verify( service => service.LoadArchiveDictionaries( It.IsAny<string>() ), Times.Once );
        context.DialogServiceMock.Verify( service => service.ConfirmationDialogShowAsync(
            It.Is<ConfirmationDialogParameters>( parameters =>
                parameters.Title == Strings_Translation.CreateTranslationEmbeddedJapaneseDictionaryConfirmationTitle
                && parameters.Message == string.Format( Strings_Translation.CreateTranslationEmbeddedJapaneseDictionaryMizConfirmationMessage, @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" )
                    + Environment.NewLine
                    + Environment.NewLine
                    + Strings_Translation.CreateTranslationEmbeddedJapaneseDictionaryStartupPromptMessage
                && parameters.ConfirmButtonText == Strings_Translation.CreateTranslationEmbeddedJapaneseDictionaryImportButtonText
                && parameters.SecondaryButtonText == Strings_Translation.CreateTranslationEmbeddedJapaneseDictionaryContinueWithoutImportButtonText
                && parameters.CancelButtonText == Strings_Translation.CreateTranslationEmbeddedJapaneseDictionaryCloseButtonText ) ), Times.Once );
    }

    [Fact]
    public async Task ActivateAsyncはJPdictionaryを読み込まず続けるときDEFAULTdictionaryを読み込む() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadArchiveDictionaries( It.IsAny<string>() ) )
            .Returns( Result.Ok( new TranslationArchiveDictionaries(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" )
                ],
                true,
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "jp1" )
                ] ) ) );
        context.DialogServiceMock
            .SetupSequence( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
            .ReturnsAsync( ConfirmationDialogResult.Secondary );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.HandleWindowLoadedAsync( TestContext.Current.CancellationToken );

        Assert.Single( viewModel.DictionaryItems );
        Assert.Equal( string.Empty, viewModel.DictionaryItems.Single().Translated );
        context.TranslationDictionaryServiceMock.Verify( service => service.LoadDictionary( It.IsAny<string>(), It.IsAny<string>() ), Times.Never );
    }

    [Fact]
    public async Task ActivateAsyncはJPdictionaryが全件一致するときTranslatedへ取り込む() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadArchiveDictionaries( It.IsAny<string>() ) )
            .Returns( Result.Ok( new TranslationArchiveDictionaries(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" ),
                    new TranslationDictionaryItem( "DictKey_sortie_2", "o2" )
                ],
                true,
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "jp1" ),
                    new TranslationDictionaryItem( "DictKey_sortie_2", "jp2" )
                ] ) ) );
        context.DialogServiceMock
            .SetupSequence( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
            .ReturnsAsync( ConfirmationDialogResult.Confirm );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.HandleWindowLoadedAsync( TestContext.Current.CancellationToken );

        Assert.Equal( "jp1", viewModel.DictionaryItems[0].Translated );
        Assert.Equal( "jp2", viewModel.DictionaryItems[1].Translated );
    }

    [Fact]
    public async Task ActivateAsyncはJPdictionaryが完全一致しないとき一致keyだけを取り込む() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadArchiveDictionaries( It.IsAny<string>() ) )
            .Returns( Result.Ok( new TranslationArchiveDictionaries(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" ),
                    new TranslationDictionaryItem( "DictKey_sortie_2", "o2" )
                ],
                true,
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "jp1" ),
                    new TranslationDictionaryItem( "DictKey_other", "skip" )
                ] ) ) );
        context.DialogServiceMock
            .SetupSequence( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
            .ReturnsAsync( ConfirmationDialogResult.Confirm )
            .ReturnsAsync( ConfirmationDialogResult.Confirm )
            .ReturnsAsync( ConfirmationDialogResult.Confirm );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.HandleWindowLoadedAsync( TestContext.Current.CancellationToken );

        Assert.Equal( "jp1", viewModel.DictionaryItems[0].Translated );
        Assert.Equal( string.Empty, viewModel.DictionaryItems[1].Translated );
        context.DialogServiceMock.Verify( service => service.ConfirmationDialogShowAsync(
            It.Is<ConfirmationDialogParameters>( parameters =>
                parameters.Title == Strings_Translation.CreateTranslationDictionaryImportPartialConfirmationTitle
                && parameters.Message == string.Format( Strings_Translation.CreateTranslationDictionaryImportPartialConfirmationMessage, 1 ) ) ), Times.Once );
    }

    [Fact]
    public async Task ActivateAsyncはJPdictionaryの部分取り込み確認でキャンセルされたときDEFAULTdictionaryで開く() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadArchiveDictionaries( It.IsAny<string>() ) )
            .Returns( Result.Ok( new TranslationArchiveDictionaries(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" )
                ],
                true,
                [
                    new TranslationDictionaryItem( "DictKey_other", "skip" )
                ] ) ) );
        context.DialogServiceMock
            .SetupSequence( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
            .ReturnsAsync( ConfirmationDialogResult.Confirm )
            .ReturnsAsync( ConfirmationDialogResult.Cancel );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.HandleWindowLoadedAsync( TestContext.Current.CancellationToken );

        Assert.Equal( string.Empty, viewModel.DictionaryItems.Single().Translated );
    }

    [Fact]
    public async Task ActivateAsyncはTrkでJPdictionary警告文言を切り替える() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadArchiveDictionaries( It.IsAny<string>() ) )
            .Returns( Result.Ok( new TranslationArchiveDictionaries(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" )
                ],
                true,
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "jp1" )
                ] ) ) );
        context.DialogServiceMock
            .Setup( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
            .ReturnsAsync( ConfirmationDialogResult.Cancel );
        var viewModel = context.CreateViewModel( @"C:\Tracks\Mission1.trk" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.HandleWindowLoadedAsync( TestContext.Current.CancellationToken );

        context.DialogServiceMock.Verify( service => service.ConfirmationDialogShowAsync(
            It.Is<ConfirmationDialogParameters>( parameters =>
                parameters.Message == string.Format( Strings_Translation.CreateTranslationEmbeddedJapaneseDictionaryTrkConfirmationMessage, @"C:\Tracks\Mission1.trk" )
                    + Environment.NewLine
                    + Environment.NewLine
                    + Strings_Translation.CreateTranslationEmbeddedJapaneseDictionaryStartupPromptMessage ) ), Times.Once );
    }

    [Fact]
    public async Task TranslateFileDir未設定時はCanExportがfalseになる() {
        var context = new TranslationCreationViewModelTestContext( new AppSettings
        {
            TranslateFileDir = string.Empty,
            DcsWorldInstallDir = @"C:\DCSWorld"
        } );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );

        Assert.False( viewModel.CanExport );
        Assert.True( viewModel.CanImportCsv );
        Assert.True( viewModel.CanImportPo );
    }

    [Fact]
    public void 初期状態のSplitButton表示はdictionary読み込みと書き出しになる() {
        var context = new TranslationCreationViewModelTestContext();
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        Assert.Equal( Strings_Translation.CreateTranslationImportDictionaryButtonContent, viewModel.ImportSplitButtonContent );
        Assert.Equal( Strings_Translation.CreateTranslationExportButtonContent, viewModel.ExportSplitButtonContent );
    }

    [Fact]
    public async Task ExecuteImportAsyncは初期状態でdictionary読み込みを実行する() {
        var context = new TranslationCreationViewModelTestContext();
        var importPath = @"C:\Translate\DCSWorld\Mods\aircraft\A10C\Mission1.miz\l10n\JP\dictionary";
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        context.DialogProviderMock
            .Setup( provider => provider.ShowOpenFilePicker( importPath, "dictionary|dictionary|すべてのファイル|*.*", out importPath ) )
            .Returns( true );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionaryFile( importPath ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" ) { Translated = "t1" }
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.ExecuteImportAsync();

        context.TranslationDictionaryServiceMock.Verify( service => service.LoadDictionaryFile( importPath ), Times.Once );
        context.TranslationDictionaryServiceMock.Verify( service => service.LoadPo( It.IsAny<string>() ), Times.Never );
        Assert.Equal( "t1", viewModel.DictionaryItems.Single().Translated );
    }

    [Fact]
    public async Task TranslateFileDir未設定時のExecuteImportAsyncはアーカイブ隣接dictionaryを初期パスにする() {
        var context = new TranslationCreationViewModelTestContext( new AppSettings
        {
            TranslateFileDir = string.Empty,
            DcsWorldInstallDir = @"C:\DCSWorld"
        } );
        var importPath = @"C:\DCSWorld\Mods\aircraft\A10C\dictionary";
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        context.DialogProviderMock
            .Setup( provider => provider.ShowOpenFilePicker( importPath, "dictionary|dictionary|すべてのファイル|*.*", out importPath ) )
            .Returns( true );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionaryFile( importPath ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" ) { Translated = "t1" }
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.ExecuteImportAsync();

        context.DialogProviderMock.Verify(
            provider => provider.ShowOpenFilePicker( importPath, "dictionary|dictionary|すべてのファイル|*.*", out importPath ),
            Times.Once );
        context.TranslationDictionaryServiceMock.Verify( service => service.LoadDictionaryFile( importPath ), Times.Once );
        Assert.Equal( "t1", viewModel.DictionaryItems.Single().Translated );
    }

    [Fact]
    public async Task SelectImportCsvAsyncは表示を切り替えてCSV読み込みを実行する() {
        var context = new TranslationCreationViewModelTestContext();
        var importPath = @"C:\Translate\DCSWorld\Mods\aircraft\A10C\Mission1.miz\l10n\JP\Mission1.csv";
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        context.DialogProviderMock
            .Setup( provider => provider.ShowOpenFilePicker( importPath, "CSV files|*.csv|すべてのファイル|*.*", out importPath ) )
            .Returns( true );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadCsv( importPath ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationCsvEntry>>(
                [
                    new TranslationCsvEntry( "DictKey_descriptionFoo_1", "o1", "csv1" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.SelectImportCsvAsync();

        Assert.Equal( Strings_Translation.CreateTranslationImportCsvButtonContent, viewModel.ImportSplitButtonContent );
        context.TranslationDictionaryServiceMock.Verify( service => service.LoadCsv( importPath ), Times.Once );
        Assert.Equal( "csv1", viewModel.DictionaryItems.Single().Translated );
    }

    [Fact]
    public async Task TranslateFileDir未設定時のSelectImportCsvAsyncはアーカイブ隣接csvを初期パスにする() {
        var context = new TranslationCreationViewModelTestContext( new AppSettings
        {
            TranslateFileDir = string.Empty,
            DcsWorldInstallDir = @"C:\DCSWorld"
        } );
        var importPath = @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.csv";
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        context.DialogProviderMock
            .Setup( provider => provider.ShowOpenFilePicker( importPath, "CSV files|*.csv|すべてのファイル|*.*", out importPath ) )
            .Returns( true );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadCsv( importPath ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationCsvEntry>>(
                [
                    new TranslationCsvEntry( "DictKey_descriptionFoo_1", "o1", "csv1" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.SelectImportCsvAsync();

        context.DialogProviderMock.Verify(
            provider => provider.ShowOpenFilePicker( importPath, "CSV files|*.csv|すべてのファイル|*.*", out importPath ),
            Times.Once );
        context.TranslationDictionaryServiceMock.Verify( service => service.LoadCsv( importPath ), Times.Once );
        Assert.Equal( "csv1", viewModel.DictionaryItems.Single().Translated );
    }

    [Fact]
    public async Task ExecuteImportAsyncは切替後にCSV読み込みを再実行する() {
        var context = new TranslationCreationViewModelTestContext();
        var importPath = @"C:\Translate\DCSWorld\Mods\aircraft\A10C\Mission1.miz\l10n\JP\Mission1.csv";
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        context.DialogProviderMock
            .Setup( provider => provider.ShowOpenFilePicker( importPath, "CSV files|*.csv|すべてのファイル|*.*", out importPath ) )
            .Returns( true );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadCsv( importPath ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationCsvEntry>>(
                [
                    new TranslationCsvEntry( "DictKey_descriptionFoo_1", "o1", "csv1" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.SelectImportCsvAsync();
        await viewModel.ExecuteImportAsync();

        context.TranslationDictionaryServiceMock.Verify( service => service.LoadCsv( importPath ), Times.Exactly( 2 ) );
    }

    [Fact]
    public async Task SelectImportDictionaryAsyncは表示を切り替えてdictionary読み込みを実行する() {
        var context = new TranslationCreationViewModelTestContext();
        var importPath = @"C:\Translate\DCSWorld\Mods\aircraft\A10C\Mission1.miz\l10n\JP\dictionary";
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        context.DialogProviderMock
            .Setup( provider => provider.ShowOpenFilePicker( importPath, "dictionary|dictionary|すべてのファイル|*.*", out importPath ) )
            .Returns( true );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionaryFile( importPath ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" ) { Translated = "dictionary1" }
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.SelectImportDictionaryAsync();

        Assert.Equal( Strings_Translation.CreateTranslationImportDictionaryButtonContent, viewModel.ImportSplitButtonContent );
        context.TranslationDictionaryServiceMock.Verify( service => service.LoadDictionaryFile( importPath ), Times.Once );
        Assert.Equal( "dictionary1", viewModel.DictionaryItems.Single().Translated );
    }

    [Fact]
    public async Task ExecuteImportAsyncは切替後にdictionary読み込みを再実行する() {
        var context = new TranslationCreationViewModelTestContext();
        var importPath = @"C:\Translate\DCSWorld\Mods\aircraft\A10C\Mission1.miz\l10n\JP\dictionary";
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        context.DialogProviderMock
            .Setup( provider => provider.ShowOpenFilePicker( importPath, "dictionary|dictionary|すべてのファイル|*.*", out importPath ) )
            .Returns( true );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionaryFile( importPath ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" ) { Translated = "dictionary1" }
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.SelectImportDictionaryAsync();
        await viewModel.ExecuteImportAsync();

        context.TranslationDictionaryServiceMock.Verify( service => service.LoadDictionaryFile( importPath ), Times.Exactly( 2 ) );
    }

    [Fact]
    public async Task ExecuteExportAsyncは初期状態でdictionary書き出しを実行する() {
        using var context = new TranslationCreationViewModelTestContext();
        var editableDictionary = CreateEditableDictionary(
            """
dictionary = {
    ["DictKey_descriptionFoo_1"] = "o1"
}
""" );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadEditableDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok( editableDictionary ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.DictionaryItems.Single().Translated = "translated";

        await viewModel.ExecuteExportAsync();

        context.TranslationDictionaryServiceMock.Verify( service => service.SaveDictionaryAsync(
            It.Is<string>( path => path.EndsWith( "dictionary", StringComparison.Ordinal ) ),
            It.IsAny<EditableTranslationDictionary>(),
            It.IsAny<IReadOnlyDictionary<string, string>>(),
            It.IsAny<CancellationToken>() ), Times.Once );
    }

    [Fact]
    public async Task SelectExportPoAsyncは表示と主動作をPO書き出しへ切り替える() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.SelectExportPoAsync();

        Assert.Equal( Strings_Translation.CreateTranslationExportPoSplitButtonContent, viewModel.ExportSplitButtonContent );
        context.TranslationDictionaryServiceMock.Verify( service => service.SavePoAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyList<TranslationDictionaryItem>>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>() ), Times.Once );
    }

    [Fact]
    public async Task SelectExportCsvAsyncは表示と主動作をCSV書き出しへ切り替える() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.SelectExportCsvAsync();

        Assert.Equal( Strings_Translation.CreateTranslationExportCsvButtonContent, viewModel.ExportSplitButtonContent );
        context.TranslationDictionaryServiceMock.Verify( service => service.SaveCsvAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyList<TranslationDictionaryItem>>(),
            It.IsAny<CancellationToken>() ), Times.Once );
    }

    [StaFact]
    public async Task ExportAsyncはDcsWorld配下へ現在のTranslatedを書き出す() {
        var context = new TranslationCreationViewModelTestContext();
        var editableDictionary = CreateEditableDictionary(
            """
dictionary = {
    ["DictKey_descriptionFoo_2"] = "o2",
    ["DictKey_sortie_1"] = "o1"
}
""" );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_2", "o2" ),
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" )
                ] ) );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadEditableDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok( editableDictionary ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.DictionaryItems.Single( item => item.Key == "DictKey_sortie_1" ).Translated = "translated-1";
        viewModel.DictionaryItems.Single( item => item.Key == "DictKey_descriptionFoo_2" ).Translated = "disabled";
        viewModel.DictionaryItems.Single( item => item.Key == "DictKey_descriptionFoo_2" ).IsEnabled = false;

        await viewModel.ExportAsync();

        context.TranslationDictionaryServiceMock.Verify( service => service.SaveDictionaryAsync(
            Path.Combine( new[] { @"C:\Translate", "DCSWorld", "Mods", "aircraft", "A10C", "Mission1.miz", "l10n", "JP", "dictionary" } ),
            It.Is<EditableTranslationDictionary>( dictionary => ReferenceEquals( dictionary, editableDictionary ) ),
            It.Is<IReadOnlyDictionary<string, string>>( items =>
                items.Count == 1
                && items["DictKey_sortie_1"] == "translated-1" ),
            It.IsAny<CancellationToken>() ), Times.Once );
        Assert.Contains( @"C:\Translate", viewModel.StatusMessage, StringComparison.Ordinal );
        var notification = Assert.Single( viewModel.MessageQueue.QueuedMessages );
        Assert.Equal( "書き出しが完了しました", notification.Content );
        Assert.Equal( "開く", notification.ActionContent );
        var actionHandler = Assert.IsType<Action<object>>( notification.ActionHandler );
        actionHandler( Assert.IsType<string>( notification.ActionArgument ) );
        context.SystemServiceMock.Verify(
            service => service.OpenDirectory( Path.Combine( new[] { @"C:\Translate", "DCSWorld", "Mods", "aircraft", "A10C", "Mission1.miz", "l10n", "JP" } ) ),
            Times.Once );
    }

    [Fact]
    public async Task ExportAsyncは既存ファイルがあるとき上書き確認後に保存する() {
        using var context = new TranslationCreationViewModelTestContext();
        var editableDictionary = CreateEditableDictionary(
            """
dictionary = {
    ["DictKey_descriptionFoo_1"] = "o1"
}
""" );
        var archivePath = Path.Combine( context.TempDirectory, "Mods", "aircraft", "A10C", "Mission1.miz" );
        var exportPath = Path.Combine( context.TempDirectory, "DCSWorld", "Mods", "aircraft", "A10C", "Mission1.miz", "l10n", "JP", "dictionary" );
        Directory.CreateDirectory( Path.GetDirectoryName( archivePath )! );
        Directory.CreateDirectory( Path.GetDirectoryName( exportPath )! );
        await File.WriteAllTextAsync( exportPath, "existing", TestContext.Current.CancellationToken );

        context.AppSettingsServiceMock
            .Setup( service => service.Settings )
            .Returns( new AppSettings
            {
                TranslateFileDir = context.TempDirectory,
                DcsWorldInstallDir = context.TempDirectory,
                SourceUserMissionDir = @"C:\UserMissions"
            } );
        context.DialogServiceMock
            .Setup( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
            .ReturnsAsync( ConfirmationDialogResult.Confirm );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadEditableDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok( editableDictionary ) );
        var viewModel = context.CreateViewModel( archivePath );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.DictionaryItems.Single().Translated = "translated";

        await viewModel.ExportAsync();

        context.DialogServiceMock.Verify( service => service.ConfirmationDialogShowAsync(
            It.Is<ConfirmationDialogParameters>( parameters =>
                parameters.Title == "上書き確認"
                && parameters.ConfirmButtonText == "上書き"
                && parameters.CancelButtonText == "キャンセル"
                && parameters.SecondaryButtonText == "別名保存"
                && parameters.DialogIdentifier == TranslationCreationDialogHostIdentifiers.Confirmation
                && parameters.Message.Contains( "保存先にファイルが既に存在します。上書きしますか？", StringComparison.Ordinal )
                && parameters.Message.Contains( exportPath, StringComparison.Ordinal ) ) ), Times.Once );
        context.TranslationDictionaryServiceMock.Verify( service => service.SaveDictionaryAsync(
            exportPath,
            It.IsAny<EditableTranslationDictionary>(),
            It.Is<IReadOnlyDictionary<string, string>>( items => items["DictKey_descriptionFoo_1"] == "translated" ),
            It.IsAny<CancellationToken>() ), Times.Once );
    }

    [StaFact]
    public async Task ExportAsyncは上書き確認でキャンセルされたとき保存しない() {
        using var context = new TranslationCreationViewModelTestContext();
        var editableDictionary = CreateEditableDictionary(
            """
dictionary = {
    ["DictKey_descriptionFoo_1"] = "o1"
}
""" );
        var archivePath = Path.Combine( context.TempDirectory, "Mods", "aircraft", "A10C", "Mission1.miz" );
        var exportPath = Path.Combine( context.TempDirectory, "DCSWorld", "Mods", "aircraft", "A10C", "Mission1.miz", "l10n", "JP", "dictionary" );
        Directory.CreateDirectory( Path.GetDirectoryName( archivePath )! );
        Directory.CreateDirectory( Path.GetDirectoryName( exportPath )! );
        await File.WriteAllTextAsync( exportPath, "existing", TestContext.Current.CancellationToken );

        context.AppSettingsServiceMock
            .Setup( service => service.Settings )
            .Returns( new AppSettings
            {
                TranslateFileDir = context.TempDirectory,
                DcsWorldInstallDir = context.TempDirectory,
                SourceUserMissionDir = @"C:\UserMissions"
            } );
        context.DialogServiceMock
            .Setup( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
            .ReturnsAsync( ConfirmationDialogResult.Cancel );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadEditableDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok( editableDictionary ) );
        var viewModel = context.CreateViewModel( archivePath );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        var previousStatusMessage = viewModel.StatusMessage;

        await viewModel.ExportAsync();

        context.DialogServiceMock.Verify( service => service.ConfirmationDialogShowAsync(
            It.Is<ConfirmationDialogParameters>( parameters => parameters.DialogIdentifier == TranslationCreationDialogHostIdentifiers.Confirmation ) ), Times.Once );
        context.TranslationDictionaryServiceMock.Verify( service => service.SaveDictionaryAsync(
            It.IsAny<string>(),
            It.IsAny<EditableTranslationDictionary>(),
            It.IsAny<IReadOnlyDictionary<string, string>>(),
            It.IsAny<CancellationToken>() ), Times.Never );
        Assert.Equal( previousStatusMessage, viewModel.StatusMessage );
        Assert.Empty( viewModel.MessageQueue.QueuedMessages );
    }

    [StaFact]
    public async Task ExportAsyncは上書き確認で別名保存が選択されたとき選択先へ保存する() {
        using var context = new TranslationCreationViewModelTestContext();
        var editableDictionary = CreateEditableDictionary(
            """
dictionary = {
    ["DictKey_descriptionFoo_1"] = "o1"
}
""" );
        var archivePath = Path.Combine( context.TempDirectory, "Mods", "aircraft", "A10C", "Mission1.miz" );
        var exportPath = Path.Combine( context.TempDirectory, "DCSWorld", "Mods", "aircraft", "A10C", "Mission1.miz", "l10n", "JP", "dictionary" );
        var saveAsPath = Path.Combine( context.TempDirectory, "Custom", "dictionary-copy" );
        Directory.CreateDirectory( Path.GetDirectoryName( archivePath )! );
        Directory.CreateDirectory( Path.GetDirectoryName( exportPath )! );
        await File.WriteAllTextAsync( exportPath, "existing", TestContext.Current.CancellationToken );

        context.AppSettingsServiceMock
            .Setup( service => service.Settings )
            .Returns( new AppSettings
            {
                TranslateFileDir = context.TempDirectory,
                DcsWorldInstallDir = context.TempDirectory,
                SourceUserMissionDir = @"C:\UserMissions"
            } );
        context.DialogServiceMock
            .Setup( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
            .ReturnsAsync( ConfirmationDialogResult.Secondary );
        context.DialogProviderMock
            .Setup( provider => provider.ShowSaveFilePicker( exportPath, "dictionary|dictionary|すべてのファイル|*.*", out saveAsPath ) )
            .Returns( true );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadEditableDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok( editableDictionary ) );
        var viewModel = context.CreateViewModel( archivePath );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.DictionaryItems.Single().Translated = "translated";

        await viewModel.ExportAsync();

        context.DialogProviderMock.Verify( provider => provider.ShowSaveFilePicker( exportPath, "dictionary|dictionary|すべてのファイル|*.*", out saveAsPath ), Times.Once );
        context.TranslationDictionaryServiceMock.Verify( service => service.SaveDictionaryAsync(
            saveAsPath,
            It.IsAny<EditableTranslationDictionary>(),
            It.Is<IReadOnlyDictionary<string, string>>( items => items["DictKey_descriptionFoo_1"] == "translated" ),
            It.IsAny<CancellationToken>() ), Times.Once );
        var notification = Assert.Single( viewModel.MessageQueue.QueuedMessages );
        var actionHandler = Assert.IsType<Action<object>>( notification.ActionHandler );
        actionHandler( Assert.IsType<string>( notification.ActionArgument ) );
        context.SystemServiceMock.Verify( service => service.OpenDirectory( Path.GetDirectoryName( saveAsPath )! ), Times.Once );
    }

    [StaFact]
    public async Task ExportAsyncは別名保存ダイアログでキャンセルされたとき保存しない() {
        using var context = new TranslationCreationViewModelTestContext();
        var editableDictionary = CreateEditableDictionary(
            """
dictionary = {
    ["DictKey_descriptionFoo_1"] = "o1"
}
""" );
        var archivePath = Path.Combine( context.TempDirectory, "Mods", "aircraft", "A10C", "Mission1.miz" );
        var exportPath = Path.Combine( context.TempDirectory, "DCSWorld", "Mods", "aircraft", "A10C", "Mission1.miz", "l10n", "JP", "dictionary" );
        var saveAsPath = Path.Combine( context.TempDirectory, "Custom", "dictionary-copy" );
        Directory.CreateDirectory( Path.GetDirectoryName( archivePath )! );
        Directory.CreateDirectory( Path.GetDirectoryName( exportPath )! );
        await File.WriteAllTextAsync( exportPath, "existing", TestContext.Current.CancellationToken );

        context.AppSettingsServiceMock
            .Setup( service => service.Settings )
            .Returns( new AppSettings
            {
                TranslateFileDir = context.TempDirectory,
                DcsWorldInstallDir = context.TempDirectory,
                SourceUserMissionDir = @"C:\UserMissions"
            } );
        context.DialogServiceMock
            .Setup( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
            .ReturnsAsync( ConfirmationDialogResult.Secondary );
        context.DialogProviderMock
            .Setup( provider => provider.ShowSaveFilePicker( exportPath, "dictionary|dictionary|すべてのファイル|*.*", out saveAsPath ) )
            .Returns( false );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadEditableDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok( editableDictionary ) );
        var viewModel = context.CreateViewModel( archivePath );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        var previousStatusMessage = viewModel.StatusMessage;

        await viewModel.ExportAsync();

        context.DialogProviderMock.Verify( provider => provider.ShowSaveFilePicker( exportPath, "dictionary|dictionary|すべてのファイル|*.*", out saveAsPath ), Times.Once );
        context.TranslationDictionaryServiceMock.Verify( service => service.SaveDictionaryAsync(
            It.IsAny<string>(),
            It.IsAny<EditableTranslationDictionary>(),
            It.IsAny<IReadOnlyDictionary<string, string>>(),
            It.IsAny<CancellationToken>() ), Times.Never );
        Assert.Equal( previousStatusMessage, viewModel.StatusMessage );
        Assert.Empty( viewModel.MessageQueue.QueuedMessages );
    }

    [StaFact]
    public async Task ExportAsyncは生成したdictionaryがLuaコンパイルエラーのとき保存しない() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadEditableDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok( CreateEditableDictionary(
                """
dictionary = {
    ["DictKey_descriptionFoo_1"] = "o1",
}
""" ) ) );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.SaveDictionaryAsync(
                It.IsAny<string>(),
                It.IsAny<EditableTranslationDictionary>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>() ) )
            .ThrowsAsync( new InvalidOperationException( "dictionary の Lua コンパイル検証に失敗した。" ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.DictionaryItems.Single().Translated = "translated";

        await viewModel.ExportAsync();

        context.TranslationDictionaryServiceMock.Verify( service => service.SaveDictionaryAsync(
            It.IsAny<string>(),
            It.IsAny<EditableTranslationDictionary>(),
            It.IsAny<IReadOnlyDictionary<string, string>>(),
            It.IsAny<CancellationToken>() ), Times.Once );
        Assert.Equal( Strings_Translation.CreateTranslationDictionaryExportFailedMessage, viewModel.StatusMessage );
        Assert.Empty( viewModel.MessageQueue.QueuedMessages );
    }

    [Fact]
    public async Task ExportAsyncはUserMissions配下へ保存する() {
        var context = new TranslationCreationViewModelTestContext();
        var editableDictionary = CreateEditableDictionary(
            """
dictionary = {
    ["DictKey_descriptionFoo_1"] = "o1"
}
""" );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadEditableDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok( editableDictionary ) );
        var viewModel = context.CreateViewModel( @"C:\UserMissions\MyMissions\SampleMission.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.DictionaryItems.Single().Translated = "translated";

        await viewModel.ExportAsync();

        context.TranslationDictionaryServiceMock.Verify( service => service.SaveDictionaryAsync(
            Path.Combine( new[] { @"C:\Translate", "UserMissions", "MyMissions", "SampleMission.miz", "l10n", "JP", "dictionary" } ),
            It.Is<EditableTranslationDictionary>( dictionary => ReferenceEquals( dictionary, editableDictionary ) ),
            It.Is<IReadOnlyDictionary<string, string>>( items =>
                items.Count == 1
                && items["DictKey_descriptionFoo_1"] == "translated" ),
            It.IsAny<CancellationToken>() ), Times.Once );
    }

    [StaFact]
    public async Task ExportAsyncは基準外パスのとき失敗扱いにする() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\Other\SampleMission.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.ExportAsync();

        context.TranslationDictionaryServiceMock.Verify( service => service.SaveDictionaryAsync(
            It.IsAny<string>(),
            It.IsAny<EditableTranslationDictionary>(),
            It.IsAny<IReadOnlyDictionary<string, string>>(),
            It.IsAny<CancellationToken>() ), Times.Never );
        Assert.Equal( Strings_Translation.CreateTranslationDictionaryExportFailedMessage, viewModel.StatusMessage );
        Assert.Empty( viewModel.MessageQueue.QueuedMessages );
    }

    [StaFact]
    public async Task ExportPoAsyncはDcsWorld配下へ現在のTranslatedを書き出す() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_2", "o2" ),
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.DictionaryItems.Single( item => item.Key == "DictKey_sortie_1" ).Translated = "translated-1";
        viewModel.DictionaryItems.Single( item => item.Key == "DictKey_descriptionFoo_2" ).Translated = string.Empty;

        await viewModel.ExportPoAsync();

        context.TranslationDictionaryServiceMock.Verify( service => service.SavePoAsync(
            Path.Combine( new[] { @"C:\Translate", "DCSWorld", "Mods", "aircraft", "A10C", "Mission1.miz", "l10n", "JP", "Mission1.po" } ),
            It.Is<IReadOnlyList<TranslationDictionaryItem>>( items =>
                items.Count == 2
                && items[0].Key == "DictKey_sortie_1"
                && items[0].Original == "o1"
                && items[0].Translated == "translated-1"
                && items[1].Key == "DictKey_descriptionFoo_2"
                && items[1].Translated == string.Empty ),
            "DCS Translation Japanese 1.4.0.0",
            "2026-03-13 14:25+09:00",
            "2026-03-13 14:25+09:00",
            "DCS Translation Tool 1.4.0.0",
            It.IsAny<CancellationToken>() ), Times.Once );
        Assert.Contains( "Mission1.po", viewModel.StatusMessage, StringComparison.Ordinal );
        var notification = Assert.Single( viewModel.MessageQueue.QueuedMessages );
        var actionHandler = Assert.IsType<Action<object>>( notification.ActionHandler );
        actionHandler( Assert.IsType<string>( notification.ActionArgument ) );
        context.SystemServiceMock.Verify(
            service => service.OpenDirectory( Path.Combine( new[] { @"C:\Translate", "DCSWorld", "Mods", "aircraft", "A10C", "Mission1.miz", "l10n", "JP" } ) ),
            Times.Once );
    }

    [Fact]
    public async Task ExportPoAsyncはUserMissions配下へ保存する() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\UserMissions\MyMissions\SampleMission.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.DictionaryItems.Single().Translated = "translated";

        await viewModel.ExportPoAsync();

        context.TranslationDictionaryServiceMock.Verify( service => service.SavePoAsync(
            Path.Combine( new[] { @"C:\Translate", "UserMissions", "MyMissions", "SampleMission.miz", "l10n", "JP", "SampleMission.po" } ),
            It.Is<IReadOnlyList<TranslationDictionaryItem>>( items =>
                items.Count == 1
                && items[0].Key == "DictKey_descriptionFoo_1"
                && items[0].Translated == "translated" ),
            "DCS Translation Japanese 1.4.0.0",
            "2026-03-13 14:25+09:00",
            "2026-03-13 14:25+09:00",
            "DCS Translation Tool 1.4.0.0",
            It.IsAny<CancellationToken>() ), Times.Once );
    }

    [Fact]
    public async Task ExportPoAsyncは上書き確認で別名保存が選択されたとき選択先へ保存する() {
        using var context = new TranslationCreationViewModelTestContext();
        var archivePath = Path.Combine( context.TempDirectory, "Mods", "aircraft", "A10C", "Mission1.miz" );
        var exportPath = Path.Combine( context.TempDirectory, "DCSWorld", "Mods", "aircraft", "A10C", "Mission1.miz", "l10n", "JP", "Mission1.po" );
        var saveAsPath = Path.Combine( context.TempDirectory, "Custom", "Mission1-copy.po" );
        Directory.CreateDirectory( Path.GetDirectoryName( archivePath )! );
        Directory.CreateDirectory( Path.GetDirectoryName( exportPath )! );
        await File.WriteAllTextAsync( exportPath, "existing", TestContext.Current.CancellationToken );

        context.AppSettingsServiceMock
            .Setup( service => service.Settings )
            .Returns( new AppSettings
            {
                TranslateFileDir = context.TempDirectory,
                DcsWorldInstallDir = context.TempDirectory,
                SourceUserMissionDir = @"C:\UserMissions"
            } );
        context.DialogServiceMock
            .Setup( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
            .ReturnsAsync( ConfirmationDialogResult.Secondary );
        context.DialogProviderMock
            .Setup( provider => provider.ShowSaveFilePicker( exportPath, "PO files|*.po|すべてのファイル|*.*", out saveAsPath ) )
            .Returns( true );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        var viewModel = context.CreateViewModel( archivePath );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.DictionaryItems.Single().Translated = "translated";

        await viewModel.ExportPoAsync();

        context.DialogProviderMock.Verify( provider => provider.ShowSaveFilePicker( exportPath, "PO files|*.po|すべてのファイル|*.*", out saveAsPath ), Times.Once );
        context.TranslationDictionaryServiceMock.Verify( service => service.SavePoAsync(
            saveAsPath,
            It.Is<IReadOnlyList<TranslationDictionaryItem>>( items => items.Single().Translated == "translated" ),
            "DCS Translation Japanese 1.4.0.0",
            "2026-03-13 14:25+09:00",
            "2026-03-13 14:25+09:00",
            "DCS Translation Tool 1.4.0.0",
            It.IsAny<CancellationToken>() ), Times.Once );
    }

    [StaFact]
    public async Task ExportPoAsyncは基準外パスのとき失敗扱いにする() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\Other\SampleMission.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.ExportPoAsync();

        context.TranslationDictionaryServiceMock.Verify( service => service.SavePoAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyList<TranslationDictionaryItem>>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>() ), Times.Never );
        Assert.Equal( Strings_Translation.CreateTranslationPoExportFailedMessage, viewModel.StatusMessage );
        Assert.Empty( viewModel.MessageQueue.QueuedMessages );
    }

    [StaFact]
    public async Task ExportCsvAsyncはDcsWorld配下へ現在のTranslatedを書き出す() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_2", "o2" ),
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.DictionaryItems.Single( item => item.Key == "DictKey_sortie_1" ).Translated = "translated-1";
        viewModel.DictionaryItems.Single( item => item.Key == "DictKey_descriptionFoo_2" ).Translated = string.Empty;

        await viewModel.ExportCsvAsync();

        context.TranslationDictionaryServiceMock.Verify( service => service.SaveCsvAsync(
            Path.Combine( new[] { @"C:\Translate", "DCSWorld", "Mods", "aircraft", "A10C", "Mission1.miz", "l10n", "JP", "Mission1.csv" } ),
            It.Is<IReadOnlyList<TranslationDictionaryItem>>( items =>
                items.Count == 2
                && items[0].Key == "DictKey_sortie_1"
                && items[0].Original == "o1"
                && items[0].Translated == "translated-1"
                && items[0].IsEnabled
                && items[1].Key == "DictKey_descriptionFoo_2"
                && items[1].Translated == string.Empty
                && items[1].IsEnabled ),
            It.IsAny<CancellationToken>() ), Times.Once );
        Assert.Contains( "Mission1.csv", viewModel.StatusMessage, StringComparison.Ordinal );
        Assert.Single( viewModel.MessageQueue.QueuedMessages );
    }

    [Fact]
    public async Task ImportCsvAsyncはファイル選択がキャンセルされたとき何もしない() {
        var context = new TranslationCreationViewModelTestContext();
        var importPath = @"C:\Translate\DCSWorld\Mods\aircraft\A10C\Mission1.miz\l10n\JP\Mission1.csv";
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        context.DialogProviderMock
            .Setup( provider => provider.ShowOpenFilePicker( importPath, "CSV files|*.csv|すべてのファイル|*.*", out importPath ) )
            .Returns( false );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.ImportCsvAsync();

        context.TranslationDictionaryServiceMock.Verify( service => service.LoadCsv( It.IsAny<string>() ), Times.Never );
        Assert.Equal( string.Empty, viewModel.DictionaryItems.Single().Translated );
    }

    [Fact]
    public async Task ImportCsvAsyncはTranslatedが空のとき上書き確認なしで全件取り込む() {
        var context = new TranslationCreationViewModelTestContext();
        var importPath = @"C:\Translate\DCSWorld\Mods\aircraft\A10C\Mission1.miz\l10n\JP\Mission1.csv";
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" ),
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_2", "o2" )
                ] ) );
        context.DialogProviderMock
            .Setup( provider => provider.ShowOpenFilePicker( importPath, "CSV files|*.csv|すべてのファイル|*.*", out importPath ) )
            .Returns( true );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadCsv( importPath ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationCsvEntry>>(
                [
                    new TranslationCsvEntry( "DictKey_descriptionFoo_1", "o1", "t1", false ),
                    new TranslationCsvEntry( "DictKey_descriptionFoo_2", "o2", "t2" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.ImportCsvAsync();

        context.DialogServiceMock.Verify( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ), Times.Never );
        Assert.Equal( ["t1", "t2"], [.. viewModel.DictionaryItems.Select( item => item.Translated )] );
        Assert.Equal( [false, true], [.. viewModel.DictionaryItems.Select( item => item.IsEnabled )] );
        Assert.Equal( string.Format( Strings_Translation.CreateTranslationCsvImportSucceededMessage, importPath ), viewModel.StatusMessage );
        Assert.Single( viewModel.MessageQueue.QueuedMessages );
    }

    [Fact]
    public async Task ImportCsvAsyncは完全一致しないとき一致行だけを取り込む() {
        var context = new TranslationCreationViewModelTestContext();
        var importPath = @"C:\Translate\DCSWorld\Mods\aircraft\A10C\Mission1.miz\l10n\JP\Mission1.csv";
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" ),
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_2", "o2" ) { Translated = "keep" }
                ] ) );
        context.DialogProviderMock
            .Setup( provider => provider.ShowOpenFilePicker( importPath, "CSV files|*.csv|すべてのファイル|*.*", out importPath ) )
            .Returns( true );
        context.DialogServiceMock
            .SetupSequence( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
            .ReturnsAsync( ConfirmationDialogResult.Confirm )
            .ReturnsAsync( ConfirmationDialogResult.Confirm );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadCsv( importPath ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationCsvEntry>>(
                [
                    new TranslationCsvEntry( "DictKey_descriptionFoo_1", "o1", "imported" ),
                    new TranslationCsvEntry( "DictKey_other", "other", "skip" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.ImportCsvAsync();

        context.DialogServiceMock.Verify( service => service.ConfirmationDialogShowAsync(
            It.Is<ConfirmationDialogParameters>( parameters =>
                parameters.Title == Strings_Translation.CreateTranslationCsvImportPartialConfirmationTitle
                && parameters.Message == string.Format( Strings_Translation.CreateTranslationCsvImportPartialConfirmationMessage, 1 ) ) ), Times.Once );
        Assert.Equal( "imported", viewModel.DictionaryItems[0].Translated );
        Assert.Equal( "keep", viewModel.DictionaryItems[1].Translated );
        Assert.Equal( string.Format( Strings_Translation.CreateTranslationCsvImportPartialSucceededMessage, 1, importPath ), viewModel.StatusMessage );
    }

    [Fact]
    public async Task ImportCsvAsyncは重複した組み合わせを部分取り込み対象から除外する() {
        var context = new TranslationCreationViewModelTestContext();
        var importPath = @"C:\Translate\DCSWorld\Mods\aircraft\A10C\Mission1.miz\l10n\JP\Mission1.csv";
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" ) { Translated = "keep" }
                ] ) );
        context.DialogProviderMock
            .Setup( provider => provider.ShowOpenFilePicker( importPath, "CSV files|*.csv|すべてのファイル|*.*", out importPath ) )
            .Returns( true );
        context.DialogServiceMock
            .SetupSequence( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
            .ReturnsAsync( ConfirmationDialogResult.Confirm )
            .ReturnsAsync( ConfirmationDialogResult.Confirm );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadCsv( importPath ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationCsvEntry>>(
                [
                    new TranslationCsvEntry( "DictKey_descriptionFoo_1", "o1", "dup1" ),
                    new TranslationCsvEntry( "DictKey_descriptionFoo_1", "o1", "dup2" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.ImportCsvAsync();

        Assert.Equal( "keep", viewModel.DictionaryItems.Single().Translated );
        Assert.Equal( string.Format( Strings_Translation.CreateTranslationCsvImportPartialSucceededMessage, 0, importPath ), viewModel.StatusMessage );
    }

    [Fact]
    public async Task ImportCsvAsyncはLoadCsv失敗時に失敗メッセージを設定する() {
        var context = new TranslationCreationViewModelTestContext();
        var importPath = @"C:\Translate\DCSWorld\Mods\aircraft\A10C\Mission1.miz\l10n\JP\Mission1.csv";
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        context.DialogProviderMock
            .Setup( provider => provider.ShowOpenFilePicker( importPath, "CSV files|*.csv|すべてのファイル|*.*", out importPath ) )
            .Returns( true );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadCsv( importPath ) )
            .Returns( Result.Fail<IReadOnlyList<TranslationCsvEntry>>( ResultErrorFactory.Validation( "invalid", "TEST" ) ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.ImportCsvAsync();

        Assert.Equal( Strings_Translation.CreateTranslationCsvImportFailedMessage, viewModel.StatusMessage );
    }

    [Fact]
    public async Task ImportDictionaryAsyncはファイル選択がキャンセルされたとき何もしない() {
        var context = new TranslationCreationViewModelTestContext();
        var importPath = @"C:\Translate\DCSWorld\Mods\aircraft\A10C\Mission1.miz\l10n\JP\dictionary";
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        context.DialogProviderMock
            .Setup( provider => provider.ShowOpenFilePicker( importPath, "dictionary|dictionary|すべてのファイル|*.*", out importPath ) )
            .Returns( false );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.ImportDictionaryAsync();

        context.TranslationDictionaryServiceMock.Verify( service => service.LoadDictionaryFile( It.IsAny<string>() ), Times.Never );
        Assert.Equal( string.Empty, viewModel.DictionaryItems.Single().Translated );
    }

    [Fact]
    public async Task ImportDictionaryAsyncはTranslatedが存在するとき上書き確認でキャンセルできる() {
        var context = new TranslationCreationViewModelTestContext();
        var importPath = @"C:\Translate\DCSWorld\Mods\aircraft\A10C\Mission1.miz\l10n\JP\dictionary";
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" ) { Translated = "existing" }
                ] ) );
        context.DialogProviderMock
            .Setup( provider => provider.ShowOpenFilePicker( importPath, "dictionary|dictionary|すべてのファイル|*.*", out importPath ) )
            .Returns( true );
        context.DialogServiceMock
            .Setup( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
            .ReturnsAsync( ConfirmationDialogResult.Cancel );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.ImportDictionaryAsync();

        context.DialogServiceMock.Verify( service => service.ConfirmationDialogShowAsync(
            It.Is<ConfirmationDialogParameters>( parameters =>
                parameters.Title == Strings_Translation.CreateTranslationDictionaryImportOverwriteConfirmationTitle
                && parameters.Message == Strings_Translation.CreateTranslationDictionaryImportOverwriteConfirmationMessage
                && parameters.ConfirmButtonText == "上書き"
                && parameters.CancelButtonText == "キャンセル" ) ), Times.Once );
        context.TranslationDictionaryServiceMock.Verify( service => service.LoadDictionaryFile( It.IsAny<string>() ), Times.Never );
        Assert.Equal( "existing", viewModel.DictionaryItems.Single().Translated );
    }

    [Fact]
    public async Task ImportDictionaryAsyncは完全一致しないとき一致keyだけを取り込む() {
        var context = new TranslationCreationViewModelTestContext();
        var importPath = @"C:\Translate\DCSWorld\Mods\aircraft\A10C\Mission1.miz\l10n\JP\dictionary";
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" ),
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_2", "o2" ) { Translated = "keep", IsEnabled = false }
                ] ) );
        context.DialogProviderMock
            .Setup( provider => provider.ShowOpenFilePicker( importPath, "dictionary|dictionary|すべてのファイル|*.*", out importPath ) )
            .Returns( true );
        context.DialogServiceMock
            .SetupSequence( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
            .ReturnsAsync( ConfirmationDialogResult.Confirm )
            .ReturnsAsync( ConfirmationDialogResult.Confirm );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionaryFile( importPath ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "other-original" ) { Translated = "imported" },
                    new TranslationDictionaryItem( "DictKey_other", "other" ) { Translated = "skip" }
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.ImportDictionaryAsync();

        context.DialogServiceMock.Verify( service => service.ConfirmationDialogShowAsync(
            It.Is<ConfirmationDialogParameters>( parameters =>
                parameters.Title == Strings_Translation.CreateTranslationDictionaryImportPartialConfirmationTitle
                && parameters.Message == string.Format( Strings_Translation.CreateTranslationDictionaryImportPartialConfirmationMessage, 1 )
                && parameters.ConfirmButtonText == "取り込む"
                && parameters.CancelButtonText == "キャンセル" ) ), Times.Once );
        Assert.Equal( "imported", viewModel.DictionaryItems[0].Translated );
        Assert.Equal( "keep", viewModel.DictionaryItems[1].Translated );
        Assert.False( viewModel.DictionaryItems[1].IsEnabled );
        Assert.Equal( string.Format( Strings_Translation.CreateTranslationDictionaryImportPartialSucceededMessage, 1, importPath ), viewModel.StatusMessage );
    }

    [Fact]
    public async Task ImportDictionaryAsyncは部分取り込み確認でキャンセルされたとき反映しない() {
        var context = new TranslationCreationViewModelTestContext();
        var importPath = @"C:\Translate\DCSWorld\Mods\aircraft\A10C\Mission1.miz\l10n\JP\dictionary";
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" ) { Translated = "keep" }
                ] ) );
        context.DialogProviderMock
            .Setup( provider => provider.ShowOpenFilePicker( importPath, "dictionary|dictionary|すべてのファイル|*.*", out importPath ) )
            .Returns( true );
        context.DialogServiceMock
            .SetupSequence( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
            .ReturnsAsync( ConfirmationDialogResult.Confirm )
            .ReturnsAsync( ConfirmationDialogResult.Cancel );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionaryFile( importPath ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_other", "other" ) { Translated = "skip" }
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.ImportDictionaryAsync();

        Assert.Equal( "keep", viewModel.DictionaryItems.Single().Translated );
    }

    [Fact]
    public async Task ImportDictionaryAsyncはLoadDictionaryFile失敗時に失敗メッセージを設定する() {
        var context = new TranslationCreationViewModelTestContext();
        var importPath = @"C:\Translate\DCSWorld\Mods\aircraft\A10C\Mission1.miz\l10n\JP\dictionary";
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        context.DialogProviderMock
            .Setup( provider => provider.ShowOpenFilePicker( importPath, "dictionary|dictionary|すべてのファイル|*.*", out importPath ) )
            .Returns( true );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionaryFile( importPath ) )
            .Returns( Result.Fail<IReadOnlyList<TranslationDictionaryItem>>( ResultErrorFactory.Validation( "invalid", "TEST" ) ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.ImportDictionaryAsync();

        Assert.Equal( Strings_Translation.CreateTranslationDictionaryImportFailedMessage, viewModel.StatusMessage );
    }

    [Fact]
    public async Task ImportPoAsyncはファイル選択がキャンセルされたとき何もしない() {
        var context = new TranslationCreationViewModelTestContext();
        var importPath = @"C:\Translate\DCSWorld\Mods\aircraft\A10C\Mission1.miz\l10n\JP\Mission1.po";
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        context.DialogProviderMock
            .Setup( provider => provider.ShowOpenFilePicker( importPath, "PO files|*.po|すべてのファイル|*.*", out importPath ) )
            .Returns( false );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.ImportPoAsync();

        context.TranslationDictionaryServiceMock.Verify( service => service.LoadPo( It.IsAny<string>() ), Times.Never );
        Assert.Equal( string.Empty, viewModel.DictionaryItems.Single().Translated );
    }

    [Fact]
    public async Task TranslateFileDir未設定時のImportPoAsyncはアーカイブ隣接poを初期パスにする() {
        var context = new TranslationCreationViewModelTestContext( new AppSettings
        {
            TranslateFileDir = string.Empty,
            DcsWorldInstallDir = @"C:\DCSWorld"
        } );
        var importPath = @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.po";
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        context.DialogProviderMock
            .Setup( provider => provider.ShowOpenFilePicker( importPath, "PO files|*.po|すべてのファイル|*.*", out importPath ) )
            .Returns( true );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadPo( importPath ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationPoEntry>>(
                [
                    new TranslationPoEntry( "DictKey_descriptionFoo_1", "o1", "po1" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.ImportPoAsync();

        context.DialogProviderMock.Verify(
            provider => provider.ShowOpenFilePicker( importPath, "PO files|*.po|すべてのファイル|*.*", out importPath ),
            Times.Once );
        context.TranslationDictionaryServiceMock.Verify( service => service.LoadPo( importPath ), Times.Once );
        Assert.Equal( "po1", viewModel.DictionaryItems.Single().Translated );
    }

    [Fact]
    public async Task ImportPoAsyncはTranslatedが空のとき上書き確認なしで全件取り込む() {
        var context = new TranslationCreationViewModelTestContext();
        var importPath = @"C:\Translate\DCSWorld\Mods\aircraft\A10C\Mission1.miz\l10n\JP\Mission1.po";
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" ),
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_2", "o2" )
                ] ) );
        context.DialogProviderMock
            .Setup( provider => provider.ShowOpenFilePicker( importPath, "PO files|*.po|すべてのファイル|*.*", out importPath ) )
            .Returns( true );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadPo( importPath ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationPoEntry>>(
                [
                    new TranslationPoEntry( "DictKey_descriptionFoo_1", "o1", "t1", false ),
                    new TranslationPoEntry( "DictKey_descriptionFoo_2", "o2", "t2" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.ImportPoAsync();

        context.DialogServiceMock.Verify( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ), Times.Never );
        Assert.Equal( ["t1", "t2"], [.. viewModel.DictionaryItems.Select( item => item.Translated )] );
        Assert.Equal( [false, true], [.. viewModel.DictionaryItems.Select( item => item.IsEnabled )] );
        Assert.Equal( string.Format( Strings_Translation.CreateTranslationPoImportSucceededMessage, importPath ), viewModel.StatusMessage );
        Assert.Single( viewModel.MessageQueue.QueuedMessages );
    }

    [Fact]
    public async Task ImportPoAsyncはTranslatedが存在するとき上書き確認でキャンセルできる() {
        var context = new TranslationCreationViewModelTestContext();
        var importPath = @"C:\Translate\DCSWorld\Mods\aircraft\A10C\Mission1.miz\l10n\JP\Mission1.po";
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" ) { Translated = "existing" }
                ] ) );
        context.DialogProviderMock
            .Setup( provider => provider.ShowOpenFilePicker( importPath, "PO files|*.po|すべてのファイル|*.*", out importPath ) )
            .Returns( true );
        context.DialogServiceMock
            .Setup( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
            .ReturnsAsync( ConfirmationDialogResult.Cancel );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.ImportPoAsync();

        context.DialogServiceMock.Verify( service => service.ConfirmationDialogShowAsync(
            It.Is<ConfirmationDialogParameters>( parameters =>
                parameters.Title == Strings_Translation.CreateTranslationPoImportOverwriteConfirmationTitle
                && parameters.Message == Strings_Translation.CreateTranslationPoImportOverwriteConfirmationMessage
                && parameters.ConfirmButtonText == "上書き"
                && parameters.CancelButtonText == "キャンセル" ) ), Times.Once );
        context.TranslationDictionaryServiceMock.Verify( service => service.LoadPo( It.IsAny<string>() ), Times.Never );
        Assert.Equal( "existing", viewModel.DictionaryItems.Single().Translated );
    }

    [Fact]
    public async Task ImportPoAsyncは完全一致しないとき一致エントリーだけを取り込む() {
        var context = new TranslationCreationViewModelTestContext();
        var importPath = @"C:\Translate\DCSWorld\Mods\aircraft\A10C\Mission1.miz\l10n\JP\Mission1.po";
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" ),
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_2", "o2" ) { Translated = "keep" }
                ] ) );
        context.DialogProviderMock
            .Setup( provider => provider.ShowOpenFilePicker( importPath, "PO files|*.po|すべてのファイル|*.*", out importPath ) )
            .Returns( true );
        context.DialogServiceMock
            .SetupSequence( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
            .ReturnsAsync( ConfirmationDialogResult.Confirm )
            .ReturnsAsync( ConfirmationDialogResult.Confirm );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadPo( importPath ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationPoEntry>>(
                [
                    new TranslationPoEntry( "DictKey_descriptionFoo_1", "o1", "imported" ),
                    new TranslationPoEntry( "DictKey_other", "other", "skip" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.ImportPoAsync();

        context.DialogServiceMock.Verify( service => service.ConfirmationDialogShowAsync(
            It.Is<ConfirmationDialogParameters>( parameters =>
                parameters.Title == Strings_Translation.CreateTranslationPoImportPartialConfirmationTitle
                && parameters.Message == string.Format( Strings_Translation.CreateTranslationPoImportPartialConfirmationMessage, 1 ) ) ), Times.Once );
        Assert.Equal( "imported", viewModel.DictionaryItems[0].Translated );
        Assert.Equal( "keep", viewModel.DictionaryItems[1].Translated );
        Assert.Equal( string.Format( Strings_Translation.CreateTranslationPoImportPartialSucceededMessage, 1, importPath ), viewModel.StatusMessage );
    }

    [Fact]
    public async Task ImportPoAsyncは一致0件でも確認後に完了する() {
        var context = new TranslationCreationViewModelTestContext();
        var importPath = @"C:\Translate\DCSWorld\Mods\aircraft\A10C\Mission1.miz\l10n\JP\Mission1.po";
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        context.DialogProviderMock
            .Setup( provider => provider.ShowOpenFilePicker( importPath, "PO files|*.po|すべてのファイル|*.*", out importPath ) )
            .Returns( true );
        context.DialogServiceMock
            .Setup( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
            .ReturnsAsync( ConfirmationDialogResult.Confirm );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadPo( importPath ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationPoEntry>>(
                [
                    new TranslationPoEntry( "DictKey_other", "other", "skip" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.ImportPoAsync();

        Assert.Equal( string.Empty, viewModel.DictionaryItems.Single().Translated );
        Assert.Equal( string.Format( Strings_Translation.CreateTranslationPoImportPartialSucceededMessage, 0, importPath ), viewModel.StatusMessage );
    }

    [Fact]
    public async Task ImportPoAsyncは重複した組み合わせを部分取り込み対象から除外する() {
        var context = new TranslationCreationViewModelTestContext();
        var importPath = @"C:\Translate\DCSWorld\Mods\aircraft\A10C\Mission1.miz\l10n\JP\Mission1.po";
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" ) { Translated = "keep" }
                ] ) );
        context.DialogProviderMock
            .Setup( provider => provider.ShowOpenFilePicker( importPath, "PO files|*.po|すべてのファイル|*.*", out importPath ) )
            .Returns( true );
        context.DialogServiceMock
            .SetupSequence( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
            .ReturnsAsync( ConfirmationDialogResult.Confirm )
            .ReturnsAsync( ConfirmationDialogResult.Confirm );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadPo( importPath ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationPoEntry>>(
                [
                    new TranslationPoEntry( "DictKey_descriptionFoo_1", "o1", "dup1" ),
                    new TranslationPoEntry( "DictKey_descriptionFoo_1", "o1", "dup2" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.ImportPoAsync();

        Assert.Equal( "keep", viewModel.DictionaryItems.Single().Translated );
        Assert.Equal( string.Format( Strings_Translation.CreateTranslationPoImportPartialSucceededMessage, 0, importPath ), viewModel.StatusMessage );
    }

    [Fact]
    public async Task ImportPoAsyncはLoadPo失敗時に失敗メッセージを設定する() {
        var context = new TranslationCreationViewModelTestContext();
        var importPath = @"C:\Translate\DCSWorld\Mods\aircraft\A10C\Mission1.miz\l10n\JP\Mission1.po";
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        context.DialogProviderMock
            .Setup( provider => provider.ShowOpenFilePicker( importPath, "PO files|*.po|すべてのファイル|*.*", out importPath ) )
            .Returns( true );
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadPo( importPath ) )
            .Returns( Result.Fail<IReadOnlyList<TranslationPoEntry>>( ResultErrorFactory.Validation( "invalid", "TEST" ) ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        await viewModel.ImportPoAsync();

        Assert.Equal( Strings_Translation.CreateTranslationPoImportFailedMessage, viewModel.StatusMessage );
    }

    [Fact]
    public async Task SelectedDictionaryItem設定時に詳細表示が追従する() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" ) { Translated = "t1" },
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_2", "o2" ) { Translated = "t2" }
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.SelectedDictionaryItem = viewModel.DictionaryItems[1];

        Assert.Equal( "o2", viewModel.SelectedOriginal );
        Assert.Equal( "t2", viewModel.SelectedTranslated );
    }

    [Fact]
    public async Task SelectedTranslated更新時は遅延コミットまで選択項目へ反映しない() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.SelectedDictionaryItem = viewModel.DictionaryItems.Single();
        viewModel.SelectedTranslated = "translated";

        Assert.Equal( string.Empty, viewModel.DictionaryItems.Single().Translated );
        Assert.Equal( "translated", viewModel.SelectedTranslated );

        viewModel.FlushPendingSelectedTranslatedEdit();

        Assert.Equal( "translated", viewModel.DictionaryItems.Single().Translated );
        Assert.Equal( "translated", viewModel.SelectedTranslated );
    }

    [Fact]
    public async Task 無効行選択時にSelectedTranslated更新は無視する() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" ) {
                        Translated = "keep",
                        IsEnabled = false
                    }
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.SelectedDictionaryItem = viewModel.DictionaryItems.Single();
        viewModel.SelectedTranslated = "translated";

        Assert.False( viewModel.CanEditSelectedTranslated );
        Assert.Equal( "keep", viewModel.DictionaryItems.Single().Translated );
        Assert.Equal( "keep", viewModel.SelectedTranslated );
    }

    [Fact]
    public async Task 選択項目切替時に未反映のSelectedTranslatedを直前の項目へコミットする() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" ),
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_2", "o2" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.SelectedDictionaryItem = viewModel.DictionaryItems[0];
        viewModel.SelectedTranslated = "translated";
        viewModel.SelectedDictionaryItem = viewModel.DictionaryItems[1];

        Assert.Equal( "translated", viewModel.DictionaryItems[0].Translated );
        Assert.Equal( string.Empty, viewModel.SelectedTranslated );
        Assert.Equal( viewModel.DictionaryItems[1], viewModel.SelectedDictionaryItem );
    }

    [Fact]
    public void RowViewModelはOriginalの改行をDataGrid表示用にエスケープする() {
        var rowViewModel = new TranslationDictionaryItemRowViewModel(
            new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "first line\n\nsecond line" ) );

        Assert.Equal( @"first line\\second line", rowViewModel.OriginalDisplayText );
    }

    [Fact]
    public void RowViewModelはLF改行をDataGrid表示用にエスケープする() {
        var rowViewModel = new TranslationDictionaryItemRowViewModel(
            new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "first line\n\nsecond line" ) );

        Assert.Equal( @"first line\\second line", rowViewModel.OriginalDisplayText );
    }

    [Fact]
    public void RowViewModelはTranslated更新時にDataGrid表示用文字列へ反映する() {
        var rowViewModel = new TranslationDictionaryItemRowViewModel(
            new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "original" ) )
        {
            Translated = "first line\nsecond line"
        };

        Assert.Equal( @"first line\second line", rowViewModel.TranslatedDisplayText );
    }

    [Fact]
    public void RowViewModelはTranslatedのCRLF改行をDataGrid表示用にエスケープする() {
        var rowViewModel = new TranslationDictionaryItemRowViewModel(
            new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "original" ) )
        {
            Translated = "first line\r\nsecond line"
        };

        Assert.Equal( @"first line\second line", rowViewModel.TranslatedDisplayText );
    }

    [Theory]
    [InlineData( "\nline2\n", @"\line2\" )]
    [InlineData( "\r\nline2\r\n", @"\line2\" )]
    public void RowViewModelはTranslatedの改行コード差異を区別せず1行表示する( string translated, string expected ) {
        var rowViewModel = new TranslationDictionaryItemRowViewModel(
            new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "original" ) )
        {
            Translated = translated
        };

        Assert.Equal( expected, rowViewModel.TranslatedDisplayText );
    }

    [Fact]
    public void RowViewModelはLF改行をそのままDataGrid表示へ反映する() {
        var rowViewModel = new TranslationDictionaryItemRowViewModel(
            new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "first line\nsecond line" ) );

        Assert.Equal( @"first line\second line", rowViewModel.OriginalDisplayText );
    }

    [Fact]
    public void RowViewModelは改行なし文字列をそのままDataGrid表示する() {
        var rowViewModel = new TranslationDictionaryItemRowViewModel(
            new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "single line" ) );

        Assert.Equal( "single line", rowViewModel.OriginalDisplayText );
        Assert.Equal( string.Empty, rowViewModel.TranslatedDisplayText );
    }

    [Fact]
    public async Task 選択中項目のTranslated変更時にSelectedTranslated変更通知を発火する() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );
        var notifiedProperties = new List<string>();

        viewModel.PropertyChanged += ( _, e ) => {
            if(e.PropertyName is not null) {
                notifiedProperties.Add( e.PropertyName );
            }
        };

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.SelectedDictionaryItem = viewModel.DictionaryItems.Single();

        notifiedProperties.Clear();
        viewModel.DictionaryItems.Single().Translated = "translated";

        Assert.Contains( nameof( TranslationCreationViewModel.SelectedTranslated ), notifiedProperties );
    }

    [Fact]
    public async Task SelectedDictionaryItemをnullにすると詳細表示は空文字へ戻る() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" ) { Translated = "t1" }
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.SelectedDictionaryItem = viewModel.DictionaryItems.Single();
        viewModel.SelectedDictionaryItem = null;

        Assert.Equal( string.Empty, viewModel.SelectedOriginal );
        Assert.Equal( string.Empty, viewModel.SelectedTranslated );
    }

    [Fact]
    public async Task MoveSelectionDownは表示中の次行を選択する() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" ),
                    new TranslationDictionaryItem( "DictKey_sortie_2", "o2" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.SelectedDictionaryItem = viewModel.DictionaryItems[0];

        var moved = viewModel.MoveSelectionDown();

        Assert.True( moved );
        Assert.Equal( "DictKey_sortie_2", viewModel.SelectedDictionaryItem?.Key );
    }

    [Fact]
    public async Task MoveSelectionUpは表示中の前行を選択する() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" ),
                    new TranslationDictionaryItem( "DictKey_sortie_2", "o2" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.SelectedDictionaryItem = viewModel.DictionaryItems[1];

        var moved = viewModel.MoveSelectionUp();

        Assert.True( moved );
        Assert.Equal( "DictKey_sortie_1", viewModel.SelectedDictionaryItem?.Key );
    }

    [Fact]
    public async Task MoveSelectionDownは未選択時に表示中の先頭行を選択する() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_2", "o2" ),
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );

        var moved = viewModel.MoveSelectionDown();

        Assert.True( moved );
        Assert.Equal( "DictKey_sortie_1", viewModel.SelectedDictionaryItem?.Key );
    }

    [Fact]
    public async Task MoveSelectionUpは未選択時に表示中の末尾行を選択する() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_2", "o2" ),
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );

        var moved = viewModel.MoveSelectionUp();

        Assert.True( moved );
        Assert.Equal( "DictKey_sortie_2", viewModel.SelectedDictionaryItem?.Key );
    }

    [Fact]
    public async Task MoveSelectionDownはフィルターで非表示の選択行を飛ばして先頭の表示行を選択する() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" ) { Translated = "done" },
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_2", "o2" ),
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_3", "o3" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.SelectedDictionaryItem = viewModel.DictionaryItems.Single( item => item.Key == "DictKey_descriptionFoo_1" );
        viewModel.ShowOnlyUntranslated = true;

        var moved = viewModel.MoveSelectionDown();

        Assert.True( moved );
        Assert.Equal( "DictKey_descriptionFoo_2", viewModel.SelectedDictionaryItem?.Key );
    }

    [Fact]
    public async Task MoveSelectionUpは先頭行でnoOpにする() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" ),
                    new TranslationDictionaryItem( "DictKey_sortie_2", "o2" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.SelectedDictionaryItem = viewModel.DictionaryItems[0];

        var moved = viewModel.MoveSelectionUp();

        Assert.False( moved );
        Assert.Equal( "DictKey_sortie_1", viewModel.SelectedDictionaryItem?.Key );
    }

    [Fact]
    public async Task MoveSelectionDownは表示項目が空のときnoOpにする() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_blank", string.Empty )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );

        var moved = viewModel.MoveSelectionDown();

        Assert.False( moved );
        Assert.Null( viewModel.SelectedDictionaryItem );
    }

    [Fact]
    public async Task ActivateAsyncは指定した接頭辞優先順とKey昇順で並べる() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "OtherKey_z", "v8" ),
                    new TranslationDictionaryItem( "DictKey_other_b", "v7b" ),
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_b", "v6b" ),
                    new TranslationDictionaryItem( "DictKey_descriptionNeutralsTask_b", "v5b" ),
                    new TranslationDictionaryItem( "DictKey_descriptionRedTask_b", "v4b" ),
                    new TranslationDictionaryItem( "DictKey_descriptionBlueTask_b", "v3b" ),
                    new TranslationDictionaryItem( "DictKey_descriptionText_b", "v2b" ),
                    new TranslationDictionaryItem( "DictKey_sortie_b", "v1b" ),
                    new TranslationDictionaryItem( "DictKey_sortie_a", "v1a" ),
                    new TranslationDictionaryItem( "DictKey_descriptionText_a", "v2a" ),
                    new TranslationDictionaryItem( "DictKey_descriptionBlueTask_a", "v3a" ),
                    new TranslationDictionaryItem( "DictKey_descriptionRedTask_a", "v4a" ),
                    new TranslationDictionaryItem( "DictKey_descriptionNeutralsTask_a", "v5a" ),
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_a", "v6a" ),
                    new TranslationDictionaryItem( "DictKey_other_a", "v7a" ),
                    new TranslationDictionaryItem( "AnotherKey_a", "v8a" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );

        Assert.Equal(
            [
                "DictKey_sortie_a",
                "DictKey_sortie_b",
                "DictKey_descriptionText_a",
                "DictKey_descriptionText_b",
                "DictKey_descriptionBlueTask_a",
                "DictKey_descriptionBlueTask_b",
                "DictKey_descriptionRedTask_a",
                "DictKey_descriptionRedTask_b",
                "DictKey_descriptionNeutralsTask_a",
                "DictKey_descriptionNeutralsTask_b",
                "DictKey_descriptionFoo_a",
                "DictKey_descriptionFoo_b",
                "DictKey_other_a",
                "DictKey_other_b",
                "AnotherKey_a",
                "OtherKey_z"
            ],
            [.. viewModel.DictionaryItems.Select( item => item.Key )] );
    }

    [Fact]
    public async Task ActivateAsyncは同一グループ内を自然順で並べる() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_10", "o10" ),
                    new TranslationDictionaryItem( "DictKey_sortie_2", "o2" ),
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" ),
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_10", "d10" ),
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_2", "d2" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );

        Assert.Equal(
            [
                "DictKey_sortie_1",
                "DictKey_sortie_2",
                "DictKey_sortie_10",
                "DictKey_descriptionFoo_2",
                "DictKey_descriptionFoo_10"
            ],
            [.. viewModel.DictionaryItems.Select( item => item.Key )] );
    }

    [Fact]
    public async Task ActivateAsyncはOriginalが空欄の行を既定で非表示にする() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_blank", string.Empty ),
                    new TranslationDictionaryItem( "DictKey_filled", "value" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );

        Assert.Single( GetVisibleDictionaryItems( viewModel ) );
        Assert.Equal( "DictKey_filled", GetVisibleDictionaryItems( viewModel ).Single().Key );
    }

    [Fact]
    public async Task ActivateAsyncは対象外候補の行を既定で非表示にする() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "OtherKey_visible", "o1" ),
                    new TranslationDictionaryItem( "DictKey_WptName_1", "o2" ),
                    new TranslationDictionaryItem( "DictKey_ActionComment_1", "o3" ),
                    new TranslationDictionaryItem( "DictKey_GroupName_1", "o4" ),
                    new TranslationDictionaryItem( "DictKey_UnitName_1", "o5" ),
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o6" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );

        Assert.Equal(
            ["DictKey_sortie_1"],
            [.. GetVisibleDictionaryItems( viewModel ).Select( item => item.Key )] );
        Assert.Equal(
            [true, false, false, false, false, false],
            [.. viewModel.DictionaryItems.Select( item => item.IsEnabled )] );
    }

    [Fact]
    public async Task 対象外フィルターを無効にすると対象外候補の行を再表示する() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "OtherKey_visible", "o1" ),
                    new TranslationDictionaryItem( "DictKey_WptName_1", "o2" ),
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o3" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.HidePossibleNonTranslationTargets = false;

        Assert.Equal(
            ["DictKey_sortie_1", "DictKey_WptName_1", "OtherKey_visible"],
            [.. GetVisibleDictionaryItems( viewModel ).Select( item => item.Key )] );
        Assert.Equal(
            [true, false, false],
            [.. viewModel.DictionaryItems.Select( item => item.IsEnabled )] );
    }

    [Fact]
    public async Task ActivateAsyncは対象外候補を初期状態で無効化する() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o1" ),
                    new TranslationDictionaryItem( "DictKey_WptName_1", "o2" ),
                    new TranslationDictionaryItem( "OtherKey_1", "o3" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );

        Assert.True( viewModel.DictionaryItems.Single( item => item.Key == "DictKey_sortie_1" ).IsEnabled );
        Assert.False( viewModel.DictionaryItems.Single( item => item.Key == "DictKey_WptName_1" ).IsEnabled );
        Assert.False( viewModel.DictionaryItems.Single( item => item.Key == "OtherKey_1" ).IsEnabled );
    }

    [Fact]
    public async Task ActivateAsyncはLuaコード文字列の行を対象外候補として初期非表示にする() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "Mission briefing." ),
                    new TranslationDictionaryItem( "DictKey_script_1", "trigger.action.outText('x', 10)" ),
                    new TranslationDictionaryItem( "DictKey_script_2", "Unit.getByName('A') ~= nil" ),
                    new TranslationDictionaryItem( "DictKey_script_3", "goto label\n::label::\nreturn" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );

        Assert.Equal(
            ["DictKey_sortie_1"],
            [.. GetVisibleDictionaryItems( viewModel ).Select( item => item.Key )] );
        Assert.True( viewModel.DictionaryItems.Single( item => item.Key == "DictKey_sortie_1" ).IsEnabled );
        Assert.False( viewModel.DictionaryItems.Single( item => item.Key == "DictKey_script_1" ).IsEnabled );
        Assert.False( viewModel.DictionaryItems.Single( item => item.Key == "DictKey_script_2" ).IsEnabled );
        Assert.False( viewModel.DictionaryItems.Single( item => item.Key == "DictKey_script_3" ).IsEnabled );
    }

    [Fact]
    public async Task ActivateAsyncはコメントのみのLua文字列を新ルールでは対象外候補にしない() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "Mission briefing." ),
                    new TranslationDictionaryItem( "DictKey_comment_1", "-- comment only" ),
                    new TranslationDictionaryItem( "DictKey_comment_2", "--[[comment block]]" ),
                    new TranslationDictionaryItem( "DictKey_comment_3", "--[=[comment block]=]" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );

        Assert.Equal(
            ["DictKey_sortie_1", "DictKey_comment_1", "DictKey_comment_2", "DictKey_comment_3"],
            [.. GetVisibleDictionaryItems( viewModel ).Select( item => item.Key )] );
        Assert.All( viewModel.DictionaryItems, item => Assert.True( item.IsEnabled ) );
    }

    [Fact]
    public async Task ActivateAsyncはコメントを除いた残りがLuaなら対象外候補にする() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "Mission briefing." ),
                    new TranslationDictionaryItem( "DictKey_script_1", "-- comment only\ntrigger.action.outText('x', 10)" ),
                    new TranslationDictionaryItem( "DictKey_plain_1", "Pilot, check in on channel 2." )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );

        Assert.Equal(
            ["DictKey_sortie_1", "DictKey_plain_1"],
            [.. GetVisibleDictionaryItems( viewModel ).Select( item => item.Key )] );
        Assert.False( viewModel.DictionaryItems.Single( item => item.Key == "DictKey_script_1" ).IsEnabled );
        Assert.True( viewModel.DictionaryItems.Single( item => item.Key == "DictKey_plain_1" ).IsEnabled );
    }

    [Fact]
    public async Task 初期無効化された行を選択するとTranslated編集不可になる() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_WptName_1", "o1" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.HidePossibleNonTranslationTargets = false;
        viewModel.SelectedDictionaryItem = viewModel.DictionaryItems.Single();

        Assert.False( viewModel.CanEditSelectedTranslated );
    }

    [Fact]
    public async Task ShowEnabledItemsを無効にすると無効行のみ表示する() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_enabled", "o1" ),
                    new TranslationDictionaryItem( "DictKey_disabled", "o2" ) { IsEnabled = false }
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.HidePossibleNonTranslationTargets = false;
        viewModel.ShowEnabledItems = false;

        Assert.Equal(
            ["DictKey_disabled"],
            [.. GetVisibleDictionaryItems( viewModel ).Select( item => item.Key )] );
    }

    [Fact]
    public async Task ShowDisabledItemsを無効にすると有効行のみ表示する() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_enabled", "o1" ),
                    new TranslationDictionaryItem( "DictKey_disabled", "o2" ) { IsEnabled = false }
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.HidePossibleNonTranslationTargets = false;
        viewModel.ShowDisabledItems = false;

        Assert.Equal(
            ["DictKey_enabled"],
            [.. GetVisibleDictionaryItems( viewModel ).Select( item => item.Key )] );
    }

    [Fact]
    public async Task ShowEnabledItemsとShowDisabledItemsを両方無効にすると0件表示する() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_enabled", "o1" ),
                    new TranslationDictionaryItem( "DictKey_disabled", "o2" ) { IsEnabled = false }
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.HidePossibleNonTranslationTargets = false;
        viewModel.ShowEnabledItems = false;
        viewModel.ShowDisabledItems = false;

        Assert.Empty( GetVisibleDictionaryItems( viewModel ) );
    }

    [Fact]
    public async Task ShowOnlyUntranslatedを有効にするとTranslated入力済み行を非表示にする() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" ) { Translated = "done" },
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_2", "o2" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.ShowOnlyUntranslated = true;

        var visibleItems = GetVisibleDictionaryItems( viewModel );
        Assert.Single( visibleItems );
        Assert.Equal( "DictKey_descriptionFoo_2", visibleItems[0].Key );
    }

    [Fact]
    public async Task 二つのフィルターは同時適用される() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_blank", string.Empty ),
                    new TranslationDictionaryItem( "DictKey_done", "o1" ) { Translated = "translated" },
                    new TranslationDictionaryItem( "DictKey_todo", "o2" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.ShowOnlyUntranslated = true;

        var visibleItems = GetVisibleDictionaryItems( viewModel );
        Assert.Single( visibleItems );
        Assert.Equal( "DictKey_todo", visibleItems[0].Key );
    }

    [Fact]
    public async Task 三つのフィルターは同時適用される() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "OtherKey_hidden", "o1" ),
                    new TranslationDictionaryItem( "DictKey_WptName_1", "o2" ),
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o3" ) { Translated = "done" },
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", string.Empty ),
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_2", "o4" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.ShowOnlyUntranslated = true;

        Assert.Equal(
            ["DictKey_descriptionFoo_2"],
            [.. GetVisibleDictionaryItems( viewModel ).Select( item => item.Key )] );
    }

    [Fact]
    public async Task フィルター適用後も表示順を維持する() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_b", "o2" ),
                    new TranslationDictionaryItem( "DictKey_sortie_b", "o1" ) { Translated = "done" },
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_a", "o3" ),
                    new TranslationDictionaryItem( "OtherKey_a", "o4" ),
                    new TranslationDictionaryItem( "DictKey_sortie_a", "o5" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.HidePossibleNonTranslationTargets = false;
        viewModel.ShowOnlyUntranslated = true;

        Assert.Equal(
            [
                "DictKey_sortie_a",
                "DictKey_descriptionFoo_a",
                "DictKey_descriptionFoo_b",
                "OtherKey_a"
            ],
            [.. GetVisibleDictionaryItems( viewModel ).Select( item => item.Key )] );
    }

    [Fact]
    public async Task Translated更新時に未翻訳フィルターを再適用する() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "k1", "o1" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.ShowOnlyUntranslated = true;

        var item = viewModel.DictionaryItems.Single();
        item.Translated = "translated";

        Assert.Empty( GetVisibleDictionaryItems( viewModel ) );
    }

    [Fact]
    public async Task SelectedTranslated編集中は未翻訳フィルターを即時再適用しない() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.ShowOnlyUntranslated = true;
        viewModel.SelectedDictionaryItem = viewModel.DictionaryItems.Single();
        viewModel.SelectedTranslated = "translated";

        Assert.Single( GetVisibleDictionaryItems( viewModel ) );
        Assert.Equal( string.Empty, viewModel.DictionaryItems.Single().Translated );

        viewModel.FlushPendingSelectedTranslatedEdit();

        Assert.Empty( GetVisibleDictionaryItems( viewModel ) );
    }

    [Fact]
    public async Task IsEnabled更新時に有効状態フィルターを再適用する() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_toggle", "o1" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.ShowDisabledItems = false;

        var item = viewModel.DictionaryItems.Single();
        item.IsEnabled = false;

        Assert.Empty( GetVisibleDictionaryItems( viewModel ) );
    }

    [Fact]
    public async Task ActivateAsyncは読込失敗時に状態メッセージを設定する() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Fail<IReadOnlyList<TranslationDictionaryItem>>( ResultErrorFactory.NotFound( "not found", "TEST" ) ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );

        Assert.False( viewModel.HasDictionaryItems );
        Assert.Equal( Strings_Translation.CreateTranslationDictionaryLoadFailedMessage, viewModel.StatusMessage );
    }

    [Fact]
    public async Task ActivateAsyncはdictionary読込サービス例外時も状態メッセージを設定する() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Throws( new InvalidOperationException( "boom" ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );

        Assert.False( viewModel.HasDictionaryItems );
        Assert.Equal( Strings_Translation.CreateTranslationDictionaryLoadFailedMessage, viewModel.StatusMessage );
    }

    [Fact]
    public async Task ActivateAsyncは空結果時に空状態メッセージを設定する() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>( [] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ActivateAndInitializeAfterShownAsync( viewModel );

        Assert.False( viewModel.HasDictionaryItems );
        Assert.Equal( Strings_Translation.CreateTranslationDictionaryEmptyMessage, viewModel.StatusMessage );
    }

    private static async Task ActivateAndInitializeAfterShownAsync( TranslationCreationViewModel viewModel ) {
        await ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );
        await viewModel.InitializeAfterShownAsync( TestContext.Current.CancellationToken );
    }

    private static TranslationDictionaryItemRowViewModel[] GetVisibleDictionaryItems( TranslationCreationViewModel viewModel ) =>
        [.. viewModel.DictionaryItems.Where( viewModel.ShouldIncludeRow )];

    private sealed class TranslationCreationViewModelTestContext : IDisposable {

        internal TranslationCreationViewModelTestContext( AppSettings? settings = null ) {
            TempDirectory = Path.Combine( Path.GetTempPath(), $"TranslationCreationViewModelTests_{Guid.NewGuid():N}" );
            Directory.CreateDirectory( TempDirectory );
            this.Settings = settings ?? new AppSettings
            {
                TranslateFileDir = @"C:\Translate",
                DcsWorldInstallDir = @"C:\DCSWorld",
                SourceUserMissionDir = @"C:\UserMissions"
            };

            AppSettingsServiceMock
                .Setup( service => service.Settings )
                .Returns( this.Settings );
            ApplicationInfoServiceMock
                .Setup( service => service.GetVersion() )
                .Returns( new Version( 1, 4, 0, 0 ) );
            SystemServiceMock
                .Setup( service => service.GetCurrentDateTimeOffset() )
                .Returns( PoRevisionDate );
            TranslationDictionaryServiceMock
                .Setup( service => service.HasArchiveEntry( It.IsAny<string>(), It.IsAny<string>() ) )
                .Returns( Result.Ok( false ) );
            TranslationDictionaryServiceMock
                .Setup( service => service.LoadArchiveDictionaries( It.IsAny<string>() ) )
                .Returns<string>( archiveFullPath => {
                    var defaultDictionaryResult = TranslationDictionaryServiceMock.Object.LoadDictionary( archiveFullPath );
                    if(defaultDictionaryResult.IsFailed) {
                        return Result.Fail<TranslationArchiveDictionaries>( defaultDictionaryResult.Errors );
                    }

                    var hasJapaneseDictionaryResult = TranslationDictionaryServiceMock.Object.HasArchiveEntry( archiveFullPath, "l10n/JP/dictionary" );
                    if(hasJapaneseDictionaryResult.IsFailed) {
                        return Result.Fail<TranslationArchiveDictionaries>( hasJapaneseDictionaryResult.Errors );
                    }

                    if(!hasJapaneseDictionaryResult.Value) {
                        return Result.Ok( new TranslationArchiveDictionaries( defaultDictionaryResult.Value, false, [] ) );
                    }

                    var japaneseDictionaryResult = TranslationDictionaryServiceMock.Object.LoadDictionary( archiveFullPath, "l10n/JP/dictionary" );
                    return japaneseDictionaryResult.IsFailed
                        ? Result.Fail<TranslationArchiveDictionaries>( japaneseDictionaryResult.Errors )
                        : Result.Ok( new TranslationArchiveDictionaries( defaultDictionaryResult.Value, true, japaneseDictionaryResult.Value ) );
                } );
        }

        internal string TempDirectory { get; }
        internal AppSettings Settings { get; }
        internal Mock<IAppSettingsService> AppSettingsServiceMock { get; } = new();
        internal Mock<IApplicationInfoService> ApplicationInfoServiceMock { get; } = new();
        internal Mock<IDialogService> DialogServiceMock { get; } = new();
        internal Mock<IDialogProvider> DialogProviderMock { get; } = new();
        internal Mock<ISystemService> SystemServiceMock { get; } = new();
        internal Mock<ITranslationDictionaryService> TranslationDictionaryServiceMock { get; } = new();
        internal Mock<ILoggingService> LoggerMock { get; } = new();

        internal TranslationCreationViewModel CreateViewModel( string archiveFullPath ) =>
            new(
                archiveFullPath,
                AppSettingsServiceMock.Object,
                SystemServiceMock.Object,
                LoggerMock.Object,
                new TranslationCreationSession(),
                new TranslationCreationWorkflowService(
                    new TranslationCreationDictionaryLoader( TranslationDictionaryServiceMock.Object ),
                    new TranslationCreationImportExportService(
                        AppSettingsServiceMock.Object,
                        ApplicationInfoServiceMock.Object,
                        TranslationDictionaryServiceMock.Object,
                        new TranslationCreationDialogService( DialogServiceMock.Object, DialogProviderMock.Object, LoggerMock.Object ),
                        new TranslationCreationPathService(),
                        SystemServiceMock.Object,
                        LoggerMock.Object ),
                    LoggerMock.Object ),
                new TranslationCreationLayoutStateService( AppSettingsServiceMock.Object ),
                new TranslationCreationDialogService( DialogServiceMock.Object, DialogProviderMock.Object, LoggerMock.Object ),
                new TranslationCreationFilterService(),
                new TranslationCreationNotificationService( SystemServiceMock.Object, LoggerMock.Object ) );

        public void Dispose() {
            if(Directory.Exists( TempDirectory )) {
                Directory.Delete( TempDirectory, true );
            }
        }
    }

    private static EditableTranslationDictionary CreateEditableDictionary( string originalText ) {
        var normalizedText = originalText.ReplaceLineEndings( "\n" );
        var items = new List<TranslationDictionaryItem>();
        Dictionary<string, TranslationDictionaryValueRange> valueRanges = new( StringComparer.Ordinal );

        foreach(System.Text.RegularExpressions.Match match in DictionaryEntryRegex().Matches( normalizedText )) {
            var key = match.Groups["key"].Value;
            var value = match.Groups["value"].Value;
            items.RemoveAll( item => item.Key == key );
            items.Add( new TranslationDictionaryItem( key, value ) );
            valueRanges[key] = new TranslationDictionaryValueRange( key, match.Groups["value"].Index, match.Groups["value"].Length );
        }

        return new EditableTranslationDictionary( normalizedText, items, valueRanges );
    }

    [GeneratedRegex( "\\[\\s*\\\"(?<key>(?:\\\\(?:\\n|.|\\r)|[^\\\"\\\\])*)\\\"\\s*\\]\\s*=\\s*\\\"(?<value>(?:\\\\(?:\\n|.|\\r)|[^\\\"\\\\])*)\\\"", RegexOptions.CultureInvariant )]
    private static partial Regex DictionaryEntryRegex();

}