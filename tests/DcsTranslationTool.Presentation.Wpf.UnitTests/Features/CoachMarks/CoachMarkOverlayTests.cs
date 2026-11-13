using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

using DcsTranslationTool.Presentation.Wpf.Features.CoachMarks;
using DcsTranslationTool.Presentation.Wpf.Features.Settings;
using DcsTranslationTool.Presentation.Wpf.Shell;
using DcsTranslationTool.TestCommon.Wpf;

using Path = System.Windows.Shapes.Path;

namespace DcsTranslationTool.Presentation.Wpf.UnitTests.Features.CoachMarks;

/// <summary>案内の暗幕と操作対象、説明ダイアログの配置を検証する。</summary>
[Collection( "WPF application resources" )]
public sealed class CoachMarkOverlayTests {
    /// <summary>暗幕が対象外の操作を遮り、案内ダイアログが対象に重ならないことを検証する。</summary>
    [WpfFact]
    public void 対象だけを切り抜き周囲の操作を遮りダイアログは対象に重ねない() {
        var target = new Button { Content = "対象", Width = 120, Height = 40 };
        CoachMarkHighlight.SetTargetId( target, "target" );
        var content = new Canvas();
        var other = new Button { Content = "対象外", Width = 100, Height = 32 };
        content.Children.Add( other );
        Canvas.SetLeft( target, 40 );
        Canvas.SetTop( target, 60 );
        content.Children.Add( target );
        var dialog = new Border { Width = 260, Height = 140, Background = Brushes.White };
        var overlay = new CoachMarkOverlay { Target = "target" };
        overlay.Children.Add( dialog );
        var root = new Grid();
        root.Children.Add( content );
        root.Children.Add( overlay );
        var window = new Window { Width = 640, Height = 480, Content = root, ShowInTaskbar = false };
        try {
            window.Show();
            window.UpdateLayout();
            var mask = Assert.IsType<Path>( overlay.Children[0] );
            var targetPoint = target.TranslatePoint( new Point( 60, 20 ), root );
            Assert.False( mask.Data.FillContains( targetPoint ) );
            Assert.True( mask.Data.FillContains( new Point( 10, 10 ) ) );
            Assert.Same( target, FindAncestor<Button>( root.InputHitTest( targetPoint ) as DependencyObject ) );
            Assert.Same( mask, root.InputHitTest( new Point( 10, 10 ) ) );
            target.Focus();
            other.Focus();
            Assert.Same( target, Keyboard.FocusedElement );
            var targetBounds = new Rect( target.TranslatePoint( new Point(), root ), target.RenderSize );
            var dialogBounds = new Rect( dialog.TranslatePoint( new Point(), root ), dialog.RenderSize );
            Assert.False( targetBounds.IntersectsWith( dialogBounds ) );
            Assert.True( new Rect( root.RenderSize ).Contains( dialogBounds ) );

            Canvas.SetTop( target, 260 );
            window.UpdateLayout();
            targetPoint = target.TranslatePoint( new Point( 60, 20 ), root );
            Assert.False( mask.Data.FillContains( targetPoint ) );
            Assert.True( mask.Data.FillContains( new Point( 100, 80 ) ) );

            overlay.Target = string.Empty;
            window.UpdateLayout();
            Assert.True( mask.Data.FillContains( targetPoint ) );

            overlay.Visibility = Visibility.Collapsed;
            window.UpdateLayout();
            Assert.Same( target, FindAncestor<Button>( root.InputHitTest( targetPoint ) as DependencyObject ) );
        }
        finally {
            window.Close();
        }
    }

