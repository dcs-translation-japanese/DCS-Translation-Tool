using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Features.CoachMarks;
using DcsTranslationTool.Presentation.Wpf.Features.Settings;
using DcsTranslationTool.Resources;
using DcsTranslationTool.Shared.Models;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.UnitTests.Features.CoachMarks;

/// <summary>
/// 操作案内の表示条件、段階遷移、設定保存、完了条件を検証する。
/// </summary>
public sealed class CoachMarkServiceTests : IDisposable {
    /// <summary>操作案内の設定とフォルダー検証に使用する一時フォルダー。</summary>
    private readonly string _directory = Path.Combine( Path.GetTempPath(), $"CoachMarks_{Guid.NewGuid():N}" );
    /// <summary>操作案内の状態遷移の検証に使用するアプリケーション設定。</summary>
    private readonly AppSettings _settings = new();
    /// <summary>設定の読み込みと保存を検証するモック。</summary>
    private readonly Mock<IAppSettingsService> _store = new();

    /// <summary>
    /// テスト用フォルダーと設定サービスのモックを用意する。
    /// </summary>
    public CoachMarkServiceTests() {
        Directory.CreateDirectory( _directory );
        _store.SetupGet( service => service.Settings ).Returns( _settings );
        _store.Setup( service => service.SaveAsync( It.IsAny<CancellationToken>() ) ).Returns( Task.CompletedTask );
    }

    /// <summary>
    /// 自動起動は未表示のユーザーだけを対象にすることを検証する。
    /// </summary>
    /// <param name="seen">案内を表示済みとして保存するかどうか。</param>
    /// <param name="expected">自動で案内が始まることを期待するかどうか。</param>
    /// <returns>検証の完了を表すタスク。</returns>
    [Theory]
    [InlineData( false, true )]
    [InlineData( true, false )]
    public async Task 自動起動は未表示のユーザーだけを対象にする( bool seen, bool expected ) {
        _settings.HasSeenCoachMark = seen;
        var sut = Create();
        await sut.StartAsync( automatic: true );
        Assert.Equal( expected, sut.IsActive );
        Assert.True( _settings.HasSeenCoachMark );
    }

    /// <summary>自動起動は導入を表示し開始またはスキップの操作を待つことを検証する。</summary>
    /// <returns>検証の完了を表すタスク。</returns>
    [Theory]
    [InlineData( false )]
    [InlineData( true )]
    public async Task 自動起動は導入を表示し開始またはスキップの操作を待つ( bool skip ) {
        var sut = Create();
        await sut.StartAsync( automatic: true );
        Assert.Equal( CoachMarkStep.Introduction, sut.Step );
        Assert.Empty( sut.Target );
        Assert.False( sut.IsSettingsStep );
        Assert.True( sut.CanNext );
        Assert.Equal( Strings_CoachMarks.IntroductionTitle, sut.Title );
        Assert.Equal( Strings_CoachMarks.IntroductionDescription, sut.Description );
        Assert.Equal( Strings_CoachMarks.BeginButtonText, sut.NextButtonText );
        if(skip) {
            sut.Skip();
            Assert.Equal( CoachMarkStep.Completed, sut.Step );
            Assert.False( sut.CanSkip );
            await sut.NextAsync();
            Assert.False( sut.IsActive );
            var nextSession = Create();
            await nextSession.StartAsync( automatic: true );
            Assert.False( nextSession.IsActive );
        }
        else {
            await sut.NextAsync();
            Assert.Equal( CoachMarkStep.InstallDirectory, sut.Step );
        }
        _store.Verify( service => service.SaveAsync( It.IsAny<CancellationToken>() ), Times.Once );
    }

    /// <summary>導入中の翻訳処理通知では案内を進めたり完了したりしないことを検証する。</summary>
    /// <returns>検証の完了を表すタスク。</returns>
    [Fact]
    public async Task 導入中の翻訳処理通知では案内を進めたり完了したりしない() {
        var sut = Create();
        await sut.StartAsync( automatic: true );
        sut.UpdateSelection( true );
        sut.BeginApply();
        sut.RecordApplyResult( true, false );
        sut.ApplyFinished( true );
        Assert.Equal( CoachMarkStep.Introduction, sut.Step );
    }

