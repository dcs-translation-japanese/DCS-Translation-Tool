using System.Reflection;
using System.Windows.Navigation;

using Caliburn.Micro;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Presentation.Wpf.Features.CoachMarks;
using DcsTranslationTool.Presentation.Wpf.Features.Download;
using DcsTranslationTool.Presentation.Wpf.Features.Main;
using DcsTranslationTool.Presentation.Wpf.Features.Settings;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.Shell;
using DcsTranslationTool.Shared.Models;
using DcsTranslationTool.TestCommon.Wpf;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.UnitTests.Shell;

/// <summary>設定・注入では遷移操作を案内し、Tips ではメニューに自動遷移することを検証する。</summary>
[Collection( "WPF application resources" )]
public sealed class CoachMarkNavigationTests {
    /// <summary>設定画面とダウンロード画面へは手動で移動し、Tips の表示時にはメイン画面へ自動で戻ることを検証する。</summary>
    /// <returns>検証の完了を表すタスク。</returns>
    [StaFact]
    public async Task 設定とDownloadは手動で移動しTipsではMainViewに自動で戻る() {
        EnsureViewResources();
        var main = new MainView();
        var settingsPage = new SettingsView();
        var download = new DownloadView();
        object currentPage = main;
        var navigation = new Mock<INavigationService>();
        navigation.SetupGet( service => service.CurrentContent ).Returns( () => currentPage );
        navigation.SetupGet( service => service.CanGoBack ).Returns( true );
        var settings = new AppSettings
        {
            DcsWorldInstallDir = Path.GetTempPath(),
            SourceUserMissionDir = Path.GetTempPath(),
            TranslateFileDir = Path.GetTempPath(),
        };
        var store = new Mock<IAppSettingsService>();
        store.SetupGet( service => service.Settings ).Returns( settings );
        store.Setup( service => service.SaveAsync( It.IsAny<CancellationToken>() ) ).Returns( Task.CompletedTask );
        var update = new Mock<IUpdateCheckService>();
        update.Setup( service => service.CheckForUpdateAsync( It.IsAny<CancellationToken>() ) ).ReturnsAsync( UpdateCheckResult.NoUpdate );
        var coach = new CoachMarkService( store.Object );
        var shell = new ShellViewModel( store.Object, Mock.Of<IEventAggregator>(), Mock.Of<ILoggingService>(),
            navigation.Object, Mock.Of<ISnackbarService>(), Mock.Of<ISystemService>(), update.Object, coachMarks: coach );
        var notifications = new List<string?>();
        shell.PropertyChanged += ( _, e ) => notifications.Add( e.PropertyName );

        void Navigate( object page ) {
            currentPage = page;
            var ctor = typeof( NavigationEventArgs ).GetConstructors( BindingFlags.Instance | BindingFlags.NonPublic ).Single();
            var args = (NavigationEventArgs)ctor.Invoke( [null, page, null, null, null, false] );
            navigation.Raise( service => service.Navigated += null, args );
        }

        navigation.Setup( service => service.NavigateToViewModel<MainViewModel>( It.IsAny<object?>() ) )
            .Callback( () => Navigate( main ) );

        await shell.ActivateAsync( CancellationToken.None );
        Assert.Equal( 1, navigation.Invocations.Count( invocation => invocation.Method.Name.StartsWith( "Navigate", StringComparison.Ordinal ) ) );
        Assert.Equal( CoachMarkStep.Introduction, coach.Step );
        Assert.Empty( shell.CoachMarkTarget );
        Assert.True( shell.CanNextCoachMark );
        await shell.NextCoachMark();
        Assert.Same( main, currentPage );
        Assert.Equal( 1, navigation.Invocations.Count( invocation => invocation.Method.Name.StartsWith( "Navigate", StringComparison.Ordinal ) ) );
        Assert.Equal( "OpenSettings", shell.CoachMarkTarget );
        Assert.False( shell.CanNextCoachMark );
        Assert.NotEqual( coach.Title, shell.CoachMarkTitle );
        Assert.NotEqual( coach.Description, shell.CoachMarkDescription );
        await shell.NextCoachMark();
        Assert.Equal( CoachMarkStep.InstallDirectory, coach.Step );
        navigation.Invocations.Clear();

        Navigate( settingsPage );
        Assert.Equal( coach.Target, shell.CoachMarkTarget );
        Assert.True( shell.CanNextCoachMark );
        Assert.Equal( coach.Title, shell.CoachMarkTitle );
        Assert.Equal( coach.Description, shell.CoachMarkDescription );
        await shell.NextCoachMark();
        Assert.Equal( CoachMarkStep.UserDirectory, coach.Step );
        Assert.Equal( "UserMission", shell.CoachMarkTarget );
        await shell.NextCoachMark();
        await shell.NextCoachMark();
        await shell.NextCoachMark();
        Assert.Equal( CoachMarkStep.SelectTranslation, coach.Step );
        Assert.Same( settingsPage, currentPage );
        Assert.Equal( "GoBack", shell.CoachMarkTarget );
        Assert.False( shell.CanNextCoachMark );
        Assert.DoesNotContain( navigation.Invocations, invocation => invocation.Method.Name.StartsWith( "Navigate", StringComparison.Ordinal ) );

        Navigate( main );
        Assert.Equal( "OpenDownload", shell.CoachMarkTarget );
        Assert.False( shell.CanNextCoachMark );
        Navigate( download );
        Assert.Equal( "DCSWorld/Mods/aircraft/TF-51D", shell.CoachMarkTarget );
        Assert.Equal( coach.Description, shell.CoachMarkDescription );
        Assert.Contains( nameof( ShellViewModel.CoachMarkTarget ), notifications );
        Assert.Contains( nameof( ShellViewModel.CanNextCoachMark ), notifications );

        coach.UpdateSelection( true );
        coach.BeginApply();
        coach.RecordApplyResult( true, false );
        coach.ApplyFinished( true );
        Assert.Equal( CoachMarkStep.TranslationApplied, coach.Step );
        await shell.NextCoachMark();
        Assert.Equal( "DownloadTranslation", shell.CoachMarkTarget );
        Assert.True( shell.CanNextCoachMark );
        await shell.NextCoachMark();
        Assert.Equal( CoachMarkStep.TranslationTip, coach.Step );
        Assert.Same( main, currentPage );
        navigation.Verify( service => service.NavigateToViewModel<MainViewModel>( It.IsAny<object?>() ), Times.Once );
        Assert.Equal( "OpenTranslation", shell.CoachMarkTarget );
        Assert.True( shell.CanNextCoachMark );
        await shell.NextCoachMark();
        Assert.Equal( "OpenUpload", shell.CoachMarkTarget );
        await shell.NextCoachMark();
        Assert.Equal( CoachMarkStep.Completed, coach.Step );
        Assert.Empty( shell.CoachMarkTarget );
        await shell.NextCoachMark();
        Assert.False( coach.IsActive );
        navigation.Verify( service => service.NavigateToViewModel<MainViewModel>( It.IsAny<object?>() ), Times.Once );
        navigation.Invocations.Clear();

        Navigate( settingsPage );
        await coach.StartAsync();
        Assert.Equal( CoachMarkStep.InstallDirectory, coach.Step );
        Assert.Equal( "DcsWorldInstall", shell.CoachMarkTarget );
        Assert.True( shell.CanNextCoachMark );
        coach.Skip();
        Assert.Equal( CoachMarkStep.Completed, coach.Step );
        Assert.Empty( shell.CoachMarkTarget );
        Assert.True( shell.CanNextCoachMark );
        await shell.NextCoachMark();
        Assert.False( shell.CanNextCoachMark );
        Assert.DoesNotContain( navigation.Invocations, invocation => invocation.Method.Name.StartsWith( "Navigate", StringComparison.Ordinal ) );
    }

