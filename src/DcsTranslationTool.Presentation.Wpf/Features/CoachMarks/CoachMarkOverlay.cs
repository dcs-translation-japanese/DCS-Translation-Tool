using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using Path = System.Windows.Shapes.Path;

namespace DcsTranslationTool.Presentation.Wpf.Features.CoachMarks;

/// <summary>操作対象を切り抜いた暗幕と、画面を押し縮めない案内ダイアログを表示する。</summary>
public sealed class CoachMarkOverlay : Canvas {
    /// <summary>現在の案内対象の識別子。</summary>
    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(
        nameof( Target ), typeof( string ), typeof( CoachMarkOverlay ),
        new FrameworkPropertyMetadata( string.Empty, FrameworkPropertyMetadataOptions.AffectsArrange ) );

    /// <summary>案内中も常に操作可能にするウィンドウの閉じるボタン。</summary>
    public static readonly DependencyProperty CloseButtonProperty = DependencyProperty.Register(
        nameof( CloseButton ), typeof( FrameworkElement ), typeof( CoachMarkOverlay ),
        new FrameworkPropertyMetadata( null, FrameworkPropertyMetadataOptions.AffectsArrange ) );

    /// <summary>案内対象と閉じるボタン以外の操作を遮る暗幕。</summary>
    private readonly Path _mask = new() { Fill = new SolidColorBrush( Color.FromArgb( 176, 0, 0, 0 ) ) };
    /// <summary>現在の案内対象として切り抜く要素。</summary>
    private FrameworkElement? _target;
    /// <summary>キーボードフォーカスの移動を監視する親要素。</summary>
    private FrameworkElement? _root;
    /// <summary>フォーカス移動処理の再入を防ぐフラグ。</summary>
    private bool _movingFocus;
    /// <summary>ドラッグ操作で指定した案内ダイアログの位置。</summary>
    private Point? _dialogPosition;
    /// <summary>前回の描画で暗幕から切り抜いた案内対象の範囲。</summary>
    private Rect _lastHole = Rect.Empty;
    /// <summary>前回の描画で暗幕から切り抜いた閉じるボタンの範囲。</summary>
    private Rect _lastCloseHole = Rect.Empty;
    /// <summary>前回の暗幕描画時のオーバーレイサイズ。</summary>
    private Size _lastSize;

    /// <summary>暗幕を最背面に置き、スクロールやサイズ変更に追従する。</summary>
    public CoachMarkOverlay() {
        Children.Add( _mask );
        ClipToBounds = true;
        LayoutUpdated += OnLayoutUpdated;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += ( _, e ) => { if(!(bool)e.NewValue) _dialogPosition = null; };
    }

    /// <summary>切り抜く要素の識別子を取得または設定する。</summary>
    public string Target {
        get => (string)GetValue( TargetProperty );
        set => SetValue( TargetProperty, value );
    }

    /// <summary>暗幕から除外し、キーボード操作も許可する閉じるボタン。</summary>
    public FrameworkElement? CloseButton {
        get => (FrameworkElement?)GetValue( CloseButtonProperty );
        set => SetValue( CloseButtonProperty, value );
    }

    /// <summary>案内ダイアログを移動し、ウィンドウ内に収めた位置を保持する。</summary>
    public void MoveDialog( double horizontalChange, double verticalChange ) {
        if(Children.Count < 2 || Children[1] is not FrameworkElement dialog) return;
        _dialogPosition = new Point( GetLeft( dialog ) + horizontalChange, GetTop( dialog ) + verticalChange );
        PlaceDialog( _lastHole );
    }

    /// <summary>レイアウト変更に合わせて案内対象、暗幕、ダイアログ位置とフォーカスを更新する。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">イベントの情報。</param>
    private void OnLayoutUpdated( object? sender, EventArgs e ) {
        if(!IsVisible || Parent is not FrameworkElement root) return;
        var target = FindTarget( root );
        if(!ReferenceEquals( _target, target )) {
            _target = target;
            if(target is not null) CoachMarkHighlight.ScrollIntoView( target );
        }

        var hole = UpdateMask( root, target );
        PlaceDialog( hole );
        if(root.IsKeyboardFocusWithin && !IsAllowedFocus( Keyboard.FocusedElement as DependencyObject )) FocusTargetOrDialog();
    }