    /// <summary>
    /// 開始時に表示済みを保存しスキップや途中終了後は自動起動しないことを検証する。
    /// </summary>
    /// <param name="skip">終了前に案内をスキップするかどうか。</param>
    /// <returns>検証の完了を表すタスク。</returns>
    [Theory]
    [InlineData( false )]
    [InlineData( true )]
    public async Task 開始時に表示済みを保存しスキップや途中終了後は自動起動しない( bool skip ) {
        var sut = Create();
        await sut.StartAsync( automatic: true );
        _store.Verify( service => service.SaveAsync( It.IsAny<CancellationToken>() ), Times.Once );
        if(skip) sut.Skip();
        var nextSession = Create();
        await nextSession.StartAsync( automatic: true );
        Assert.False( nextSession.IsActive );
    }

    /// <summary>表示するステップ番号は省略の有無にかかわらず連番になることを検証する。</summary>
    /// <returns>検証の完了を表すタスク。</returns>
    [Theory]
    [InlineData( false, false )]
    [InlineData( false, true )]
    [InlineData( true, false )]
    [InlineData( true, true )]
    public async Task 表示するステップ番号は省略の有無にかかわらず連番になる( bool automatic, bool needsDownloadDirectory ) {
        ValidSettings();
        if(needsDownloadDirectory) _settings.TranslateFileDir = string.Empty;
        var sut = Create();
        await sut.StartAsync( automatic );
        if(automatic) await sut.NextAsync();
        var steps = Enum.GetValues<CoachMarkStep>().Where( step =>
            step is not (CoachMarkStep.Inactive or CoachMarkStep.Introduction) &&
            (needsDownloadDirectory || step != CoachMarkStep.DownloadDirectory) ).ToArray();
        for(var index = 0; index < steps.Length; index++) {
            Assert.Equal( steps[index], sut.Step );
            var prefix = $"{index + 1} / {steps.Length}　";
            Assert.StartsWith( prefix, sut.Title );
            switch(sut.Step) {
                case CoachMarkStep.DownloadDirectory:
                    _settings.TranslateFileDir = Path.Combine( _directory, "download" );
                    Assert.StartsWith( prefix, sut.Title );
                    await sut.NextAsync();
                    break;
                case CoachMarkStep.SelectTranslation:
                    sut.UpdateSelection( true );
                    break;
                case CoachMarkStep.ApplyTranslation:
                    sut.BeginApply();
                    sut.RecordApplyResult( true, false );
                    sut.ApplyFinished( true );
                    break;
                default:
                    await sut.NextAsync();
                    break;
            }
        }
        Assert.False( sut.IsActive );
    }

    /// <summary>再実行では設定済みのダウンロード先を省略して番号を振り直すことを検証する。</summary>
    /// <returns>検証の完了を表すタスク。</returns>
    [Fact]
    public async Task 再実行では設定済みのダウンロード先を省略して番号を振り直す() {
        ValidSettings();
        _settings.TranslateFileDir = string.Empty;
        var sut = Create();
        await sut.StartAsync();
        await sut.NextAsync();
        await sut.NextAsync();
        Assert.Equal( CoachMarkStep.DownloadDirectory, sut.Step );
        Assert.StartsWith( "3 / 12", sut.Title );
        _settings.TranslateFileDir = Path.Combine( _directory, "download" );
        await sut.StartAsync();
        Assert.StartsWith( "1 / 11", sut.Title );
        await sut.NextAsync();
        await sut.NextAsync();
        Assert.Equal( CoachMarkStep.AircraftRecommendation, sut.Step );
        Assert.StartsWith( "3 / 11", sut.Title );
    }

    /// <summary>ユーザーフォルダーは設定済みでも入力確認を案内することを検証する。</summary>
    /// <returns>検証の完了を表すタスク。</returns>
    [Theory]
    [InlineData( false )]
    [InlineData( true )]
    public async Task ユーザーフォルダーは設定済みでも入力確認を案内する( bool configured ) {
        ValidSettings();
        _settings.SourceUserMissionDir = configured ? _directory : string.Empty;
        var sut = Create();
        await sut.StartAsync();
        await sut.NextAsync();
        Assert.Equal( CoachMarkStep.UserDirectory, sut.Step );
        Assert.Equal( "UserMission", sut.Target );
        Assert.Equal( configured ? _directory : string.Empty, _settings.SourceUserMissionDir );
        if(!configured) {
            await sut.NextAsync();
            Assert.Equal( CoachMarkStep.UserDirectory, sut.Step );
            Assert.NotEmpty( sut.ErrorMessage );
        }
        _settings.SourceUserMissionDir = _directory;
        await sut.NextAsync();
        Assert.Equal( CoachMarkStep.AircraftRecommendation, sut.Step );
    }