    /// <summary>入力欄やページ遷移後の新しい案内対象に、暗幕の切り抜きが追従することを検証する。</summary>
    [WpfFact]
    public void 入力欄とページ遷移後の新しい対象にも切り抜きを追従する() {
        var target = new TextBox { Width = 150, Height = 32, Text = "設定値" };
        CoachMarkHighlight.SetTargetId( target, "directory" );
        var page = new ContentControl { Content = target };
        var overlay = new CoachMarkOverlay { Target = "directory" };
        overlay.Children.Add( new Border { Width = 200, Height = 100, Background = Brushes.White } );
        var root = new Grid();
        root.Children.Add( page );
        root.Children.Add( overlay );
        var window = new Window { Width = 640, Height = 480, Content = root, ShowInTaskbar = false };
        try {
            window.Show();
            window.UpdateLayout();
            var mask = Assert.IsType<Path>( overlay.Children[0] );
            Assert.False( mask.Data.FillContains( target.TranslatePoint( new Point( 75, 16 ), root ) ) );
            target.Text = "更新した設定値";
            Assert.Equal( "更新した設定値", target.Text );

            var replacement = new Button { Width = 100, Height = 40, VerticalAlignment = VerticalAlignment.Top };
            CoachMarkHighlight.SetTargetId( replacement, "apply" );
            overlay.Target = "apply";
            page.Content = replacement;
            window.UpdateLayout();
            Assert.False( mask.Data.FillContains( replacement.TranslatePoint( new Point( 50, 20 ), root ) ) );
            Assert.True( mask.Data.FillContains( new Point( 20, 20 ) ) );
        }
        finally {
            window.Close();
        }
    }

    /// <summary>小さい画面でも、翻訳選択のタブと左側の操作領域を案内ダイアログが塞がないことを検証する。</summary>
    [WpfFact]
    public void 小さい画面でも翻訳選択のタブと左側をダイアログで塞がない() {
        var tree = new TabControl { Margin = new Thickness( 16, 60, 16, 16 ) };
        tree.Items.Add( new TabItem { Header = "翻訳", Content = new TreeView() } );
        CoachMarkHighlight.SetTargetId( tree, "tree" );
        var overlay = new CoachMarkOverlay { Target = "tree" };
        var dialog = new Border { Width = 420, Height = 220, Background = Brushes.White };
        overlay.Children.Add( dialog );
        var root = new Grid();
        root.Children.Add( tree );
        root.Children.Add( overlay );
        var window = new Window { Width = 600, Height = 400, Content = root, ShowInTaskbar = false };
        try {
            window.Show();
            window.UpdateLayout();
            var dialogBounds = new Rect( dialog.TranslatePoint( new Point(), root ), dialog.RenderSize );
            Assert.True( new Rect( root.RenderSize ).Contains( dialogBounds ) );
            Assert.False( dialogBounds.Contains( tree.TranslatePoint( new Point( 30, 15 ), root ) ) );
            Assert.False( dialogBounds.Contains( tree.TranslatePoint( new Point( 30, 100 ), root ) ) );
            Assert.True( dialog.ActualHeight <= (root.ActualHeight - 32) * 0.6 );
            window.Width = 800;
            window.Height = 600;
            window.UpdateLayout();
            Assert.False( Assert.IsType<Path>( overlay.Children[0] ).Data.FillContains(
                tree.TranslatePoint( new Point( 30, 15 ), root ) ) );
        }
        finally {
            window.Close();
        }
    }

    /// <summary>どの案内段階でも閉じるボタンが暗幕から除外され、フォーカス移動とクリックが可能なことを検証する。</summary>
    [WpfFact]
    public void どの案内段階でも閉じるボタンを切り抜きフォーカスとクリックを妨げない() {
        var target = new Button { Width = 100, Height = 36 };
        CoachMarkHighlight.SetTargetId( target, "target" );
        var close = new Button
        {
            Content = "×",
            Width = 32,
            Height = 30,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top
        };
        CoachMarkHighlight.SetTargetId( close, "close" );
        var overlay = new CoachMarkOverlay { CloseButton = close };
        var dialog = new Border { Width = 260, Height = 140, Background = Brushes.White };
        overlay.Children.Add( dialog );
        var root = new Grid();
        root.Children.Add( target );
        root.Children.Add( close );
        root.Children.Add( overlay );
        var window = new Window { Width = 640, Height = 480, Content = root, ShowInTaskbar = false };
        try {
            window.Show();
            foreach(var targetId in new[] { "target", "close", "missing", string.Empty }) {
                overlay.Target = targetId;
                window.UpdateLayout();
                var point = close.TranslatePoint( new Point( 16, 15 ), root );
                Assert.False( Assert.IsType<Path>( overlay.Children[0] ).Data.FillContains( point ) );
                Assert.Same( close, FindAncestor<Button>( root.InputHitTest( point ) as DependencyObject ) );
                close.Focus();
                Assert.Same( close, Keyboard.FocusedElement );
                overlay.MoveDialog( 10000, -10000 );
                window.UpdateLayout();
                Assert.True( Canvas.GetTop( dialog ) >= close.ActualHeight );
            }
            window.Width = 500;
            window.UpdateLayout();
            Assert.False( Assert.IsType<Path>( overlay.Children[0] ).Data.FillContains(
                close.TranslatePoint( new Point( 16, 15 ), root ) ) );
        }
        finally {
            window.Close();
        }
    }

