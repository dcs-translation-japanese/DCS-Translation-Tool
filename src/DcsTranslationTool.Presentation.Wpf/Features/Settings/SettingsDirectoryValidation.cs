using System.IO;

using DcsTranslationTool.Resources;

namespace DcsTranslationTool.Presentation.Wpf.Features.Settings;

/// <summary>設定画面と操作案内で共用する必須フォルダーの検証。</summary>
public static class SettingsDirectoryValidation {
    /// <summary>
    /// 必須フォルダーの入力値を検証する。DCS World とユーザーフォルダーは実在を必須とし、ダウンロード先は空欄のみを拒否する。
    /// </summary>
    /// <param name="key">フォルダー設定を識別するキー。</param>
    /// <param name="path">検証するフォルダーのパス。</param>
    /// <returns>検証エラーのメッセージ。入力が有効な場合は null。</returns>
    public static string? GetError( string key, string path ) {
        if(string.IsNullOrWhiteSpace( path )) {
            return Strings_Settings.DirectoryRequiredMessage;
        }

        if(key is "DcsWorldInstall" or "UserMission" && !Directory.Exists( path )) {
            return Strings_Settings.DirectoryNotFoundMessage;
        }

        return null;
    }
}