    /// <summary>
    /// 手動起動は表示済みでも必ず最初から始まることを検証する。
    /// </summary>
    /// <returns>検証の完了を表すタスク。</returns>
    [Fact]
    public async Task 手動起動は表示済みでも必ず最初から始まる() {
        var sut = Create();
        await sut.StartAsync();
        ValidSettings();
        await sut.NextAsync();
        Assert.Equal( CoachMarkStep.UserDirectory, sut.Step );
        await sut.StartAsync();
        Assert.Equal( CoachMarkStep.InstallDirectory, sut.Step );
    }

    /// <summary>
    /// 最初の段階で再実行してもエラー表示を消して最初から案内することを検証する。
    /// </summary>
    /// <returns>検証の完了を表すタスク。</returns>
    [Fact]
    public async Task 最初の段階で再実行してもエラー表示を消して最初から案内する() {
        var sut = Create();
        await sut.StartAsync();
        await sut.NextAsync();
        Assert.NotEmpty( sut.ErrorMessage );
        await sut.StartAsync();
        Assert.Equal( CoachMarkStep.InstallDirectory, sut.Step );
        Assert.Empty( sut.ErrorMessage );
    }

    /// <summary>
    /// 保存に失敗した場合は開始せず表示済みにもしないことを検証する。
    /// </summary>
    /// <returns>検証の完了を表すタスク。</returns>
    [Fact]
    public async Task 保存に失敗した場合は開始せず表示済みにもしない() {
        _store.Setup( service => service.SaveAsync( It.IsAny<CancellationToken>() ) ).ThrowsAsync( new IOException( "save failed" ) );
        var sut = Create();
        await Assert.ThrowsAsync<IOException>( () => sut.StartAsync() );
        Assert.False( sut.IsActive );
        Assert.False( _settings.HasSeenCoachMark );
    }

    /// <summary>
    /// 必須設定が不正なら進まず既存設定を変更しないことを検証する。
    /// </summary>
    /// <returns>検証の完了を表すタスク。</returns>
    [Fact]
    public async Task 必須設定が不正なら進まず既存設定を変更しない() {
        var sut = Create();
        await sut.StartAsync();
        _settings.DcsWorldInstallDir = Path.Combine( _directory, "missing" );
        await sut.NextAsync();
        Assert.Equal( CoachMarkStep.InstallDirectory, sut.Step );
        Assert.NotEmpty( sut.ErrorMessage );
        sut.Skip();
        Assert.Equal( Path.Combine( _directory, "missing" ), _settings.DcsWorldInstallDir );
    }

    /// <summary>
    /// 有効な設定は保存して再入力なしで推奨設定へ進むことを検証する。
    /// </summary>
    /// <returns>検証の完了を表すタスク。</returns>
    [Fact]
    public async Task 有効な設定は保存して再入力なしで推奨設定へ進む() {
        ValidSettings();
        var sut = Create();
        await sut.StartAsync();
        await sut.NextAsync();
        Assert.Equal( CoachMarkStep.UserDirectory, sut.Step );
        await sut.NextAsync();
        Assert.Equal( CoachMarkStep.AircraftRecommendation, sut.Step );
        _store.Verify( service => service.SaveAsync( It.IsAny<CancellationToken>() ), Times.Exactly( 3 ) );
        Assert.False( Directory.Exists( _settings.TranslateFileDir ) );
    }