    /// <summary>ドラッグした位置を保持し、画面サイズが変わっても案内ダイアログが画面内に収まることを検証する。</summary>
    [WpfFact]
    public void ドラッグした位置を維持しサイズ変更でもダイアログを画面内に収める() {
        var dialog = new Border { Width = 260, Height = 140, Background = Brushes.White };
        var overlay = new CoachMarkOverlay();
        overlay.Children.Add( dialog );
        var root = new Grid();
        root.Children.Add( overlay );
        var window = new Window { Width = 640, Height = 480, Content = root, ShowInTaskbar = false };
        try {
            window.Show();
            window.UpdateLayout();
            var original = new Point( Canvas.GetLeft( dialog ), Canvas.GetTop( dialog ) );
            overlay.MoveDialog( 60, 40 );
            window.UpdateLayout();
            Assert.Equal( original.X + 60, Canvas.GetLeft( dialog ), 2 );
            Assert.Equal( original.Y + 40, Canvas.GetTop( dialog ), 2 );
            overlay.Target = "next";
            window.UpdateLayout();
            Assert.Equal( original.X + 60, Canvas.GetLeft( dialog ), 2 );
            Assert.Equal( original.Y + 40, Canvas.GetTop( dialog ), 2 );
            window.Width = 420;
            window.Height = 300;
            window.UpdateLayout();
            Assert.True( new Rect( root.RenderSize ).Contains(
                new Rect( dialog.TranslatePoint( new Point(), root ), dialog.RenderSize ) ) );
            overlay.Visibility = Visibility.Collapsed;
            overlay.Visibility = Visibility.Visible;
            window.UpdateLayout();
            Assert.Equal( (root.ActualWidth - dialog.ActualWidth) / 2, Canvas.GetLeft( dialog ), 2 );
        }
        finally {
            window.Close();
        }
    }

    /// <summary>スクロールで案内対象が画面外へ移動しても、操作できる位置へ戻ることを検証する。</summary>
    [WpfFact]
    public void スクロールで対象を見失っても操作できる位置へ戻す() {
        var target = new TextBox { Height = 32, Margin = new Thickness( 0, 20, 0, 0 ) };
        CoachMarkHighlight.SetTargetId( target, "directory" );
        var content = new StackPanel();
        content.Children.Add( target );
        content.Children.Add( new Border { Height = 1200 } );
        var scroll = new ScrollViewer { Content = content };
        var overlay = new CoachMarkOverlay { Target = "directory" };
        overlay.Children.Add( new Border { Width = 220, Height = 100, Background = Brushes.White } );
        var root = new Grid();
        root.Children.Add( scroll );
        root.Children.Add( overlay );
        var window = new Window { Width = 640, Height = 480, Content = root, ShowInTaskbar = false };
        try {
            window.Show();
            window.UpdateLayout();
            scroll.ScrollToEnd();
            window.UpdateLayout();
            WpfTestHelper.PumpDispatcher();
            window.UpdateLayout();
            var point = target.TranslatePoint( new Point( 20, 16 ), root );
            Assert.True( new Rect( root.RenderSize ).Contains( point ) );
            Assert.False( Assert.IsType<Path>( overlay.Children[0] ).Data.FillContains( point ) );
        }
        finally {
            window.Close();
        }
    }

