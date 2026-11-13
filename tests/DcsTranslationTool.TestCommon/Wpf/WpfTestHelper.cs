namespace DcsTranslationTool.TestCommon.Wpf;

/// <summary>
/// WPF テストに必要な共通補助処理を提供する。
/// </summary>
public static class WpfTestHelper {
    /// <summary>
    /// アプリケーション リソース初期化を保証する。
    /// </summary>
    /// <param name="resourceSources">追加するマージ辞書 URI 一覧。</param>
    public static void EnsureApplicationResources( params string[] resourceSources ) {
        var applicationType = GetRequiredType( "System.Windows.Application, PresentationFramework" );
        var currentApplication = applicationType.GetProperty( "Current" )?.GetValue( null );
        if(currentApplication is null) {
            currentApplication = Activator.CreateInstance( applicationType )
                ?? throw new InvalidOperationException( "Application の生成に失敗した。" );
            var shutdownModeProperty = applicationType.GetProperty( "ShutdownMode" )
                ?? throw new InvalidOperationException( "Application.ShutdownMode が見つからない。" );
            // 個々のテストのウィンドウ終了で、後続テストの Application を終了させない。
            shutdownModeProperty.SetValue( currentApplication, Enum.Parse( shutdownModeProperty.PropertyType, "OnExplicitShutdown" ) );
        }
        var resourcesProperty = applicationType.GetProperty( "Resources" )
            ?? throw new InvalidOperationException( "Application.Resources が見つからない。" );
        var resources = resourcesProperty.GetValue( currentApplication )
            ?? throw new InvalidOperationException( "Application.Resources の取得に失敗した。" );
        var mergedDictionariesProperty = resources.GetType().GetProperty( "MergedDictionaries" )
            ?? throw new InvalidOperationException( "MergedDictionaries が見つからない。" );
        var dictionaries = mergedDictionariesProperty.GetValue( resources ) as System.Collections.IEnumerable
            ?? throw new InvalidOperationException( "MergedDictionaries の取得に失敗した。" );
        foreach(var source in resourceSources) {
            AddMergedDictionaryIfMissing( dictionaries, source );
        }
    }

    /// <summary>
    /// 指定ルートから最初に見つかった子要素を返す。
    /// </summary>
    /// <typeparam name="T">取得対象型。</typeparam>
    /// <param name="root">探索起点。</param>
    /// <returns>最初に見つかった要素を返す。見つからない場合は <see langword="null"/> を返す。</returns>
    public static T? FindDescendant<T>( object root )
        where T : class {
        var visualTreeHelperType = GetRequiredType( "System.Windows.Media.VisualTreeHelper, PresentationCore" );
        var getChildrenCountMethod = visualTreeHelperType.GetMethod( "GetChildrenCount" )
            ?? throw new InvalidOperationException( "VisualTreeHelper.GetChildrenCount が見つからない。" );
        var getChildMethod = visualTreeHelperType.GetMethod( "GetChild" )
            ?? throw new InvalidOperationException( "VisualTreeHelper.GetChild が見つからない。" );
        var childrenCount = (int)(getChildrenCountMethod.Invoke( null, [root] )
            ?? throw new InvalidOperationException( "子要素数の取得に失敗した。" ));
        for(var index = 0; index < childrenCount; index++) {
            var child = getChildMethod.Invoke( null, [root, index] )
                ?? throw new InvalidOperationException( $"子要素の取得に失敗した。Index={index}" );
            if(child is T target) {
                return target;
            }

            var descendant = FindDescendant<T>( child );
            if(descendant is not null) {
                return descendant;
            }
        }

        return null;
    }

    /// <summary>
    /// 指定ルート配下の要素を取得する。
    /// </summary>
    /// <typeparam name="T">取得対象型。</typeparam>
    /// <param name="root">探索起点。</param>
    /// <returns>取得した要素を返す。</returns>
    public static T GetRequiredDescendant<T>( object root )
        where T : class =>
        FindDescendant<T>( root ) ?? throw new InvalidOperationException( $"{typeof( T ).Name} が見つからない。" );

    /// <summary>
    /// Dispatcher キューを処理する。
    /// </summary>
    public static void PumpDispatcher() {
        var dispatcherType = GetRequiredType( "System.Windows.Threading.Dispatcher, WindowsBase" );
        var dispatcherPriorityType = GetRequiredType( "System.Windows.Threading.DispatcherPriority, WindowsBase" );
        var currentDispatcher = dispatcherType.GetProperty( "CurrentDispatcher" )?.GetValue( null )
            ?? throw new InvalidOperationException( "CurrentDispatcher の取得に失敗した。" );
        var priority = Enum.Parse( dispatcherPriorityType, "Background" );
        var invokeMethod = dispatcherType.GetMethod( "Invoke", [typeof( Action ), dispatcherPriorityType] )
            ?? throw new InvalidOperationException( "Dispatcher.Invoke が見つからない。" );
        invokeMethod.Invoke( currentDispatcher, [new Action( () => { } ), priority] );
    }