    /// <summary>実際の起動経路で、初回起動時にだけ操作案内を自動表示することを検証する。</summary>
    /// <returns>検証の完了を表すタスク。</returns>
    [StaTheory]
    [InlineData( false )]
    [InlineData( true )]
    public async Task 実際の起動経路で初回だけツアーを自動表示する( bool seen ) {
        EnsureViewResources();
        var settings = new AppSettings { HasSeenCoachMark = seen };
        var store = new Mock<IAppSettingsService>();
        store.SetupGet( service => service.Settings ).Returns( settings );
        store.Setup( service => service.SaveAsync( It.IsAny<CancellationToken>() ) ).Returns( Task.CompletedTask );
        var navigation = new Mock<INavigationService>();
        navigation.SetupGet( service => service.CurrentContent ).Returns( new MainView() );
        var update = new Mock<IUpdateCheckService>();
        update.Setup( service => service.CheckForUpdateAsync( It.IsAny<CancellationToken>() ) ).ReturnsAsync( UpdateCheckResult.NoUpdate );
        var coach = new CoachMarkService( store.Object );
        var shell = new ShellViewModel( store.Object, Mock.Of<IEventAggregator>(), Mock.Of<ILoggingService>(),
            navigation.Object, Mock.Of<ISnackbarService>(), Mock.Of<ISystemService>(), update.Object, coachMarks: coach );
        var window = new ShellView { ShowInTaskbar = false };
        try {
            ViewModelBinder.Bind( shell, window, null );
            await ScreenExtensions.TryActivateAsync( shell );
            window.Show();
            WpfTestHelper.PumpDispatcher();
            window.UpdateLayout();

            var overlay = WpfTestHelper.GetRequiredDescendant<CoachMarkOverlay>( window );
            Assert.Equal( !seen, overlay.IsVisible );
            Assert.Equal( !seen, coach.IsActive );
            Assert.Empty( overlay.Target );
            Assert.Equal( seen ? CoachMarkStep.Inactive : CoachMarkStep.Introduction, coach.Step );
            Assert.Equal( !seen, shell.CanNextCoachMark );
            if(!seen) Assert.Equal( DcsTranslationTool.Resources.Strings_CoachMarks.BeginButtonText, coach.NextButtonText );
            Assert.True( settings.HasSeenCoachMark );
            store.Verify( service => service.SaveAsync( It.IsAny<CancellationToken>() ), seen ? Times.Never() : Times.Once() );

            var close = Assert.IsType<System.Windows.Controls.Button>( window.FindName( "CloseWindowButton" ) );
            Assert.Same( close, overlay.CloseButton );
            if(!seen) {
                var closePoint = close.TranslatePoint( new System.Windows.Point( close.ActualWidth / 2, close.ActualHeight / 2 ), overlay );
                var mask = WpfTestHelper.GetRequiredDescendant<System.Windows.Shapes.Path>( overlay );
                Assert.False( mask.Data.FillContains( closePoint ) );
                close.Focus();
                Assert.Same( close, System.Windows.Input.Keyboard.FocusedElement );
            }
            close.RaiseEvent( new System.Windows.RoutedEventArgs( System.Windows.Controls.Button.ClickEvent ) );
            Assert.False( window.IsVisible );
        }
        finally {
            window.Close();
        }
    }

