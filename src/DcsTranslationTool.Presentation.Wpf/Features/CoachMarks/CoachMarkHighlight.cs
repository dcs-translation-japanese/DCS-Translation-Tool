using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace DcsTranslationTool.Presentation.Wpf.Features.CoachMarks;

/// <summary>操作を妨げず、現在の案内対象を強調する。</summary>
public static class CoachMarkHighlight {
    /// <summary>要素に案内対象の識別子を割り当てる添付プロパティ。</summary>
    public static readonly DependencyProperty TargetIdProperty = DependencyProperty.RegisterAttached(
        "TargetId", typeof( string ), typeof( CoachMarkHighlight ), new PropertyMetadata( null, OnTargetIdChanged ) );
    /// <summary>現在強調する案内対象の識別子を保持する添付プロパティ。</summary>
    public static readonly DependencyProperty CurrentTargetProperty = DependencyProperty.RegisterAttached(
        "CurrentTarget", typeof( string ), typeof( CoachMarkHighlight ), new PropertyMetadata( null, OnCurrentTargetChanged ) );
    /// <summary>スクロール時に画面上端へ合わせる見出し要素。</summary>
    public static readonly DependencyProperty ScrollAnchorProperty = DependencyProperty.RegisterAttached(
        "ScrollAnchor", typeof( FrameworkElement ), typeof( CoachMarkHighlight ), new PropertyMetadata( null ) );
    /// <summary>要素に関連付けた強調枠を保持する添付プロパティ。</summary>
    private static readonly DependencyProperty HighlightProperty = DependencyProperty.RegisterAttached(
        "Highlight", typeof( HighlightAdorner ), typeof( CoachMarkHighlight ) );

    /// <summary>
    /// 要素に操作案内の対象識別子を設定する。
    /// </summary>
    /// <param name="element">操作案内の対象となる要素。</param>
    /// <param name="value">要素に割り当てる案内対象の識別子。</param>
    public static void SetTargetId( DependencyObject element, string value ) => element.SetValue( TargetIdProperty, value );
    /// <summary>
    /// 要素に設定されている操作案内の対象識別子を取得する。
    /// </summary>
    /// <param name="element">操作案内の対象となる要素。</param>
    /// <returns>要素に割り当てられた識別子。未設定の場合は null。</returns>
    public static string? GetTargetId( DependencyObject element ) => (string?)element.GetValue( TargetIdProperty );
    /// <summary>
    /// 現在の案内対象の識別子を要素に設定する。
    /// </summary>
    /// <param name="element">操作案内の対象となる要素。</param>
    /// <param name="value">現在の案内対象を識別する値。</param>
    public static void SetCurrentTarget( DependencyObject element, string value ) => element.SetValue( CurrentTargetProperty, value );
    /// <summary>
    /// 要素が参照する現在の案内対象の識別子を取得する。
    /// </summary>
    /// <param name="element">操作案内の対象となる要素。</param>
    /// <returns>現在の案内対象の識別子。未設定の場合は null。</returns>
    public static string? GetCurrentTarget( DependencyObject element ) => (string?)element.GetValue( CurrentTargetProperty );

    /// <summary>案内対象に対応する見出しを設定する。</summary>
    public static void SetScrollAnchor( DependencyObject element, FrameworkElement value ) => element.SetValue( ScrollAnchorProperty, value );

    /// <summary>案内対象に対応する見出しを取得する。</summary>
    public static FrameworkElement? GetScrollAnchor( DependencyObject element ) => (FrameworkElement?)element.GetValue( ScrollAnchorProperty );

