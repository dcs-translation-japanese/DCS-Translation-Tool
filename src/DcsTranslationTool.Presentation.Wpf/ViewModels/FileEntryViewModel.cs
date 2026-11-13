using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

using Caliburn.Micro;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Domain.Models;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;
using DcsTranslationTool.Shared.Models;

namespace DcsTranslationTool.Presentation.Wpf.ViewModels;

/// <inheritdoc/>
public class FileEntryViewModel(
    FileEntry model,
    ChangeTypeMode changeTypeMode,
    ILoggingService logger
) : PropertyChangedBase, IFileEntryViewModel, IDisposable {

    #region Fields

    /// <summary>チェック状態を保持する。子要素の選択が混在している場合は null。</summary>
    private bool? _checkState = false;
    /// <summary>エントリーが選択されているかどうかを保持する。</summary>
    private bool _isSelected = false;

    /// <summary>このエントリーに属する子エントリーの一覧。</summary>
    private ObservableCollection<IFileEntryViewModel> _children = [];
    /// <summary>子エントリーの変更通知を購読済みかどうかを保持する。</summary>
    private bool _childrenHandlersAttached;
    /// <summary>チェック状態の伝播中に、子エントリーからの変更通知による再計算を抑制するフラグ。</summary>
    private bool _suppressCheckPropagation = false;
    /// <summary>エントリーの変更種別を判定するモード。</summary>
    private readonly ChangeTypeMode _mode = changeTypeMode;
    /// <summary>ツリー上で子エントリーを展開しているかどうかを保持する。</summary>
    private bool _isExpanded;
    /// <summary>エントリーを画面に表示するかどうかを保持する。</summary>
    private bool _isVisible = true;
    /// <summary>前回の判定で得られた変更種別のキャッシュ。</summary>
    private FileChangeType? _cachedChangeType;
    /// <summary>変更種別の再計算が必要かどうかを保持する。</summary>
    private bool _isChangeTypeDirty = true;

    #endregion

    #region Events / Model

    /// <inheritdoc/>
    public event EventHandler<bool?>? CheckStateChanged;

    /// <inheritdoc/>
    public FileEntry Model { get; } = model;

    #endregion

    #region Basic Properties

    /// <inheritdoc/>
    public string Name => Model.Name;

    /// <inheritdoc/>
    public string Path => Model.Path;

    /// <inheritdoc/>
    public bool IsDirectory => Model.IsDirectory;

    #endregion

    #region UI State

    /// <inheritdoc/>
    public bool IsExpanded {
        get => _isExpanded;
        set => Set( ref _isExpanded, value );
    }

    /// <inheritdoc/>
    public bool IsVisible {
        get => _isVisible;
        set => Set( ref _isVisible, value );
    }

    #endregion

    #region Derived Properties

    /// <inheritdoc/>
    public FileChangeType? ChangeType {
        get {
            if(_isChangeTypeDirty) {
                _cachedChangeType = ComputeChangeType();
                _isChangeTypeDirty = false;
            }

            return _cachedChangeType;
        }
    }

    /// <inheritdoc/>
    public bool CanCheck => (_mode, ChangeType) switch
    {
        (ChangeTypeMode.Upload, FileChangeType.Unchanged ) => false,
        (ChangeTypeMode.Download, FileChangeType.Unchanged ) => false,
        _ => true,
    };

    /// <inheritdoc/>
    public bool? CheckState {
        get => _checkState;
        set {
            var coercedValue = CanCheck ? value : false;
            if(!Set( ref _checkState, coercedValue )) return;

            // 親→子へ伝播
            if(!_suppressCheckPropagation && IsDirectory && coercedValue is not null) {
                try {
                    _suppressCheckPropagation = true;
                    foreach(var child in Children) {
                        var childTargetValue = child.CanCheck ? coercedValue : false;
                        if(child.CheckState != childTargetValue) child.CheckState = childTargetValue;
                    }
                }
                finally {
                    _suppressCheckPropagation = false;
                }
            }

            CheckStateChanged?.Invoke( this, _checkState );
        }
    }
    /// <inheritdoc/>
    public bool IsSelected {
        get => _isSelected;
        set {
            if(!CanCheck && !value) return;

            if(!Set( ref _isSelected, value )) return;
            logger.Info( $"選択状態を更新した。Path={Model.Path}, Value={value}" );
        }
    }

    /// <inheritdoc/>
    public ObservableCollection<IFileEntryViewModel> Children {
        get {
            EnsureChildrenHandlersAttached();
            return _children;
        }
        set {
            if(ReferenceEquals( _children, value )) return;
            DetachChildrenHandlers( _children );
            Set( ref _children, value ?? [] );
            AttachChildrenHandlers( _children );
            logger.Info( $"子ノードコレクションを更新した。Path={Model.Path}, ChildCount={_children.Count}" );

            // 参照入替に伴い集計系を更新
            InvalidateChangeType();
            RecomputeCheckStateFromChildren();
            if(!CanCheck && _checkState is not false) CheckState = false;
        }
    }

    #endregion

    #region Ctor / Dispose

    /// <summary>
    /// 子ノードのイベント購読を解除してノードを破棄する。
    /// </summary>
    public void Dispose() {
        DetachChildrenHandlers( Children );
        GC.SuppressFinalize( this );
        logger.Info( $"FileEntryViewModel を破棄した。Path={Model.Path}" );
    }

    #endregion

    #region Public Methods

    /// <inheritdoc/>
    public void SetSelectRecursive( bool value ) {
        logger.Info( $"選択状態を再帰的に設定する。Path={Model.Path}, Value={value}" );
        IsSelected = value;
        foreach(var child in Children) child.SetSelectRecursive( value );
    }

    /// <inheritdoc/>
    public List<FileEntry> GetCheckedModelRecursive( bool fileOnly = false ) {
        var checkedChildrenModels = new List<FileEntry>();

        switch(CheckState, IsDirectory, fileOnly) {
            case (true, false, _ ):
            case (true, true, false ):
            case (null, false, _ ):
            case (null, true, false ):
                checkedChildrenModels.Add( Model );
                break;
        }

        foreach(var child in Children)
            checkedChildrenModels.AddRange( child.GetCheckedModelRecursive( fileOnly ) );

        return checkedChildrenModels;
    }

    /// <inheritdoc/>
    public List<IFileEntryViewModel> GetCheckedViewModelRecursive() {
        var checkedChildren = new List<IFileEntryViewModel>();

        if(CheckState is true &&
            !IsDirectory &&
            ChangeType != FileChangeType.Unchanged) {
            checkedChildren.Add( this );
        }

        foreach(var child in Children)
            checkedChildren.AddRange( child.GetCheckedViewModelRecursive() );

        return checkedChildren;
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// 子ノードのイベントが未購読の場合だけ購読を開始する。
    /// </summary>
    private void EnsureChildrenHandlersAttached() {
        if(_childrenHandlersAttached) return;
        AttachChildrenHandlers( _children );
    }

    /// <summary>
    /// 現在ノードの変更種別を算出する。
    /// </summary>
    /// <returns>算出した変更種別。</returns>
    private FileChangeType? ComputeChangeType() {
        if(!IsDirectory) {
            return (Model.LocalSha, Model.RepoSha) switch
            {
                (string l, string r ) when l == r => FileChangeType.Unchanged,
                (string l, string r ) when l != r => FileChangeType.Modified,
                (string _, null ) => FileChangeType.LocalOnly,
                (null, string _ ) => FileChangeType.RepoOnly,
                (null, null ) => null,
                _ => throw new NotImplementedException()
            };
        }

        if(Children.Count == 0) {
            return FileChangeType.Unchanged;
        }

        var hasModified = false;
        var hasRepoOnly = false;
        var hasLocalOnly = false;
        var hasUnchanged = false;

        foreach(var child in Children) {
            switch(child.ChangeType) {
                case FileChangeType.Modified:
                    hasModified = true;
                    break;
                case FileChangeType.RepoOnly:
                    hasRepoOnly = true;
                    break;
                case FileChangeType.LocalOnly:
                    hasLocalOnly = true;
                    break;
                case FileChangeType.Unchanged:
                    hasUnchanged = true;
                    break;
            }
        }

        return _mode switch
        {
            ChangeTypeMode.Download when hasModified => FileChangeType.Modified,
            ChangeTypeMode.Download when hasRepoOnly => FileChangeType.RepoOnly,
            ChangeTypeMode.Download when hasUnchanged => FileChangeType.Unchanged,
            ChangeTypeMode.Download when hasLocalOnly => FileChangeType.LocalOnly,

            ChangeTypeMode.Upload when hasModified => FileChangeType.Modified,
            ChangeTypeMode.Upload when hasLocalOnly => FileChangeType.LocalOnly,
            ChangeTypeMode.Upload when hasUnchanged => FileChangeType.Unchanged,
            ChangeTypeMode.Upload when hasRepoOnly => FileChangeType.RepoOnly,

            ChangeTypeMode.Download => null,
            ChangeTypeMode.Upload => null,
            _ => throw new InvalidEnumArgumentException(),
        };
    }

    /// <summary>
    /// 変更種別キャッシュを無効化して関連プロパティを通知する。
    /// </summary>
    private void InvalidateChangeType() {
        _isChangeTypeDirty = true;
        NotifyOfPropertyChange( nameof( ChangeType ) );
        NotifyOfPropertyChange( nameof( CanCheck ) );
    }

    /// <summary>
    /// 子ノードの状態から自分のチェック状態を再計算する
    /// </summary>
    private void RecomputeCheckStateFromChildren() {
        if(!IsDirectory || Children.Count == 0) return;

        var allChecked = Children.All(c => c.CheckState == true);
        var allUnchecked = Children.All(c => c.CheckState == false);

        bool? newState = (allChecked, allUnchecked) switch
        {
            (true, _) => true,
            (_, true) => false,
            _ => null,
        };

        if(CheckState == newState) return;
        CheckState = newState;
        NotifyOfPropertyChange( nameof( CheckState ) );
        CheckStateChanged?.Invoke( this, CheckState );
        logger.Info( $"子ノードに基づきチェック状態を再計算した。Path={Model.Path}, Value={newState}" );
    }

    /// <summary>既存コレクションから購読を全解除</summary>
    private void DetachChildrenHandlers( ObservableCollection<IFileEntryViewModel> collection ) {
        if(collection is null) return;
        collection.CollectionChanged -= OnChildrenCollectionChanged;
        foreach(var ch in collection) {
            if(ch is INotifyPropertyChanged inpc) inpc.PropertyChanged -= OnChildPropertyChanged;
            ch.CheckStateChanged -= OnChildCheckStateChanged;
        }
        if(ReferenceEquals( collection, _children )) _childrenHandlersAttached = false;
    }

    /// <summary>コレクションへ購読を付与</summary>
    private void AttachChildrenHandlers( ObservableCollection<IFileEntryViewModel> collection ) {
        if(collection is null) return;
        collection.CollectionChanged -= OnChildrenCollectionChanged; // 二重防止
        collection.CollectionChanged += OnChildrenCollectionChanged;
        foreach(var ch in collection) {
            if(ch is INotifyPropertyChanged inpc) inpc.PropertyChanged += OnChildPropertyChanged;
            ch.CheckStateChanged += OnChildCheckStateChanged;
        }
        if(ReferenceEquals( collection, _children )) _childrenHandlersAttached = true;
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// 子ノードの増減に合わせてイベント購読と変更種別、チェック状態を更新する。
    /// </summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">イベントの情報。</param>
    private void OnChildrenCollectionChanged( object? sender, NotifyCollectionChangedEventArgs e ) {
        if(e.Action == NotifyCollectionChangedAction.Reset) {
            DetachChildrenHandlers( Children );
            AttachChildrenHandlers( Children );
            InvalidateChangeType();
            RecomputeCheckStateFromChildren();
            if(!CanCheck && CheckState != false) CheckState = false;
            logger.Info( $"子ノードコレクションがリセットされた。Path={Model.Path}" );
            return;
        }

        if(e.OldItems is not null) {
            foreach(var obj in e.OldItems) {
                if(obj is INotifyPropertyChanged inpc) inpc.PropertyChanged -= OnChildPropertyChanged;
                if(obj is IFileEntryViewModel vm) vm.CheckStateChanged -= OnChildCheckStateChanged;
            }
        }
        if(e.NewItems is not null) {
            foreach(var obj in e.NewItems) {
                if(obj is INotifyPropertyChanged inpc) inpc.PropertyChanged += OnChildPropertyChanged;
                if(obj is IFileEntryViewModel vm) vm.CheckStateChanged += OnChildCheckStateChanged;
            }
        }

        InvalidateChangeType();
        RecomputeCheckStateFromChildren();
        if(!CanCheck && CheckState is not false) CheckState = false;
    }

    /// <summary>
    /// 子ノードの変更種別が変わったときに集計値とチェック可否を更新する。
    /// </summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">イベントの情報。</param>
    private void OnChildPropertyChanged( object? sender, PropertyChangedEventArgs e ) {
        if(e.PropertyName == nameof( ChangeType )) {
            InvalidateChangeType();
            if(!CanCheck && CheckState is not false) CheckState = false;
        }
    }

    /// <summary>子のチェック状態変化時に自分の状態を再計算する</summary>
    private void OnChildCheckStateChanged( object? sender, bool? e ) {
        if(_suppressCheckPropagation) return;
        var previousState = CheckState;
        RecomputeCheckStateFromChildren();
        // 部分選択のままでも、操作ボタンと案内には選択内容の変更を通知する。
        if(CheckState == previousState) CheckStateChanged?.Invoke( this, CheckState );
    }

    #endregion
}