    /// <summary>
    /// 設定の保存に失敗しても現在の段階に留まることを検証する。
    /// </summary>
    /// <returns>検証の完了を表すタスク。</returns>
    [Fact]
    public async Task 設定の保存に失敗しても現在の段階に留まる() {
        ValidSettings();
        var sut = Create();
        await sut.StartAsync();
        _store.Setup( service => service.SaveAsync( It.IsAny<CancellationToken>() ) ).ThrowsAsync( new IOException() );
        await Assert.ThrowsAsync<IOException>( () => sut.NextAsync() );
        Assert.Equal( CoachMarkStep.InstallDirectory, sut.Step );
        Assert.True( sut.CanNext );
    }

    /// <summary>
    /// 推奨設定は変更せずDownloadへ進めることを検証する。
    /// </summary>
    /// <returns>検証の完了を表すタスク。</returns>
    [Fact]
    public async Task 推奨設定は変更せずDownloadへ進める() {
        var sut = await AtDownloadAsync();
        Assert.Equal( CoachMarkStep.SelectTranslation, sut.Step );
        Assert.False( _settings.UseExternalAircraftInjectionDir );
        Assert.False( _settings.UseExternalCampaignInjectionDir );
    }

    /// <summary>
    /// miz翻訳を選ぶと適用操作の案内へ直接進むことを検証する。
    /// </summary>
    /// <returns>検証の完了を表すタスク。</returns>
    [Fact]
    public async Task miz翻訳を選ぶと6段階目の適用へ直接進む() {
        var sut = await AtDownloadAsync();
        sut.UpdateSelection( hasMizTranslation: false );
        Assert.Equal( CoachMarkStep.SelectTranslation, sut.Step );
        sut.UpdateSelection( hasMizTranslation: true );
        Assert.Equal( CoachMarkStep.ApplyTranslation, sut.Step );
        Assert.Equal( "ApplyTranslation", sut.Target );
        Assert.StartsWith( "6 / 11", sut.Title );
        Assert.Contains( "「適用」", sut.Description );
        Assert.False( sut.CanNext );
        sut.BeginApply();
        sut.RecordApplyResult( true, false );
        sut.ApplyFinished( true );
        Assert.Equal( CoachMarkStep.TranslationApplied, sut.Step );
        Assert.StartsWith( "7 / 11", sut.Title );
    }

    /// <summary>翻訳の注入成功後に、ダウンロード、翻訳作成、アップロードの Tips を順に表示して案内を完了することを検証する。</summary>
    /// <returns>検証の完了を表すタスク。</returns>
    [Fact]
    public async Task 注入成功後はDLと翻訳とUploadのTipsを順に表示して完了する() {
        var sut = await AtDownloadAsync();
        sut.UpdateSelection( true );
        sut.BeginApply();
        sut.RecordApplyResult( true, false );
        sut.ApplyFinished( true );
        Assert.Contains( "注入", sut.Description );
        await sut.NextAsync();
        Assert.Equal( "DownloadTip", sut.Step.ToString() );
        Assert.Equal( "DownloadTranslation", sut.Target );
        Assert.Contains( "ダウンロードするだけ", sut.Description );
        await sut.NextAsync();
        Assert.Equal( "TranslationTip", sut.Step.ToString() );
        Assert.Equal( "OpenTranslation", sut.Target );
        Assert.Contains( "自作", sut.Description );
        await sut.NextAsync();
        Assert.Equal( "UploadTip", sut.Step.ToString() );
        Assert.Equal( "OpenUpload", sut.Target );
        Assert.Contains( "アップロード", sut.Description );
        await sut.NextAsync();
        Assert.Equal( CoachMarkStep.Completed, sut.Step );
        Assert.Contains( "設定画面", sut.Description );
        Assert.Equal( Strings_CoachMarks.CloseButtonText, sut.NextButtonText );
        await sut.NextAsync();
        Assert.False( sut.IsActive );
        _store.Verify( service => service.SaveAsync( It.IsAny<CancellationToken>() ), Times.Exactly( 5 ) );
    }

    /// <summary>スキップ時も完了と設定画面からの再表示方法を案内することを検証する。</summary>
    /// <returns>検証の完了を表すタスク。</returns>
    [Fact]
    public async Task スキップ時も完了と設定画面からの再表示方法を案内する() {
        var sut = Create();
        await sut.StartAsync( automatic: true );
        sut.Skip();
        Assert.Equal( CoachMarkStep.Completed, sut.Step );
        Assert.True( sut.IsActive );
        Assert.Contains( "完了", sut.Title );
        Assert.Contains( "設定画面", sut.Description );
        Assert.DoesNotContain( "注入されました", sut.Description );
        await sut.NextAsync();
        Assert.False( sut.IsActive );
        sut.Skip();
        Assert.False( sut.IsActive );
    }

