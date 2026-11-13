using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Features.CoachMarks;
using DcsTranslationTool.Presentation.Wpf.Features.Common;
using DcsTranslationTool.Presentation.Wpf.Features.Download;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;
using DcsTranslationTool.Resources;
using DcsTranslationTool.Shared.Models;
using DcsTranslationTool.TestCommon.Wpf;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.UnitTests.Features.CoachMarks;

/// <summary>翻訳選択の操作案内で、対象の表示と入力制限を検証する。</summary>
[Collection( "WPF application resources" )]
public sealed class CoachMarkTranslationTargetTests {
    /// <summary>画面外にある TF-51D のチェックボックスを表示して操作を限定し、スキップ後に制限を解除することを検証する。</summary>
    /// <returns>検証の完了を表すタスク。</returns>
    [WpfFact]
    public async Task 翻訳選択では画面外のTF51Dチェックボックスだけを操作できスキップ後は制限を解除する() {
        var application = System.Windows.Application.Current ?? new System.Windows.Application
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown,
        };
        application.Resources.MergedDictionaries.Clear();
        application.Resources.MergedDictionaries.Add( (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(
            "<md:BundledTheme xmlns:md=\"clr-namespace:MaterialDesignThemes.Wpf;assembly=MaterialDesignThemes.Wpf\" BaseTheme=\"Light\" PrimaryColor=\"DeepPurple\" SecondaryColor=\"Lime\" />" ) );
        WpfTestHelper.EnsureApplicationResources(
            "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesign3.Defaults.xaml",
            "pack://application:,,,/DcsTranslationTool.Presentation.Wpf;component/Assets/Styles/CustomBrushes.xaml",
            "pack://application:,,,/DcsTranslationTool.Presentation.Wpf;component/Assets/Styles/_Thickness.xaml",
            "pack://application:,,,/DcsTranslationTool.Presentation.Wpf;component/Assets/Styles/TreeViewStyle.xaml" );
        var store = new Mock<IAppSettingsService>();
        store.SetupGet( x => x.Settings ).Returns( new AppSettings
        {
            DcsWorldInstallDir = System.IO.Path.GetTempPath(),
            SourceUserMissionDir = System.IO.Path.GetTempPath(),
            TranslateFileDir = System.IO.Path.GetTempPath(),
        } );
        store.Setup( x => x.SaveAsync() ).Returns( Task.CompletedTask );
        var coach = new CoachMarkService( store.Object );
        await coach.StartAsync();
        await coach.NextAsync();
        await coach.NextAsync();
        await coach.NextAsync();
        await coach.NextAsync();
        Assert.Equal( CoachMarkStep.SelectTranslation, coach.Step );
        const string targetPath = "DCSWorld/Mods/aircraft/TF-51D";
        Assert.Equal( targetPath, coach.Target );

        var logger = Mock.Of<ILoggingService>();
        var treeService = new FileEntryTreeService( logger );
        var entries = Enumerable.Range( 0, 60 ).Select( i =>
            new FileEntry( "dictionary", $"DCSWorld/Mods/aircraft/AA-{i:D2}/Test.miz/l10n/DEFAULT/dictionary", false, null, "repo" ) )
            .Append( new FileEntry( "dictionary", "UserMissions/TF-51D/Test.miz/l10n/DEFAULT/dictionary", false, null, "repo" ) )
            .ToList();
        var tabs = treeService.BuildTabs( [], entries, ChangeTypeMode.Download );
        var vm = new DownloadViewModel( Mock.Of<IApiService>(), store.Object, Mock.Of<IDialogService>(),
            Mock.Of<IDownloadWorkflowService>(), Mock.Of<IDispatcherService>(), Mock.Of<IFileEntryService>(),
            Mock.Of<IFileEntryWatcherLifecycle>(), treeService, logger, Mock.Of<ISnackbarService>(), Mock.Of<ISystemService>(), coach );
        foreach(var tab in tabs) vm.Tabs.Add( tab );
        vm.SelectedTabIndex = 2;
        var previouslySelected = vm.Tabs[0].Root.Children.First();
        previouslySelected.CheckState = true;
        vm.SelectedTabIndex = 0;
        Assert.Equal( CoachMarkStep.SelectTranslation, coach.Step );
        previouslySelected.CheckState = false;
        vm.SelectedTabIndex = 2;
        var page = new DownloadView { DataContext = vm };
        var overlay = new CoachMarkOverlay();
        overlay.SetBinding( CoachMarkOverlay.TargetProperty, new Binding( nameof( coach.Target ) ) { Source = coach } );
        overlay.SetBinding( UIElement.VisibilityProperty, new Binding( nameof( coach.IsActive ) )
        {
            Source = coach,
            Converter = new BooleanToVisibilityConverter(),
        } );
        overlay.Children.Add( new Button { Content = "スキップ", Width = 120, Height = 40 } );
        var root = new Grid();
        root.Children.Add( new Frame { Content = page, NavigationUIVisibility = System.Windows.Navigation.NavigationUIVisibility.Hidden } );
        root.Children.Add( overlay );
        var window = new Window { Content = root, Width = 800, Height = 480, ShowInTaskbar = false };
        try {
            window.Show();
            WpfTestHelper.PumpDispatcher();
            window.UpdateLayout();
            var mask = Assert.IsType<System.Windows.Shapes.Path>( overlay.Children[0] );
            Assert.True( mask.Data.FillContains( new Point( 10, 10 ) ) );
            Assert.DoesNotContain( Descendants( page ).OfType<CheckBox>(),
                checkbox => CoachMarkHighlight.GetTargetId( checkbox ) == targetPath );

            var withTarget = treeService.BuildTabs( [], entries.Append(
                new FileEntry( "dictionary", targetPath + "/Test.miz/l10n/DEFAULT/dictionary", false, null, "repo" ) ).ToList(), ChangeTypeMode.Download );
            var aircraft = withTarget.Single( tab => tab.TabType == CategoryType.Aircraft );
            var targetEntry = aircraft.Root.Children.Single( entry => entry.Path == targetPath );
            targetEntry.IsVisible = false;
            vm.Tabs[0].Root = aircraft.Root;
            WpfTestHelper.PumpDispatcher();
            window.UpdateLayout();
            WpfTestHelper.PumpDispatcher();
            window.UpdateLayout();

            Assert.Equal( 0, vm.SelectedTabIndex );
            var target = Assert.Single( Descendants( page ).OfType<CheckBox>(),
                checkbox => CoachMarkHighlight.GetTargetId( checkbox ) == targetPath );
            var point = target.TranslatePoint( new Point( target.ActualWidth / 2, target.ActualHeight / 2 ), root );
            Assert.True( new Rect( root.RenderSize ).Contains( point ) );
            Assert.False( mask.Data.FillContains( point ) );
            Assert.Contains( root.InputHitTest( point ) as DependencyObject, Descendants( target ).Append( target ) );
            var labelPoint = target.TranslatePoint( new Point( target.ActualWidth + 2, target.ActualHeight / 2 ), root );
            Assert.True( mask.Data.FillContains( labelPoint ) );
            Assert.Same( mask, root.InputHitTest( labelPoint ) );
            Assert.True( target.Focus() );
            var other = Descendants( page ).OfType<CheckBox>().First( checkbox => checkbox.DataContext == vm );
            Assert.False( other.Focus() );
            Assert.True( target.IsKeyboardFocused );
            var otherPoint = other.TranslatePoint( new Point( other.ActualWidth / 2, other.ActualHeight / 2 ), root );
            Assert.Same( mask, root.InputHitTest( otherPoint ) );
            target.SetCurrentValue( ToggleButton.IsCheckedProperty, true );
            Assert.True( targetEntry.CheckState );
            Assert.All( aircraft.Root.Children.Where( entry => entry != targetEntry ), entry => Assert.False( entry.CheckState ) );
            var tree = WpfTestHelper.GetRequiredDescendant<TreeView>( page );
            Assert.True( VirtualizingStackPanel.GetIsVirtualizing( tree ) );
            vm.SelectedTabIndex = 2;
            vm.SelectedTabIndex = 0;
            Assert.Equal( CoachMarkStep.ApplyTranslation, coach.Step );
            WpfTestHelper.PumpDispatcher();
            window.UpdateLayout();
            var apply = Descendants( page ).OfType<ActionButton>().Single( button =>
                CoachMarkHighlight.GetTargetId( button ) == "ApplyTranslation" );
            var applyPoint = apply.TranslatePoint( new Point( apply.ActualWidth / 2, apply.ActualHeight / 2 ), root );
            Assert.False( mask.Data.FillContains( applyPoint ) );
            Assert.Contains( root.InputHitTest( applyPoint ) as DependencyObject, Descendants( apply ).Append( apply ) );
            Assert.True( WpfTestHelper.GetRequiredDescendant<Button>( apply ).Focus() );
            var download = Descendants( page ).OfType<ActionButton>().Single( button =>
                button.Text == Strings_Download.DownloadButtonContent );
            var downloadPoint = download.TranslatePoint( new Point( download.ActualWidth / 2, download.ActualHeight / 2 ), root );
            Assert.Same( mask, root.InputHitTest( downloadPoint ) );
            Assert.False( WpfTestHelper.GetRequiredDescendant<Button>( download ).Focus() );

            coach.BeginApply();
            coach.RecordApplyResult( true, false );
            coach.ApplyFinished( true );
            await coach.NextAsync();
            Assert.Equal( CoachMarkStep.DownloadTip, coach.Step );
            WpfTestHelper.PumpDispatcher();
            window.UpdateLayout();
            Assert.False( mask.Data.FillContains( downloadPoint ) );
            Assert.Contains( root.InputHitTest( downloadPoint ) as DependencyObject, Descendants( download ).Append( download ) );

            coach.Skip();
            Assert.Equal( CoachMarkStep.Completed, coach.Step );
            Assert.True( coach.IsActive );
            Assert.Empty( coach.Target );
            await coach.NextAsync();
            WpfTestHelper.PumpDispatcher();
            window.UpdateLayout();
            Assert.Equal( Visibility.Collapsed, overlay.Visibility );
            Assert.False( targetEntry.IsVisible );
            Assert.True( other.Focus() );
        }
        finally {
            window.Close();
        }
    }

    /// <summary>ビジュアルツリーの子孫要素を深さ優先で列挙する。</summary>
    /// <param name="parent">探索を開始する親要素。</param>
    /// <returns>親要素自身を含まない子孫要素の列。</returns>
    private static IEnumerable<DependencyObject> Descendants( DependencyObject parent ) {
        for(var i = 0; i < VisualTreeHelper.GetChildrenCount( parent ); i++) {
            var child = VisualTreeHelper.GetChild( parent, i );
            yield return child;
            foreach(var descendant in Descendants( child )) yield return descendant;
        }
    }
}