    /// <summary>案内対象と閉じるボタンの表示範囲を切り抜き、必要な場合だけ暗幕を再描画する。</summary>
    /// <param name="root">案内対象の座標変換に使用する親要素。</param>
    /// <param name="target">切り抜く案内対象。対象がない場合は null。</param>
    /// <returns>暗幕から切り抜いた案内対象の範囲。対象が表示されていない場合は空の範囲。</returns>
    private Rect UpdateMask( FrameworkElement root, FrameworkElement? target ) {
        var bounds = new Rect( RenderSize );
        var hole = Rect.Empty;
        if(target is not null && target.IsVisible && target.ActualWidth > 0 && target.ActualHeight > 0) {
            hole = target.TransformToAncestor( root ).TransformBounds( new Rect( target.RenderSize ) );
            if(target is not CheckBox) hole.Inflate( 4, 4 );
            for(var parent = VisualTreeHelper.GetParent( target ); parent is FrameworkElement ancestor && ancestor != root;
                parent = VisualTreeHelper.GetParent( ancestor )) {
                if(ancestor.ClipToBounds) {
                    hole.Intersect( ancestor.TransformToAncestor( root ).TransformBounds( new Rect( ancestor.RenderSize ) ) );
                }
            }
            hole.Intersect( bounds );
            if(hole.IsEmpty || hole.Width == 0 || hole.Height == 0) {
                hole = Rect.Empty;
                CoachMarkHighlight.ScrollIntoView( target );
            }
        }

        var closeHole = Rect.Empty;
        if(CloseButton is { IsVisible: true } close && root.IsAncestorOf( close )) {
            closeHole = close.TransformToAncestor( root ).TransformBounds( new Rect( close.RenderSize ) );
            closeHole.Intersect( bounds );
        }
        if(_mask.Data is null || _lastHole != hole || _lastCloseHole != closeHole || _lastSize != RenderSize) {
            var cutouts = new GeometryGroup { FillRule = FillRule.Nonzero };
            if(!hole.IsEmpty) cutouts.Children.Add( new RectangleGeometry( hole, 6, 6 ) );
            if(!closeHole.IsEmpty) cutouts.Children.Add( new RectangleGeometry( closeHole ) );
            _mask.Data = new CombinedGeometry( GeometryCombineMode.Exclude, new RectangleGeometry( bounds ), cutouts );
            _lastHole = hole;
            _lastCloseHole = closeHole;
            _lastSize = RenderSize;
        }
        return hole;
    }

    /// <summary>案内対象を基準にダイアログを配置し、閉じるボタンを避けて画面内に収める。</summary>
    /// <param name="hole">暗幕から切り抜いた案内対象の範囲。</param>
    private void PlaceDialog( Rect hole ) {
        if(Children.Count < 2 || Children[1] is not FrameworkElement dialog) return;
        dialog.MaxWidth = Math.Max( 0, ActualWidth - 32 );
        // ダイアログの影も閉じるボタンへ重ならないようにする。
        var topMargin = _lastCloseHole.IsEmpty ? 16 : Math.Max( 16, _lastCloseHole.Bottom + 24 );
        var availableHeight = Math.Max( 0, ActualHeight - topMargin - 16 );
        var overlapsTarget = !hole.IsEmpty
            && hole.Height > ActualHeight / 2 && hole.Width > ActualWidth / 2;
        dialog.MaxHeight = overlapsTarget ? availableHeight * 0.6 : availableHeight;
        var size = dialog.RenderSize;
        var left = Math.Max( 16, (ActualWidth - size.Width) / 2 );
        var top = Math.Max( topMargin, (ActualHeight - size.Height) / 2 );
        if(!hole.IsEmpty) {
            left = Math.Clamp( hole.Left, 16, Math.Max( 16, ActualWidth - size.Width - 16 ) );
            if(hole.Bottom + 16 + size.Height <= ActualHeight - 16) top = hole.Bottom + 16;
            else if(hole.Top - 16 - size.Height >= topMargin) top = hole.Top - 16 - size.Height;
            else {
                top = Math.Clamp( hole.Top, topMargin, Math.Max( topMargin, ActualHeight - size.Height - 16 ) );
                if(hole.Right + 16 + size.Width <= ActualWidth - 16) left = hole.Right + 16;
                else if(hole.Left - size.Width - 16 >= 16) left = hole.Left - size.Width - 16;
                else {
                    left = Math.Max( 16, ActualWidth - size.Width - 16 );
                    top = Math.Max( topMargin, ActualHeight - size.Height - 16 );
                }
            }
        }
        if(_dialogPosition is { } position) {
            left = Math.Clamp( position.X, 16, Math.Max( 16, ActualWidth - size.Width - 16 ) );
            top = Math.Clamp( position.Y, topMargin, Math.Max( topMargin, ActualHeight - size.Height - 16 ) );
            _dialogPosition = new Point( left, top );
        }
        SetLeft( dialog, left );
        SetTop( dialog, Math.Clamp( top, topMargin, Math.Max( topMargin, ActualHeight - size.Height - 16 ) ) );
    }