    /// <summary>
    /// 翻訳の選択を解除すると選択操作の案内へ戻ることを検証する。
    /// </summary>
    /// <returns>検証の完了を表すタスク。</returns>
    [Fact]
    public async Task 選択を解除すると翻訳選択の案内へ戻る() {
        var sut = await AtDownloadAsync();
        sut.UpdateSelection( true );
        Assert.Equal( CoachMarkStep.ApplyTranslation, sut.Step );
        Assert.StartsWith( "6 / 11", sut.Title );
        sut.UpdateSelection( false );
        Assert.Equal( CoachMarkStep.SelectTranslation, sut.Step );
        Assert.StartsWith( "5 / 11", sut.Title );
    }

    /// <summary>
    /// 実際のmiz注入成功かつ処理全体成功の場合だけ完了することを検証する。
    /// </summary>
    /// <param name="injectedMiz">実際の .miz への注入に成功したかどうか。</param>
    /// <param name="failed">注入処理に失敗が含まれるかどうか。</param>
    /// <param name="workflowSucceeded">注入ワークフロー全体が成功したかどうか。</param>
    /// <param name="completed">案内が完了することを期待するかどうか。</param>
    /// <returns>検証の完了を表すタスク。</returns>
    [Theory]
    [InlineData( true, false, true, true )]
    [InlineData( false, false, true, false )]
    [InlineData( true, true, true, false )]
    [InlineData( true, false, false, false )]
    public async Task 実際のmiz注入成功かつ処理全体成功の場合だけ完了する(
        bool injectedMiz, bool failed, bool workflowSucceeded, bool completed ) {
        var sut = await AtDownloadAsync();
        sut.UpdateSelection( true );
        sut.BeginApply();
        sut.RecordApplyResult( injectedMiz, failed );
        sut.ApplyFinished( workflowSucceeded );
        Assert.Equal( completed ? CoachMarkStep.TranslationApplied : CoachMarkStep.ApplyTranslation, sut.Step );
    }

    /// <summary>
    /// 複数グループの一部が失敗した場合は完了せず再試行できることを検証する。
    /// </summary>
    /// <returns>検証の完了を表すタスク。</returns>
    [Fact]
    public async Task 複数グループの一部が失敗した場合は完了せず再試行できる() {
        var sut = await AtDownloadAsync();
        sut.UpdateSelection( true );
        sut.BeginApply();
        sut.RecordApplyResult( true, false );
        sut.RecordApplyResult( false, true );
        sut.ApplyFinished( true );
        Assert.Equal( CoachMarkStep.ApplyTranslation, sut.Step );
        sut.BeginApply();
        sut.RecordApplyResult( true, false );
        sut.ApplyFinished( true );
        Assert.Equal( CoachMarkStep.TranslationApplied, sut.Step );
    }

    /// <summary>
    /// スキップ後に処理が成功してもTipsを表示しないことを検証する。
    /// </summary>
    /// <returns>検証の完了を表すタスク。</returns>
    [Fact]
    public async Task スキップ後に処理が成功してもTipsを表示しない() {
        var sut = await AtDownloadAsync();
        sut.BeginApply();
        sut.Skip();
        sut.RecordApplyResult( true, false );
        sut.ApplyFinished( true );
        Assert.Equal( CoachMarkStep.Completed, sut.Step );
        await sut.NextAsync();
        Assert.False( sut.IsActive );
    }

    /// <summary>
    /// 非実在フォルダーはダウンロード先だけ許可することを検証する。
    /// </summary>
    /// <param name="key">検証対象となるフォルダー設定のキー。</param>
    /// <param name="valid">フォルダー設定が検証を通過することを期待するかどうか。</param>
    [Theory]
    [InlineData( "DcsWorldInstall", false )]
    [InlineData( "UserMission", false )]
    [InlineData( "TranslateFile", true )]
    public void 非実在フォルダーはダウンロード先だけ許可する( string key, bool valid ) {
        var path = Path.Combine( _directory, "not-created" );
        Assert.Equal( valid, SettingsDirectoryValidation.GetError( key, path ) is null );
        Assert.Equal( Strings_Settings.DirectoryRequiredMessage, SettingsDirectoryValidation.GetError( key, "  " ) );
        Assert.Null( SettingsDirectoryValidation.GetError( key, _directory ) );
    }