    /// <summary>実際の案内ダイアログが本文と操作ボタンを表示し、画面全体を占有しないことを検証する。</summary>
    [StaFact]
    public void 実際の案内ダイアログは本文と操作を表示し画面の高さを奪わない() {
        WpfTestHelper.EnsureApplicationResources();
        var application = System.Windows.Application.Current;
        Assert.NotNull( application );
        application.Resources.MergedDictionaries.Clear();
        var theme = (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(
            "<md:BundledTheme xmlns:md=\"clr-namespace:MaterialDesignThemes.Wpf;assembly=MaterialDesignThemes.Wpf\" BaseTheme=\"Light\" PrimaryColor=\"DeepPurple\" SecondaryColor=\"Lime\" />" );
        application.Resources.MergedDictionaries.Insert( 0, theme );
        WpfTestHelper.EnsureApplicationResources(
            "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesign3.Defaults.xaml",
            "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Button.xaml",
            "pack://application:,,,/DcsTranslationTool.Presentation.Wpf;component/Assets/Styles/CustomBrushes.xaml",
            "pack://application:,,,/DcsTranslationTool.Presentation.Wpf;component/Assets/Styles/_Thickness.xaml",
            "pack://application:,,,/DcsTranslationTool.Presentation.Wpf;component/Assets/Styles/WindowStyle.xaml",
            "pack://application:,,,/DcsTranslationTool.Presentation.Wpf;component/Assets/Styles/ButtonStyle.xaml" );
        var window = new ShellView
        {
            ShowInTaskbar = false,
            Width = 800,
            Height = 600,
            DataContext = new {
                CoachMarkTarget = string.Empty,
                CoachMarkTitle = "DCS World のフォルダーを設定",
                CoachMarkDescription = string.Concat( Enumerable.Repeat( "有効なフォルダーを指定して次へ進んでください。", 100 ) ),
                CanNextCoachMark = true,
                CoachMarks = new {
                    IsActive = true,
                    Target = string.Empty,
                    ErrorMessage = string.Empty,
                    NextButtonText = "次へ",
                },
            },
        };
        try {
            window.Show();
            WpfTestHelper.PumpDispatcher();
            window.UpdateLayout();
            var overlay = WpfTestHelper.GetRequiredDescendant<CoachMarkOverlay>( window );
            var close = Assert.IsType<Button>( window.FindName( "CloseWindowButton" ) );
            var titlebar = Assert.IsType<Grid>( Assert.IsType<StackPanel>( close.Parent ).Parent );
            var closeCenter = close.TranslatePoint( new Point( close.ActualWidth / 2, close.ActualHeight / 2 ), overlay );
            var titlebarBottom = titlebar.TranslatePoint( new Point( 0, titlebar.ActualHeight ), overlay ).Y;
            var mask = Assert.IsType<Path>( overlay.Children[0] );
            Assert.False( mask.Data.FillContains( closeCenter ) );
            Assert.True( mask.Data.FillContains( new Point( closeCenter.X, titlebarBottom + 1 ) ),
                $"TitlebarHeight={titlebar.ActualHeight}, CloseHeight={close.ActualHeight}" );
            var dialog = Assert.IsAssignableFrom<FrameworkElement>( overlay.Children[1] );
            Assert.True( double.IsNaN( dialog.Width ) );
            var scroll = WpfTestHelper.GetRequiredDescendant<ScrollViewer>( dialog );
            var description = WpfTestHelper.GetRequiredDescendant<TextBlock>( scroll );
            for(DependencyObject? element = description; element is not null && element != overlay;
                element = VisualTreeHelper.GetParent( element )) {
                if(element is UIElement visual) Assert.Null( visual.Effect );
            }
            Assert.True( scroll.ActualHeight > 0 );
            Assert.True( scroll.ScrollableHeight > 0,
                $"Dialog={dialog.RenderSize}, MaxHeight={dialog.MaxHeight}, Scroll={scroll.RenderSize}, Extent={scroll.ExtentHeight}, Viewport={scroll.ViewportHeight}" );
            var button = WpfTestHelper.GetRequiredDescendant<Button>( dialog );
            var root = Assert.IsType<Grid>( overlay.Parent );
            var dialogBounds = new Rect( dialog.TranslatePoint( new Point(), root ), dialog.RenderSize );
            Assert.True( new Rect( root.RenderSize ).Contains( dialogBounds ) );
            Assert.True( dialogBounds.Contains( button.TranslatePoint( new Point(), root ) ) );
            var captionPoint = root.PointToScreen( new Point( root.ActualWidth / 2, 15 ) );
            var captionCoordinates = ((int)captionPoint.Y << 16) | ((int)captionPoint.X & 0xffff);
            Assert.Equal( (nint)2, SendMessage( new System.Windows.Interop.WindowInteropHelper( window ).Handle,
                0x0084, 0, captionCoordinates ) );
            var handle = WpfTestHelper.GetRequiredDescendant<Thumb>( dialog );
            var originalLeft = Canvas.GetLeft( dialog );
            handle.RaiseEvent( new DragDeltaEventArgs( 50, 0 ) { RoutedEvent = Thumb.DragDeltaEvent } );
            window.UpdateLayout();
            var expectedLeft = Math.Clamp( originalLeft + 50, 16, Math.Max( 16, overlay.ActualWidth - dialog.ActualWidth - 16 ) );
            Assert.Equal( expectedLeft, Canvas.GetLeft( dialog ), 2 );
            handle.RaiseEvent( new DragDeltaEventArgs( 10000, 10000 ) { RoutedEvent = Thumb.DragDeltaEvent } );
            window.UpdateLayout();
            Assert.True( new Rect( root.RenderSize ).Contains(
                new Rect( dialog.TranslatePoint( new Point(), root ), dialog.RenderSize ) ) );
            var frame = WpfTestHelper.GetRequiredDescendant<Frame>( window );
            var settings = new SettingsView();
            frame.Content = settings;
            foreach(var (targetId, headingName) in new[] {
                ("DcsWorldInstall", "DcsWorldInstallHeading"),
                ("UserMission", "UserMissionHeading"),
                ("TranslateFile", "TranslateFileHeading"),
            }) {
                overlay.Target = targetId;
                WpfTestHelper.PumpDispatcher();
                window.UpdateLayout();
                var heading = Assert.IsType<TextBlock>( settings.FindName( headingName ) );
                var settingsScroll = WpfTestHelper.GetRequiredDescendant<ScrollViewer>( settings );
                var picker = Assert.IsAssignableFrom<Panel>( heading.Parent ).Children.OfType<FrameworkElement>()
                    .Single( element => CoachMarkHighlight.GetTargetId( element ) == targetId );
                CoachMarkHighlight.SetCurrentTarget( picker, targetId );
                picker.MoveFocus( new TraversalRequest( FocusNavigationDirection.First ) );
                WpfTestHelper.PumpDispatcher();
                window.UpdateLayout();
                var headingTop = heading.TranslatePoint( new Point(), settingsScroll ).Y;
                Assert.True( headingTop >= 0 && headingTop <= heading.ActualHeight,
                    $"Target={targetId}, HeadingTop={headingTop}, Offset={settingsScroll.VerticalOffset}, MaxOffset={settingsScroll.ScrollableHeight}" );
                Assert.True( picker.TranslatePoint( new Point( 0, picker.ActualHeight ), settingsScroll ).Y <= settingsScroll.ActualHeight );
            }
            var contentHeight = frame.ActualHeight;
            overlay.Visibility = Visibility.Collapsed;
            window.UpdateLayout();
            Assert.Equal( contentHeight, frame.ActualHeight );
        }
        finally {
            window.Close();
        }
    }

    /// <summary>ウィンドウへメッセージを同期送信し、タイトルバーのヒットテスト結果を取得する。</summary>
    /// <param name="window">メッセージの送信先ウィンドウのハンドル。</param>
    /// <param name="message">送信するメッセージの識別子。</param>
    /// <param name="wParam">メッセージ固有の追加情報。</param>
    /// <param name="lParam">メッセージ固有の追加情報。</param>
    /// <returns>ウィンドウでのメッセージ処理の結果。</returns>
    [System.Runtime.InteropServices.DllImport( "user32.dll" )]
    private static extern nint SendMessage( nint window, uint message, nint wParam, nint lParam );

    /// <summary>指定した要素から親をたどり、指定した型に一致する最初の要素を探す。</summary>
    /// <typeparam name="T">検索する要素の型。</typeparam>
    /// <param name="element">探索を開始する要素。</param>
    /// <returns>指定した型に一致する要素。見つからない場合は null。</returns>
    private static T? FindAncestor<T>( DependencyObject? element ) where T : DependencyObject {
        while(element is not null) {
            if(element is T match) return match;
            element = VisualTreeHelper.GetParent( element );
        }
        return null;
    }
}