    /// <summary>親要素のキーボードフォーカス移動イベントを購読する。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">イベントの情報。</param>
    private void OnLoaded( object sender, RoutedEventArgs e ) {
        _root = Parent as FrameworkElement;
        if(_root is not null) _root.PreviewGotKeyboardFocus += OnPreviewGotKeyboardFocus;
    }

    /// <summary>キーボードフォーカス移動イベントの購読を解除し、要素への参照を解放する。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">イベントの情報。</param>
    private void OnUnloaded( object sender, RoutedEventArgs e ) {
        if(_root is not null) _root.PreviewGotKeyboardFocus -= OnPreviewGotKeyboardFocus;
        _root = null;
        _target = null;
    }

    /// <summary>操作が許可されていない要素へのフォーカス移動を防ぐ。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">イベントの情報。</param>
    private void OnPreviewGotKeyboardFocus( object sender, KeyboardFocusChangedEventArgs e ) {
        if(!IsVisible || IsAllowedFocus( e.NewFocus as DependencyObject )) return;
        e.Handled = true;
        FocusTargetOrDialog();
    }

    /// <summary>指定した要素が案内対象、閉じるボタン、案内ダイアログのいずれかに属するか判定する。</summary>
    /// <param name="element">フォーカス移動先の要素。</param>
    /// <returns>移動先が操作を許可された範囲内であれば true。それ以外は false。</returns>
    private bool IsAllowedFocus( DependencyObject? element ) {
        while(element is not null) {
            if(ReferenceEquals( element, _target ) || ReferenceEquals( element, CloseButton ) || ReferenceEquals( element, this )) return true;
            element = element is Visual ? VisualTreeHelper.GetParent( element ) : LogicalTreeHelper.GetParent( element );
        }
        return false;
    }

    /// <summary>案内対象へのフォーカス移動を試み、移動できない場合は案内ダイアログ内へ移す。</summary>
    private void FocusTargetOrDialog() {
        if(_movingFocus) return;
        _movingFocus = true;
        try {
            if(_target is not null && (_target.Focus() || _target.MoveFocus( new TraversalRequest( FocusNavigationDirection.First ) ))) return;
            if(Children.Count > 1 && Children[1] is FrameworkElement dialog) {
                dialog.MoveFocus( new TraversalRequest( FocusNavigationDirection.First ) );
            }
        }
        finally {
            _movingFocus = false;
        }
    }

    /// <summary>オーバーレイ自身を除く表示中の子孫要素から、現在の案内対象を探す。</summary>
    /// <param name="parent">探索を開始する親要素。</param>
    /// <returns>識別子が一致する表示中の要素。見つからない場合は null。</returns>
    private FrameworkElement? FindTarget( DependencyObject parent ) {
        if(string.IsNullOrEmpty( Target )) return null;
        for(var i = 0; i < VisualTreeHelper.GetChildrenCount( parent ); i++) {
            var child = VisualTreeHelper.GetChild( parent, i );
            if(ReferenceEquals( child, this )) continue;
            if(child is FrameworkElement element && element.IsVisible && CoachMarkHighlight.GetTargetId( element ) == Target) return element;
            if(FindTarget( child ) is { } target) return target;
        }
        return null;
    }
}