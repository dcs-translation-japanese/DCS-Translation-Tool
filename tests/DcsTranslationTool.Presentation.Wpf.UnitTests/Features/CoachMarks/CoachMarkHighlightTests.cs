using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

using DcsTranslationTool.Presentation.Wpf.Features.CoachMarks;

namespace DcsTranslationTool.Presentation.Wpf.UnitTests.Features.CoachMarks;

/// <summary>
/// 実際の WPF ウィンドウで案内対象の強調表示と解除を検証する。
/// </summary>
public sealed class CoachMarkHighlightTests {
    /// <summary>
    /// 現在の対象だけを強調し操作を妨げずスキップやアンロードで解除することを検証する。
    /// </summary>
    [WpfFact]
    public void 現在の対象だけを強調し操作を妨げずスキップやアンロードで解除する() {
        var target = new TextBox { Text = "編集できる入力欄", Margin = new Thickness( 12 ) };
        CoachMarkHighlight.SetTargetId( target, "directory" );
        CoachMarkHighlight.SetCurrentTarget( target, "directory" );
        var window = new Window
        {
            Width = 400,
            Height = 200,
            ShowInTaskbar = false,
            Content = new AdornerDecorator { Child = target },
        };
        try {
            window.Show();
            window.UpdateLayout();
            var layer = AdornerLayer.GetAdornerLayer( target );
            Assert.NotNull( layer );
            var adorner = Assert.Single( layer.GetAdorners( target )! );
            Assert.False( adorner.IsHitTestVisible );
            Assert.True( target.IsEnabled );
            target.Text = "更新した値";
            Assert.Equal( "更新した値", target.Text );

            CoachMarkHighlight.SetCurrentTarget( target, "other" );
            Assert.Null( layer.GetAdorners( target ) );
            CoachMarkHighlight.SetCurrentTarget( target, "directory" );
            Assert.Single( layer.GetAdorners( target )! );
            window.Content = null;
            target.RaiseEvent( new RoutedEventArgs( FrameworkElement.UnloadedEvent ) );
            Assert.Null( layer.GetAdorners( target ) );
        }
        finally {
            window.Close();
        }
    }
}