    /// <summary>
    /// 手動再実行の保存中にスキップしても案内を再表示しないことを検証する。
    /// </summary>
    /// <returns>検証の完了を表すタスク。</returns>
    [Fact]
    public async Task 手動再実行の保存中にスキップしても案内を再表示しない() {
        var sut = Create();
        await sut.StartAsync();
        var saved = new TaskCompletionSource( TaskCreationOptions.RunContinuationsAsynchronously );
        _store.Setup( service => service.SaveAsync( It.IsAny<CancellationToken>() ) ).Returns( saved.Task );
        var starting = sut.StartAsync();
        sut.Skip();
        saved.SetResult();
        await starting;
        Assert.Equal( CoachMarkStep.Completed, sut.Step );
    }

    /// <summary>
    /// 必須項目を個別に検証し保存完了前は次の項目へ進めないことを検証する。
    /// </summary>
    /// <returns>検証の完了を表すタスク。</returns>
    [Fact]
    public async Task 必須項目を個別に検証し保存完了前は次の項目へ進めない() {
        _settings.TranslateFileDir = string.Empty;
        var sut = Create();
        await sut.StartAsync();
        _settings.DcsWorldInstallDir = _directory;
        await sut.NextAsync();
        Assert.Equal( CoachMarkStep.UserDirectory, sut.Step );
        await sut.NextAsync();
        Assert.Equal( CoachMarkStep.UserDirectory, sut.Step );
        _settings.SourceUserMissionDir = _directory;
        await sut.NextAsync();
        Assert.Equal( CoachMarkStep.DownloadDirectory, sut.Step );
        await sut.NextAsync();
        Assert.Equal( CoachMarkStep.DownloadDirectory, sut.Step );
        _settings.TranslateFileDir = Path.Combine( _directory, "not-created" );
        var saved = new TaskCompletionSource( TaskCreationOptions.RunContinuationsAsynchronously );
        _store.Setup( service => service.SaveAsync( It.IsAny<CancellationToken>() ) ).Returns( saved.Task );
        var next = sut.NextAsync();
        Assert.False( sut.CanNext );
        Assert.Equal( CoachMarkStep.DownloadDirectory, sut.Step );
        saved.SetResult();
        await next;
        Assert.Equal( CoachMarkStep.AircraftRecommendation, sut.Step );
    }