    /// <summary>設定の見出しをスクロール領域の上端に揃え、それ以外は対象を表示領域へ移す。</summary>
    internal static void ScrollIntoView( FrameworkElement element ) {
        if(GetScrollAnchor( element ) is not { } anchor) {
            element.BringIntoView();
            return;
        }
        anchor.BringIntoView();
        for(var parent = VisualTreeHelper.GetParent( anchor ); parent is not null; parent = VisualTreeHelper.GetParent( parent )) {
            if(parent is not ScrollViewer scroll) continue;
            scroll.ScrollToVerticalOffset( scroll.VerticalOffset + anchor.TranslatePoint( new Point(), scroll ).Y );
            break;
        }
    }

    /// <summary>
    /// 対象識別子の変更時にライフサイクルイベントを購読し、強調表示を更新する。
    /// </summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">イベントの情報。</param>
    private static void OnTargetIdChanged( DependencyObject sender, DependencyPropertyChangedEventArgs e ) {
        if(sender is not FrameworkElement element) return;
        element.Loaded -= OnLoaded;
        element.Unloaded -= OnUnloaded;
        element.Loaded += OnLoaded;
        element.Unloaded += OnUnloaded;
        Update( element );
    }

    /// <summary>
    /// 現在の案内対象が変わったときに強調表示を更新する。
    /// </summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">イベントの情報。</param>
    private static void OnCurrentTargetChanged( DependencyObject sender, DependencyPropertyChangedEventArgs e ) {
        if(sender is FrameworkElement element) Update( element );
    }

    /// <summary>
    /// 読み込まれた要素が案内対象であれば強調枠を表示する。
    /// </summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">イベントの情報。</param>
    private static void OnLoaded( object sender, RoutedEventArgs e ) => Update( (FrameworkElement)sender );
    /// <summary>
    /// 要素のアンロード時に強調枠を取り除く。
    /// </summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">イベントの情報。</param>
    private static void OnUnloaded( object sender, RoutedEventArgs e ) => Remove( (FrameworkElement)sender );

    /// <summary>
    /// 現在の案内対象を表示領域に移動し、強調枠を付け直す。
    /// </summary>
    /// <param name="element">操作案内の対象となる要素。</param>
    private static void Update( FrameworkElement element ) {
        Remove( element );
        if(!element.IsLoaded || string.IsNullOrEmpty( GetTargetId( element ) ) || GetTargetId( element ) != GetCurrentTarget( element )) return;
        var layer = AdornerLayer.GetAdornerLayer( element );
        if(layer is null) return;
        ScrollIntoView( element );
        var highlight = new HighlightAdorner( element );
        element.SetValue( HighlightProperty, highlight );
        layer.Add( highlight );
    }

    /// <summary>
    /// 要素に関連付けた強調枠を装飾レイヤーから取り除く。
    /// </summary>
    /// <param name="element">操作案内の対象となる要素。</param>
    private static void Remove( FrameworkElement element ) {
        if(element.GetValue( HighlightProperty ) is not HighlightAdorner highlight) return;
        if(VisualTreeHelper.GetParent( highlight ) is AdornerLayer layer) layer.Remove( highlight );
        element.ClearValue( HighlightProperty );
    }

    /// <summary>
    /// 入力操作を妨げずに案内対象の外周を強調する装飾を表す。
    /// </summary>
    private sealed class HighlightAdorner : Adorner {
        /// <summary>
        /// 対象要素に、ヒットテストを行わない強調枠を関連付ける。
        /// </summary>
        /// <param name="element">操作案内の対象となる要素。</param>
        public HighlightAdorner( FrameworkElement element ) : base( element ) {
            IsHitTestVisible = false;
            IsEnabled = false;
        }

        /// <summary>
        /// 案内対象の外周にシステムの強調色で枠を描画する。
        /// </summary>
        /// <param name="drawingContext">強調枠の描画に使用するコンテキスト。</param>
        protected override void OnRender( DrawingContext drawingContext ) {
            var bounds = new Rect( AdornedElement.RenderSize );
            bounds.Inflate( 3, 3 );
            drawingContext.DrawRoundedRectangle( null, new Pen( SystemColors.HighlightBrush, 3 ), bounds, 5, 5 );
        }
    }
}