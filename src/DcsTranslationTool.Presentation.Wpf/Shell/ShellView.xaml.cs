using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Navigation;
using System.Windows.Shell;

namespace DcsTranslationTool.Presentation.Wpf.Shell;
/// <summary>
/// MainView.xaml の相互作用ロジック
/// </summary>
public partial class ShellView : Window {
    /// <summary>メインウィンドウの画面要素を初期化する。</summary>
    public ShellView() {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    /// <summary>閉じるボタンの操作でメインウィンドウを閉じる。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">イベントの情報。</param>
    private void CloseWindowButton_Click( object sender, RoutedEventArgs e ) => Close();

    /// <summary>ドラッグ量に応じて操作案内ダイアログを移動する。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">イベントの情報。</param>
    private void CoachMarkDialog_DragDelta( object sender, DragDeltaEventArgs e ) {
        CoachOverlay.MoveDialog( e.HorizontalChange, e.VerticalChange );
        e.Handled = true;
    }

    /// <summary>
    /// 初回表示後に Shell の最小サイズを同期する。
    /// </summary>
    private void OnLoaded( object sender, RoutedEventArgs e ) => UpdateMinimumShellSize();

    /// <summary>
    /// ページ遷移後に Shell の最小サイズを再同期する。
    /// </summary>
    private void RootFrame_Navigated( object sender, NavigationEventArgs e ) => UpdateMinimumShellSize();

    /// <summary>
    /// 現在のレイアウト要求に基づいて Shell の最小サイズを更新する。
    /// </summary>
    private void UpdateMinimumShellSize() {
        var desiredSize = MeasureMinimumContentSize( RootLayout );
        var windowChrome = WindowChrome.GetWindowChrome( this );
        var resizeBorderThickness = windowChrome?.ResizeBorderThickness ?? default;
        var currentWindowSize = new Size(
            Math.Max( ActualWidth, Width ),
            Math.Max( ActualHeight, Height ) );
        var minimumShellSize = CalculateMinimumShellSize( desiredSize, currentWindowSize, resizeBorderThickness );

        MinWidth = minimumShellSize.Width;
        MinHeight = minimumShellSize.Height;
    }

    /// <summary>
    /// 要素が必要とする最小コンテンツサイズを計測する。
    /// </summary>
    /// <param name="element">計測対象の要素。</param>
    /// <returns>無制約計測で得られた最小要求サイズ。</returns>
    internal static Size MeasureMinimumContentSize( FrameworkElement element ) {
        ArgumentNullException.ThrowIfNull( element );

        var restoreSize = new Size(
            Math.Max( element.ActualWidth, 0 ),
            Math.Max( element.ActualHeight, 0 ) );

        element.UpdateLayout();
        element.Measure( new Size( double.PositiveInfinity, double.PositiveInfinity ) );
        var desiredSize = element.DesiredSize;

        if(restoreSize.Width > 0 || restoreSize.Height > 0) {
            element.Measure( restoreSize );
        }

        return desiredSize;
    }

    /// <summary>
    /// Shell の最小サイズを現在サイズを上限として算出する。
    /// </summary>
    /// <param name="desiredContentSize">コンテンツが要求するサイズ。</param>
    /// <param name="currentWindowSize">現在のウィンドウサイズ。</param>
    /// <param name="resizeBorderThickness">リサイズ枠の厚み。</param>
    /// <returns>ウィンドウへ適用する最小サイズ。</returns>
    internal static Size CalculateMinimumShellSize( Size desiredContentSize, Size currentWindowSize, Thickness resizeBorderThickness ) {
        var desiredWindowWidth = Math.Max( 0, desiredContentSize.Width )
            + resizeBorderThickness.Left
            + resizeBorderThickness.Right;
        var desiredWindowHeight = Math.Max( 0, desiredContentSize.Height )
            + resizeBorderThickness.Top
            + resizeBorderThickness.Bottom;

        var boundedWindowWidth = currentWindowSize.Width > 0
            ? Math.Min( desiredWindowWidth, currentWindowSize.Width )
            : desiredWindowWidth;
        var boundedWindowHeight = currentWindowSize.Height > 0
            ? Math.Min( desiredWindowHeight, currentWindowSize.Height )
            : desiredWindowHeight;

        return new Size( boundedWindowWidth, boundedWindowHeight );
    }
}