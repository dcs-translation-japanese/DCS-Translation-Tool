using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using DcsTranslationTool.Presentation.Wpf.Features.CoachMarks;
using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;

namespace DcsTranslationTool.Presentation.Wpf.Features.Download;
/// <summary>
/// DownloadView.xaml の相互作用ロジック
/// </summary>
public partial class DownloadView : Page {
    /// <summary>操作案内のために一時的に表示した、通常は非表示のエントリー。</summary>
    private IFileEntryViewModel? _revealedEntry;

    /// <summary>画面を初期化し、案内対象の表示と非表示状態の復元に必要なイベントを登録する。</summary>
    public DownloadView() {
        InitializeComponent();
        LayoutUpdated += OnLayoutUpdated;
        Unloaded += ( _, _ ) => RestoreHiddenEntry();
    }

    /// <summary>翻訳選択の案内対象を含むタブを選び、非表示や仮想化で未表示の行を表示する。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">イベントの情報。</param>
    private void OnLayoutUpdated( object? sender, EventArgs e ) {
        if(!IsLoaded || DataContext is not DownloadViewModel vm
            || vm.CoachMarks is not { Step: CoachMarkStep.SelectTranslation } coach) {
            RestoreHiddenEntry();
            return;
        }
        var tab = vm.Tabs.FirstOrDefault( tab => tab.Root.Children.Any( entry => entry.Path == coach.Target ) );
        var entry = tab?.Root.Children.FirstOrDefault( entry => entry.Path == coach.Target );
        if(!ReferenceEquals( entry, _revealedEntry )) RestoreHiddenEntry();
        if(tab is null || entry is null) return;
        if(!entry.IsVisible) {
            _revealedEntry = entry;
            entry.IsVisible = true;
        }
        var tabIndex = vm.Tabs.IndexOf( tab );
        if(vm.SelectedTabIndex != tabIndex) {
            vm.SelectedTabIndex = tabIndex;
            return;
        }
        var tree = FindVisualChild<TreeView>( this );
        if(tree is null || tree.ItemContainerGenerator.ContainerFromItem( entry ) is not null) return;
        var index = tree.Items.IndexOf( entry );
        // 仮想化で未生成の行を表示し、暗幕がチェックボックスを見つけられるようにする。
        if(index >= 0) FindVisualChild<VirtualizingPanel>( tree )?.BringIndexIntoViewPublic( index );
    }

    /// <summary>操作案内のために表示したエントリーを元の非表示状態に戻す。</summary>
    private void RestoreHiddenEntry() {
        if(_revealedEntry is null) return;
        _revealedEntry.IsVisible = false;
        _revealedEntry = null;
    }

    /// <summary>ビジュアルツリーの子孫から、指定した型に一致する最初の要素を探す。</summary>
    /// <typeparam name="T">検索する要素の型。</typeparam>
    /// <param name="parent">探索を開始する親要素。</param>
    /// <returns>指定した型の最初の子孫要素。見つからない場合は null。</returns>
    private static T? FindVisualChild<T>( DependencyObject parent ) where T : DependencyObject {
        for(var i = 0; i < VisualTreeHelper.GetChildrenCount( parent ); i++) {
            var child = VisualTreeHelper.GetChild( parent, i );
            if(child is T match) return match;
            if(FindVisualChild<T>( child ) is { } descendant) return descendant;
        }
        return null;
    }
}