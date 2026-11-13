using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Presentation.Wpf.ViewModels;
using DcsTranslationTool.Shared.Models;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.UnitTests.ViewModels;

/// <summary>
/// FileEntryViewModel の選択状態に関する動作を検証する。
/// </summary>
public sealed class FileEntryViewModelTests {
    /// <summary>
    /// 部分選択のまま子のチェックが変わっても祖先へ通知することを検証する。
    /// </summary>
    [Fact]
    public void 部分選択のまま子のチェックが変わっても祖先へ通知する() {
        var logger = Mock.Of<ILoggingService>();
        FileEntryViewModel Node( string name, bool directory ) => new(
            new LocalFileEntry( name, name, directory, "local" ), ChangeTypeMode.Download, logger );
        var root = Node( "UserMissions", true );
        var folder = Node( "Missions", true );
        var first = Node( "first.lua", false );
        var second = Node( "second.lua", false );
        var third = Node( "third.lua", false );
        folder.Children.Add( first );
        folder.Children.Add( second );
        folder.Children.Add( third );
        root.Children.Add( folder );
        first.CheckState = true;
        Assert.Null( root.CheckState );
        var notifications = 0;
        root.CheckStateChanged += ( _, _ ) => notifications++;

        second.CheckState = true;

        Assert.Null( folder.CheckState );
        Assert.Null( root.CheckState );
        Assert.True( notifications > 0 );
    }

    /// <summary>
    /// ディレクトリ選択時に子ノードへ選択状態を伝播しないことを検証する。
    /// </summary>
    [Fact]
    public void ディレクトリ選択時に子ノードへ選択状態を伝播しない() {
        var loggerMock = new Mock<ILoggingService>();
        var directoryNode = new FileEntryViewModel(
            new LocalFileEntry( "A10C", "A10C", true, @"C:\DCSWorld\Mods\aircraft\A10C" ),
            ChangeTypeMode.Upload,
            loggerMock.Object );
        var fileNode = new FileEntryViewModel(
            new LocalFileEntry( "Mission1.miz", "A10C/Mission1.miz", false, @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" ),
            ChangeTypeMode.Upload,
            loggerMock.Object );
        directoryNode.Children.Add( fileNode );

        directoryNode.IsSelected = true;

        Assert.True( directoryNode.IsSelected );
        Assert.False( fileNode.IsSelected );
    }

    /// <summary>
    /// ディレクトリ選択解除時に子ノードの選択状態を変更しないことを検証する。
    /// </summary>
    [Fact]
    public void ディレクトリ選択解除時に子ノードの選択状態を変更しない() {
        var loggerMock = new Mock<ILoggingService>();
        var directoryNode = new FileEntryViewModel(
            new LocalFileEntry( "A10C", "A10C", true, @"C:\DCSWorld\Mods\aircraft\A10C" ),
            ChangeTypeMode.Upload,
            loggerMock.Object );
        var fileNode = new FileEntryViewModel(
            new LocalFileEntry( "Mission1.miz", "A10C/Mission1.miz", false, @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" ),
            ChangeTypeMode.Upload,
            loggerMock.Object );
        directoryNode.Children.Add( fileNode );
        fileNode.IsSelected = true;
        directoryNode.IsSelected = true;

        directoryNode.IsSelected = false;

        Assert.False( directoryNode.IsSelected );
        Assert.True( fileNode.IsSelected );
    }
}