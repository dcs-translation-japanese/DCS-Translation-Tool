using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace DcsTranslationTool.Shared.Models;

/// <summary>
/// アプリケーション全体の設定を保持するモデル。
/// </summary>
public class AppSettings : INotifyPropertyChanged {

    /// <summary>設定スキーマの互換性管理用。</summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>設定プロパティの値が変わったときに通知する。</summary>
    public event PropertyChangedEventHandler? PropertyChanged;
    /// <summary>
    /// 値が変わった場合だけ設定を更新し、対応するプロパティの変更を通知する。
    /// </summary>
    /// <typeparam name="T">設定値の型。</typeparam>
    /// <param name="field">現在の値を保持するフィールド。</param>
    /// <param name="value">新しく設定する値。</param>
    /// <param name="name">変更通知に使用するプロパティ名。</param>
    /// <returns>値を更新した場合は true。変更がない場合は false。</returns>
    private bool Set<T>( ref T field, T value, [CallerMemberName] string? name = null ) {
        if(EqualityComparer<T>.Default.Equals( field, value )) return false;
        field = value;
        PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( name ) );
        return true;
    }

    #region Fields

    /// <summary>アプリケーションのタイトル文字列を保持する。</summary>
    private string _applicationTitle = "DcsTranslationTool";
    /// <summary>メインウィンドウの幅を保持する。</summary>
    private double _shellWidth = 600;
    /// <summary>メインウィンドウの高さを保持する。</summary>
    private double _shellHeight = 400;
    /// <summary>操作案内を表示済みかどうかを保持する。</summary>
    private bool _hasSeenCoachMark;
    /// <summary>DCS World のインストールフォルダーのパスを保持する。</summary>
    private string _dcsWorldInstallDir = string.Empty;
    /// <summary>ユーザーミッションフォルダーのパスを保持する。</summary>
    private string _sourceUserMissionDir = string.Empty;
    /// <summary>翻訳ファイルのダウンロード先フォルダーのパスを保持する。</summary>
    private string _translateFileDir = Path.Combine(AppContext.BaseDirectory, "TranslateFile");
    /// <summary>機体の翻訳注入先に外部フォルダーを使用するかどうかを保持する。</summary>
    private bool _useExternalAircraftInjectionDir = false;
    /// <summary>機体の翻訳注入先に使用する外部フォルダーのパスを保持する。</summary>
    private string _externalAircraftInjectionDir = string.Empty;
    /// <summary>キャンペーンの翻訳注入先に外部フォルダーを使用するかどうかを保持する。</summary>
    private bool _useExternalCampaignInjectionDir = false;
    /// <summary>キャンペーンの翻訳注入先に使用する外部フォルダーのパスを保持する。</summary>
    private string _externalCampaignInjectionDir = string.Empty;
    /// <summary>翻訳作成ウィンドウの幅を保持する。</summary>
    private double _translationCreationWindowWidth = 1200;
    /// <summary>翻訳作成ウィンドウの高さを保持する。</summary>
    private double _translationCreationWindowHeight = 900;
    /// <summary>翻訳作成画面の辞書一覧領域の高さ比率を保持する。</summary>
    private double _translationCreationDictionaryPaneRatio = 2;
    /// <summary>辞書の詳細テキストを右端で折り返すかどうかを保持する。</summary>
    private bool _translationCreationWrapDictionaryDetailsText = true;
    /// <summary>API 通信で優先する経路を保持する。</summary>
    private ApiRoutePreference _apiPreferredRoute = ApiRoutePreference.None;
    /// <summary>API 通信の優先経路の有効期限を UTC で保持する。</summary>
    private DateTimeOffset? _apiPreferredRouteValidUntilUtc;
    /// <summary>API 通信の優先経路を最後に検証した日時を UTC で保持する。</summary>
    private DateTimeOffset? _apiRouteLastVerifiedAtUtc;
    /// <summary>API 通信の優先経路を再検証できる日時を UTC で保持する。</summary>
    private DateTimeOffset? _apiRouteVerificationRetryAfterUtc;
    ///<summary>このアプリのリポジトリーURL。</summary>
    private const string _repositoryUrl = "https://github.com/dcs-translation-japanese/DCS-Translation-Tool/";

    #endregion

    #region Properties

    /// <summary>このアプリの最新版へのリンク。</summary>
    [JsonIgnore]
    public string ReleaseLatestUrl { get; init; } = _repositoryUrl + "releases/latest";

    /// <summary>このアプリのライセンス。</summary>
    [JsonIgnore]
    public string LicenseType { get; init; } = "MIT";

    /// <summary>アプリケーションのライセンス本文へのリンク。</summary>
    [JsonIgnore]
    public string LicenseUrl { get; init; } = _repositoryUrl + "blob/master/LICENSE";

    /// <summary>このアプリのプライバシーに関する声明。</summary>
    [JsonIgnore]
    public string PrivacyStatement { get; init; } = _repositoryUrl + "blob/master/PRIVACY.md";

    /// <summary>アプリケーションのタイトル文字列。</summary>
    [JsonIgnore]
    public string ApplicationTitle {
        get => _applicationTitle;
        set => Set( ref _applicationTitle, value );
    }

    /// <summary>シェルウィンドウの幅。</summary>
    public double ShellWidth {
        get => _shellWidth;
        set => Set( ref _shellWidth, value );
    }

    /// <summary>シェルウィンドウの高さ。</summary>
    public double ShellHeight {
        get => _shellHeight;
        set => Set( ref _shellHeight, value );
    }

    /// <summary>操作案内を一度表示したかどうか。既存設定で省略されている場合も初回表示する。</summary>
    public bool HasSeenCoachMark {
        get => _hasSeenCoachMark;
        set => Set( ref _hasSeenCoachMark, value );
    }

    /// <summary>DCS World のインストールフォルダーのパスを取得または設定する。</summary>
    public string DcsWorldInstallDir {
        get => _dcsWorldInstallDir;
        set => Set( ref _dcsWorldInstallDir, value );
    }

    /// <summary>ユーザーミッションフォルダーのパスを取得または設定する。</summary>
    public string SourceUserMissionDir {
        get => _sourceUserMissionDir;
        set => Set( ref _sourceUserMissionDir, value );
    }

    /// <summary>翻訳ファイルのダウンロード先フォルダーのパスを取得または設定する。</summary>
    public string TranslateFileDir {
        get => _translateFileDir;
        set => Set( ref _translateFileDir, value );
    }

    /// <summary>機体の翻訳注入先に外部フォルダーを使用するかどうかを取得または設定する。</summary>
    public bool UseExternalAircraftInjectionDir {
        get => _useExternalAircraftInjectionDir;
        set => Set( ref _useExternalAircraftInjectionDir, value );
    }

    /// <summary>機体の翻訳注入先に使用する外部フォルダーのパスを取得または設定する。</summary>
    public string ExternalAircraftInjectionDir {
        get => _externalAircraftInjectionDir;
        set => Set( ref _externalAircraftInjectionDir, value );
    }

    /// <summary>キャンペーンの翻訳注入先に外部フォルダーを使用するかどうかを取得または設定する。</summary>
    public bool UseExternalCampaignInjectionDir {
        get => _useExternalCampaignInjectionDir;
        set => Set( ref _useExternalCampaignInjectionDir, value );
    }

    /// <summary>キャンペーンの翻訳注入先に使用する外部フォルダーのパスを取得または設定する。</summary>
    public string ExternalCampaignInjectionDir {
        get => _externalCampaignInjectionDir;
        set => Set( ref _externalCampaignInjectionDir, value );
    }

    /// <summary>TranslationCreation Window の幅を保持する。</summary>
    public double TranslationCreationWindowWidth {
        get => _translationCreationWindowWidth;
        set => Set( ref _translationCreationWindowWidth, value );
    }

    /// <summary>TranslationCreation Window の高さを保持する。</summary>
    public double TranslationCreationWindowHeight {
        get => _translationCreationWindowHeight;
        set => Set( ref _translationCreationWindowHeight, value );
    }

    /// <summary>TranslationCreation Window の dictionary 一覧領域の高さ比率を保持する。</summary>
    public double TranslationCreationDictionaryPaneRatio {
        get => _translationCreationDictionaryPaneRatio;
        set => Set( ref _translationCreationDictionaryPaneRatio, value );
    }

    /// <summary>TranslationCreation Window の詳細テキストを右端で折り返すかどうかを保持する。</summary>
    public bool TranslationCreationWrapDictionaryDetailsText {
        get => _translationCreationWrapDictionaryDetailsText;
        set => Set( ref _translationCreationWrapDictionaryDetailsText, value );
    }

    /// <summary>API通信で優先する経路を保持する。</summary>
    public ApiRoutePreference ApiPreferredRoute {
        get => _apiPreferredRoute;
        set => Set( ref _apiPreferredRoute, value );
    }

    /// <summary>優先経路の有効期限UTCを保持する。</summary>
    public DateTimeOffset? ApiPreferredRouteValidUntilUtc {
        get => _apiPreferredRouteValidUntilUtc;
        set => Set( ref _apiPreferredRouteValidUntilUtc, value );
    }

    /// <summary>優先経路を最後に検証した時刻UTCを保持する。</summary>
    public DateTimeOffset? ApiRouteLastVerifiedAtUtc {
        get => _apiRouteLastVerifiedAtUtc;
        set => Set( ref _apiRouteLastVerifiedAtUtc, value );
    }

    /// <summary>優先経路の再検証を再試行できる時刻UTCを保持する。</summary>
    public DateTimeOffset? ApiRouteVerificationRetryAfterUtc {
        get => _apiRouteVerificationRetryAfterUtc;
        set => Set( ref _apiRouteVerificationRetryAfterUtc, value );
    }

    #endregion
}

/// <summary>API通信で利用する経路種別を表す。</summary>
public enum ApiRoutePreference {
    /// <summary>API 通信の優先経路を指定しない。</summary>
    None,
    /// <summary>システム既定の経路で API 通信を行う。</summary>
    Default,
    /// <summary>IPv4 の経路で API 通信を行う。</summary>
    Ipv4,
    /// <summary>IPv6 の経路で API 通信を行う。</summary>
    Ipv6,
}