    /// <summary>
    /// ItemsControl 配下のコンテナ生成を保証する。
    /// </summary>
    /// <param name="itemsControl">対象 ItemsControl。</param>
    public static void EnsureContainersGenerated( object itemsControl ) {
        InvokeParameterless( itemsControl, "ApplyTemplate" );
        InvokeParameterless( itemsControl, "UpdateLayout" );
        PumpDispatcher();

        if(string.Equals( itemsControl.GetType().Name, "TreeViewItem", StringComparison.Ordinal )) {
            itemsControl.GetType().GetProperty( "IsExpanded" )?.SetValue( itemsControl, true );
            InvokeParameterless( itemsControl, "UpdateLayout" );
            PumpDispatcher();
        }
    }

    /// <summary>
    /// 指定インデックスの TreeViewItem を取得する。
    /// </summary>
    /// <param name="itemsControl">取得元 ItemsControl。</param>
    /// <param name="index">対象インデックス。</param>
    /// <returns>取得した TreeViewItem を返す。</returns>
    public static T GetTreeViewItemAt<T>( object itemsControl, int index )
        where T : class {
        EnsureContainersGenerated( itemsControl );
        var itemContainerGenerator = itemsControl.GetType().GetProperty( "ItemContainerGenerator" )?.GetValue( itemsControl )
            ?? throw new InvalidOperationException( "ItemContainerGenerator が見つからない。" );
        var container = itemContainerGenerator.GetType().GetMethod( "ContainerFromIndex", [typeof( int )] )?.Invoke( itemContainerGenerator, [index] );

        return container as T
            ?? throw new InvalidOperationException( $"TreeViewItem の生成に失敗した。Index={index}" );
    }

    /// <summary>
    /// 対象要素をレイアウト更新後に返す。
    /// </summary>
    /// <typeparam name="T">取得対象型。</typeparam>
    /// <param name="root">探索起点。</param>
    /// <returns>取得した要素を返す。</returns>
    public static T GetRequiredDescendantAfterLayout<T>( object root )
        where T : class {
        InvokeParameterless( root, "ApplyTemplate" );
        InvokeParameterless( root, "UpdateLayout" );
        PumpDispatcher();
        return GetRequiredDescendant<T>( root );
    }

    /// <summary>同じ参照先のリソース辞書が未登録の場合だけ追加する。</summary>
    /// <param name="dictionaries">リソース辞書を追加するコレクション。</param>
    /// <param name="source">追加するリソース辞書の絶対 URI。</param>
    private static void AddMergedDictionaryIfMissing( System.Collections.IEnumerable dictionaries, string source ) {
        if(dictionaries.Cast<object>().Any( dictionary => string.Equals( GetSourceOriginalString( dictionary ), source, StringComparison.OrdinalIgnoreCase ) )) {
            return;
        }

        var resourceDictionaryType = GetRequiredType( "System.Windows.ResourceDictionary, PresentationFramework" );
        var dictionary = Activator.CreateInstance( resourceDictionaryType )
            ?? throw new InvalidOperationException( "ResourceDictionary の生成に失敗した。" );
        resourceDictionaryType.GetProperty( "Source" )?.SetValue( dictionary, new Uri( source, UriKind.Absolute ) );
        dictionaries.GetType().GetMethod( "Add" )?.Invoke( dictionaries, [dictionary] );
    }

    /// <summary>リソース辞書の参照先 URI に指定された元の文字列を取得する。</summary>
    /// <param name="dictionary">参照先を取得するリソース辞書。</param>
    /// <returns>参照先 URI の元の文字列。取得できない場合は null。</returns>
    private static string? GetSourceOriginalString( object dictionary ) {
        var source = dictionary.GetType().GetProperty( "Source" )?.GetValue( dictionary );
        return source?.GetType().GetProperty( "OriginalString" )?.GetValue( source ) as string;
    }

    /// <summary>アセンブリ修飾名から型を取得し、見つからない場合は例外を送出する。</summary>
    /// <param name="assemblyQualifiedName">取得する型のアセンブリ修飾名。</param>
    /// <returns>指定した名前に対応する型。</returns>
    /// <exception cref="InvalidOperationException">指定した型を取得できなかった場合。</exception>
    private static Type GetRequiredType( string assemblyQualifiedName ) =>
        Type.GetType( assemblyQualifiedName )
        ?? throw new InvalidOperationException( $"{assemblyQualifiedName} の型解決に失敗した。" );

    /// <summary>指定した名前の引数なしメソッドが存在する場合に呼び出す。</summary>
    /// <param name="target">メソッドを呼び出す対象。</param>
    /// <param name="methodName">呼び出す引数なしメソッドの名前。</param>
    private static void InvokeParameterless( object target, string methodName ) {
        target.GetType().GetMethod( methodName, Type.EmptyTypes )?.Invoke( target, null );
    }
}