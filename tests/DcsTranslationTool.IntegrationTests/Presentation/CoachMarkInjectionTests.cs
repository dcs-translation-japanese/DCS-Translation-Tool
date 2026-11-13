using System.IO.Compression;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Infrastructure.Services;
using DcsTranslationTool.Presentation.Wpf.Features.CoachMarks;
using DcsTranslationTool.Presentation.Wpf.Features.Download;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Presentation.Wpf.ViewModels;
using DcsTranslationTool.Shared.Models;

using FluentResults;

using Moq;

namespace DcsTranslationTool.IntegrationTests.Presentation;

/// <summary>
/// 実ファイルへの翻訳注入と表示済み設定の永続化を検証する統合テスト。
/// </summary>
public sealed class CoachMarkInjectionTests : IDisposable {
    /// <summary>翻訳注入と設定保存の検証に使用する一時フォルダー。</summary>
    private readonly string _root = Path.Combine( Path.GetTempPath(), $"CoachMarkInjection_{Guid.NewGuid():N}" );
    /// <summary>翻訳注入時のログ出力を受け取るモック。</summary>
    private readonly Mock<ILoggingService> _logger = new();

    /// <summary>
    /// 注入と設定保存の検証に使う一時フォルダーを作成する。
    /// </summary>
    public CoachMarkInjectionTests() => Directory.CreateDirectory( _root );

    /// <summary>
    /// 実ファイルへのmiz注入に成功した場合だけ案内が完了することを検証する。
    /// </summary>
    /// <param name="extension">注入対象のアーカイブの拡張子。</param>
    /// <param name="validArchive">有効な ZIP アーカイブを用意するかどうか。</param>
    /// <param name="completed">案内が完了することを期待するかどうか。</param>
    /// <returns>検証の完了を表すタスク。</returns>
    [Theory]
    [InlineData( ".miz", true, true )]
    [InlineData( ".MIZ", true, true )]
    [InlineData( ".trk", true, false )]
    [InlineData( ".miz", false, false )]
    public async Task 実ファイルへのmiz注入に成功した場合だけ案内が完了する( string extension, bool validArchive, bool completed ) {
        var settingsDirectory = Path.Combine( _root, "settings" );
        await using var store = new AppSettingsService( _logger.Object, settingsDirectory, "appsettings.json" );
        var translations = Path.Combine( _root, "translations" );
        var missions = Path.Combine( _root, "missions" );
        Directory.CreateDirectory( missions );
        store.Settings.DcsWorldInstallDir = _root;
        store.Settings.SourceUserMissionDir = missions;
        store.Settings.TranslateFileDir = translations;
        var coach = new CoachMarkService( store );
        await coach.StartAsync();
        await coach.NextAsync();
        await coach.NextAsync();
        await coach.NextAsync();
        await coach.NextAsync();

        var archiveName = "Example" + extension;
        var entryPath = $"DCSWorld/Mods/aircraft/TF-51D/Missions/{archiveName}/l10n/DEFAULT/dictionary";
        var source = Path.Combine( translations, entryPath );
        Directory.CreateDirectory( Path.GetDirectoryName( source )! );
        await File.WriteAllTextAsync( source, "Japanese translation", TestContext.Current.CancellationToken );
        var archivePath = Path.Combine( _root, "Mods", "aircraft", "TF-51D", "Missions", archiveName );
        Directory.CreateDirectory( Path.GetDirectoryName( archivePath )! );
        if(validArchive) {
            using var archive = ZipFile.Open( archivePath, ZipArchiveMode.Create );
            archive.CreateEntry( "mission" );
        }
        else {
            await File.WriteAllTextAsync( archivePath, "not a zip", TestContext.Current.CancellationToken );
        }

        var entry = new FileEntryViewModel(
            new LocalFileEntry( "dictionary", entryPath, false, "local" ), ChangeTypeMode.Download, _logger.Object );
        var guard = new PathSafetyGuard();
        var service = new EntryApplyService( _logger.Object, guard, new ZipService( _logger.Object ), coach );
        var api = new Mock<IApiService>( MockBehavior.Strict );
        var workflow = new DownloadWorkflowService( api.Object, _logger.Object,
            new ApplyWorkflowService( new RepoOnlySyncService( api.Object, _logger.Object, guard ), service ) );
        var dispatcher = new Mock<IDispatcherService>();
        dispatcher.Setup( value => value.InvokeAsync( It.IsAny<Func<Task>>() ) ).Returns<Func<Task>>( action => action() );
        var files = new Mock<IFileEntryService>();
        files.Setup( value => value.GetEntriesAsync() ).ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( [] ) );
        var viewModel = new DownloadViewModel(
            api.Object, store, Mock.Of<IDialogService>(), workflow, dispatcher.Object, files.Object,
            Mock.Of<IFileEntryWatcherLifecycle>(), new FileEntryTreeService( _logger.Object ),
            _logger.Object, Mock.Of<ISnackbarService>(), Mock.Of<ISystemService>(), coach );
        entry.CheckState = true;
        viewModel.Tabs.Add( new TabItemViewModel( CategoryType.Aircraft, _logger.Object, entry ) );
        viewModel.SelectedTabIndex = -1;
        viewModel.SelectedTabIndex = 0;
        var waitingStep = extension.Equals( ".trk", StringComparison.OrdinalIgnoreCase )
            ? CoachMarkStep.SelectTranslation : CoachMarkStep.ApplyTranslation;
        Assert.Equal( waitingStep, coach.Step );
        await viewModel.Apply();

        Assert.Equal( completed ? CoachMarkStep.TranslationApplied : waitingStep, coach.Step );
        if(validArchive) {
            using var archive = ZipFile.OpenRead( archivePath );
            using var reader = new StreamReader( archive.GetEntry( "l10n/DEFAULT/dictionary" )!.Open() );
            Assert.Equal( "Japanese translation", await reader.ReadToEndAsync( TestContext.Current.CancellationToken ) );
            Assert.NotNull( archive.GetEntry( "mission" ) );
        }
    }

    /// <summary>
    /// 新規と既存ユーザーの初回だけ自動表示し実際の保存を読み直しても再表示しないことを検証する。
    /// </summary>
    /// <param name="json">起動前に保存する設定 JSON。新規ユーザーの場合は null。</param>
    /// <returns>検証の完了を表すタスク。</returns>
    [Theory]
    [InlineData( null )]
    [InlineData( "{}" )]
    [InlineData( "{\"HasSeenCoachMark\":false}" )]
    public async Task 新規と既存ユーザーの初回だけ自動表示し実際の保存を読み直しても再表示しない( string? json ) {
        var directory = Path.Combine( _root, "profile" );
        Directory.CreateDirectory( directory );
        if(json is not null) {
            await File.WriteAllTextAsync( Path.Combine( directory, "appsettings.json" ), json, TestContext.Current.CancellationToken );
        }
        await using(var store = new AppSettingsService( _logger.Object, directory, "appsettings.json" )) {
            var coach = new CoachMarkService( store );
            await coach.StartAsync( automatic: true );
            Assert.True( coach.IsActive );
        }
        await using var reloaded = new AppSettingsService( _logger.Object, directory, "appsettings.json" );
        var nextSession = new CoachMarkService( reloaded );
        await nextSession.StartAsync( automatic: true );
        Assert.False( nextSession.IsActive );
        await nextSession.StartAsync();
        Assert.Equal( CoachMarkStep.InstallDirectory, nextSession.Step );
    }

    /// <summary>
    /// テスト用の一時フォルダーとその内容を削除する。
    /// </summary>
    public void Dispose() => Directory.Delete( _root, true );
}