    /// <summary>
    /// どの段階でも設定を保持してスキップできることを検証する。
    /// </summary>
    /// <param name="step">対象となる案内の段階。</param>
    /// <returns>検証の完了を表すタスク。</returns>
    [Theory]
    [InlineData( CoachMarkStep.Introduction )]
    [InlineData( CoachMarkStep.InstallDirectory )]
    [InlineData( CoachMarkStep.UserDirectory )]
    [InlineData( CoachMarkStep.DownloadDirectory )]
    [InlineData( CoachMarkStep.AircraftRecommendation )]
    [InlineData( CoachMarkStep.CampaignRecommendation )]
    [InlineData( CoachMarkStep.SelectTranslation )]
    [InlineData( CoachMarkStep.ApplyTranslation )]
    [InlineData( CoachMarkStep.TranslationApplied )]
    [InlineData( CoachMarkStep.DownloadTip )]
    [InlineData( CoachMarkStep.TranslationTip )]
    [InlineData( CoachMarkStep.UploadTip )]
    [InlineData( CoachMarkStep.Completed )]
    public async Task どの段階でも設定を保持してスキップできる( CoachMarkStep step ) {
        _settings.TranslateFileDir = string.Empty;
        var sut = Create();
        await sut.StartAsync( automatic: step == CoachMarkStep.Introduction );
        while(sut.Step < step) {
            switch(sut.Step) {
                case CoachMarkStep.InstallDirectory:
                    _settings.DcsWorldInstallDir = _directory;
                    await sut.NextAsync();
                    break;
                case CoachMarkStep.UserDirectory:
                    _settings.SourceUserMissionDir = _directory;
                    await sut.NextAsync();
                    break;
                case CoachMarkStep.DownloadDirectory:
                    _settings.TranslateFileDir = Path.Combine( _directory, "download" );
                    await sut.NextAsync();
                    break;
                case CoachMarkStep.AircraftRecommendation:
                case CoachMarkStep.CampaignRecommendation:
                case CoachMarkStep.TranslationApplied:
                case CoachMarkStep.DownloadTip:
                case CoachMarkStep.TranslationTip:
                case CoachMarkStep.UploadTip:
                    await sut.NextAsync();
                    break;
                case CoachMarkStep.SelectTranslation:
                    sut.UpdateSelection( true );
                    break;
                case CoachMarkStep.ApplyTranslation:
                    sut.BeginApply();
                    sut.RecordApplyResult( true, false );
                    sut.ApplyFinished( true );
                    break;
            }
        }
        var title = step switch
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
        var steps = Enum.GetValues<CoachMarkStep>()
            .Where( value => value is not (CoachMarkStep.Inactive or CoachMarkStep.Introduction) ).ToArray();
        Assert.Equal( step == CoachMarkStep.Introduction ? title : $"{Array.IndexOf( steps, step ) + 1} / {steps.Length}　{title}", sut.Title );
        Assert.False( string.IsNullOrWhiteSpace( sut.Description ) );
        Assert.Equal( step switch
        {
            CoachMarkStep.Introduction => Strings_CoachMarks.BeginButtonText,
            CoachMarkStep.Completed => Strings_CoachMarks.CloseButtonText,
            _ => Strings_CoachMarks.NextButtonText,
        }, sut.NextButtonText );
        var install = _settings.DcsWorldInstallDir;
        var user = _settings.SourceUserMissionDir;
        var download = _settings.TranslateFileDir;
        sut.Skip();
        Assert.Equal( CoachMarkStep.Completed, sut.Step );
        Assert.False( sut.CanSkip );
        Assert.Contains( "設定画面", sut.Description );
        await sut.NextAsync();
        Assert.False( sut.IsActive );
        Assert.Empty( sut.Title );
        Assert.Equal( install, _settings.DcsWorldInstallDir );
        Assert.Equal( user, _settings.SourceUserMissionDir );
        Assert.Equal( download, _settings.TranslateFileDir );
    }

    /// <summary>
    /// すべての案内文言を埋め込みリソースから取得できることを検証する。
    /// </summary>
    [Fact]
    public void すべての案内文言を埋め込みリソースから取得できる() {
        var properties = typeof( Strings_CoachMarks ).GetProperties()
            .Where( property => property.PropertyType == typeof( string ) );
        foreach(var property in properties) {
            Assert.False( string.IsNullOrWhiteSpace( (string?)property.GetValue( null ) ), property.Name );
        }
    }

    /// <summary>
    /// テスト用の設定サービスを使う操作案内サービスを作成する。
    /// </summary>
    /// <returns>テスト対象の操作案内サービス。</returns>
    private CoachMarkService Create() => new( _store.Object );

    /// <summary>
    /// 必須フォルダーに有効な値を設定し、ダウンロード先には未作成のパスを指定する。
    /// </summary>
    private void ValidSettings() {
        _settings.DcsWorldInstallDir = _directory;
        _settings.SourceUserMissionDir = _directory;
        _settings.TranslateFileDir = Path.Combine( _directory, "download" );
    }

    /// <summary>
    /// 有効な設定を保存して推奨設定を通過し、翻訳選択の段階まで案内を進める。
    /// </summary>
    /// <returns>翻訳選択の段階まで進めた操作案内サービスを返すタスク。</returns>
    private async Task<CoachMarkService> AtDownloadAsync() {
        ValidSettings();
        var sut = Create();
        await sut.StartAsync();
        await sut.NextAsync();
        await sut.NextAsync();
        await sut.NextAsync();
        await sut.NextAsync();
        return sut;
    }

    /// <summary>
    /// テスト用の一時フォルダーとその内容を削除する。
    /// </summary>
    public void Dispose() => Directory.Delete( _directory, true );
}