    /// <summary>メイン画面の Tips が翻訳作成とアップロードのボタンを順に強調表示することを検証する。</summary>
    /// <returns>検証の完了を表すタスク。</returns>
    [WpfFact]
    public async Task MainViewのTipsは翻訳とUploadのボタンを順に強調表示する() {
        EnsureViewResources();
        var store = new Mock<IAppSettingsService>();
        store.SetupGet( service => service.Settings ).Returns( new AppSettings
        {
            DcsWorldInstallDir = Path.GetTempPath(),
            SourceUserMissionDir = Path.GetTempPath(),
            TranslateFileDir = Path.GetTempPath(),
        } );
        store.Setup( service => service.SaveAsync( It.IsAny<CancellationToken>() ) ).Returns( Task.CompletedTask );
        var coach = new CoachMarkService( store.Object );
        await coach.StartAsync();
        await coach.NextAsync();
        await coach.NextAsync();
        await coach.NextAsync();
        await coach.NextAsync();
        coach.UpdateSelection( true );
        coach.BeginApply();
        coach.RecordApplyResult( true, false );
        coach.ApplyFinished( true );
        await coach.NextAsync();
        await coach.NextAsync();

        var main = new MainView();
        var overlay = new CoachMarkOverlay();
        overlay.SetBinding( CoachMarkOverlay.TargetProperty,
            new System.Windows.Data.Binding( nameof( coach.Target ) ) { Source = coach } );
        var root = new System.Windows.Controls.Grid();
        root.Children.Add( new System.Windows.Controls.Frame { Content = main } );
        root.Children.Add( overlay );
        var window = new System.Windows.Window { Content = root, Width = 900, Height = 600, ShowInTaskbar = false };
        try {
            window.Show();
            foreach(var id in new[] { "OpenTranslation", "OpenUpload" }) {
                WpfTestHelper.PumpDispatcher();
                window.UpdateLayout();
                Assert.Equal( id, coach.Target );
                var button = ((System.Windows.Controls.Grid)main.FindName( "ButtonsGrid" )).Children
                    .OfType<System.Windows.Controls.Button>().Single( element => CoachMarkHighlight.GetTargetId( element ) == id );
                var point = button.TranslatePoint( new System.Windows.Point( button.ActualWidth / 2, button.ActualHeight / 2 ), overlay );
                var mask = WpfTestHelper.GetRequiredDescendant<System.Windows.Shapes.Path>( overlay );
                Assert.False( mask.Data.FillContains( point ) );
                await coach.NextAsync();
            }
            Assert.Equal( CoachMarkStep.Completed, coach.Step );
        }
        finally {
            window.Close();
        }
    }

    /// <summary>画面表示の検証に必要なテーマとスタイルをアプリケーションに読み込む。</summary>
    private static void EnsureViewResources() {
        WpfTestHelper.EnsureApplicationResources();
        var application = System.Windows.Application.Current;
        Assert.NotNull( application );
        application.Resources.MergedDictionaries.Clear();
        var theme = (System.Windows.ResourceDictionary)System.Windows.Markup.XamlReader.Parse(
            "<md:BundledTheme xmlns:md=\"clr-namespace:MaterialDesignThemes.Wpf;assembly=MaterialDesignThemes.Wpf\" BaseTheme=\"Light\" PrimaryColor=\"DeepPurple\" SecondaryColor=\"Lime\" />" );
        application.Resources.MergedDictionaries.Insert( 0, theme );
        WpfTestHelper.EnsureApplicationResources(
            "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesign3.Defaults.xaml",
            "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Button.xaml",
            "pack://application:,,,/DcsTranslationTool.Presentation.Wpf;component/Assets/Styles/CustomBrushes.xaml",
            "pack://application:,,,/DcsTranslationTool.Presentation.Wpf;component/Assets/Styles/_Thickness.xaml",
            "pack://application:,,,/DcsTranslationTool.Presentation.Wpf;component/Assets/Styles/WindowStyle.xaml",
            "pack://application:,,,/DcsTranslationTool.Presentation.Wpf;component/Assets/Styles/ButtonStyle.xaml",
            "pack://application:,,,/DcsTranslationTool.Presentation.Wpf;component/Assets/Styles/TreeViewStyle.xaml" );
    }
}