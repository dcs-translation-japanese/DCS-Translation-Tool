using System.Text;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Shared.Models;

using UtfUnknown;

namespace DcsTranslationTool.Infrastructure.IO;

/// <summary>
/// 任意のバイト配列を解析して、テキスト/バイナリ判定と文字コード推測を行うユーティリティ。
/// 設計方針:
/// - 許容エンコードは UTF-8, UTF-16, 日本語系, ASCII のみに限定する（ANSI/UTF-32 は除外）
/// - BOM は UTF-8, UTF-16LE, UTF-16BE のみテキストとして即時採用
/// - UTF-16 の BOM なしはヒューリスティックで推測
/// - それ以外は UtfUnknown の結果からホワイトリスト内のみ厳格デコードし、失敗時はバイナリ
/// - 判定の確信度（0.0〜1.0）を付与
/// </summary>
public sealed class FileContentInspector : IFileContentInspector {
    /// <summary>
    /// 静的初期化子。必要なコードページ（EUC-JP 等）を有効化する。
    /// </summary>
    static FileContentInspector() {
        Encoding.RegisterProvider( CodePagesEncodingProvider.Instance );
    }

    /// <summary>
    /// メモリ上のデータを解析し、バイナリ/テキストとエンコードを判定する。
    /// </summary>
    /// <param name="content">解析対象のバイト配列。</param>
    /// <returns>解析結果 <see cref="FileContentInfo"/>。</returns>
    /// <remarks>
    /// 仕様:
    /// - テキストとして許可: UTF-8, UTF-16LE/BE, ASCII, 主要東アジア・欧州のいくつかの 8bit 符号化
    /// - UTF-32, UTF-7, ANSI（windows-1252/Encoding.Default 等）は常にバイナリ扱い
    /// </remarks>
    public FileContentInfo Inspect( byte[] content ) {
        ArgumentNullException.ThrowIfNull( content );

        // 空ファイルはテキスト（空文字）として扱う
        if(content.Length == 0) {
            return new FileContentInfo(
                IsBinary: false,
                Encoding: Encoding.UTF8,
                DetectionConfidence: 1.0,
                Text: string.Empty,
                ByteCount: 0 );
        }

        // UTF-32 BOM は非許容（即バイナリ）
        if(HasUtf32Bom( content )) return new FileContentInfo( true, null, 1.0, null, content.Length );

        // 1) BOM 優先（UTF-8, UTF-16LE, UTF-16BE のみ）
        if(TryGetBomEncoding( content, out var bomEnc ) && bomEnc is not null) {
            var text = SafeDecode(content, bomEnc);
            // .WebName は BOM 有無で不変。BOM の有無はここでは区別しない。
            return new FileContentInfo( false, bomEnc, 1.0, text, content.Length );
        }

        // 2) UTF-16LE (BOMなし) ヒューリスティック
        if(TryGuessUtf16LeWithoutBom( content, out var utf16LeEnc, out var utf16LeConf )) {
            try {
                var text = SafeDecode(content, utf16LeEnc);
                return new FileContentInfo( false, utf16LeEnc, utf16LeConf, text, content.Length );
            }
            catch {
                // 後続の推測へ
            }
        }

        // 3) UTF-16BE (BOMなし) ヒューリスティック
        if(TryGuessUtf16BeWithoutBom( content, out var utf16BeEnc, out var utf16BeConf )) {
            try {
                var text = SafeDecode(content, utf16BeEnc);
                return new FileContentInfo( false, utf16BeEnc, utf16BeConf, text, content.Length );
            }
            catch {
                // 後続の推測へ
            }
        }

        // 4) UtfUnknown で推測
        var detection = CharsetDetector.DetectFromBytes(content).Detected;

        // 5) バイナリっぽさのスコア
        var binaryScore = ComputeBinaryScore(content);

        // 5.1) 強いバイナリらしさは UTF-8 厳格判定よりも優先してバイナリ扱い
        //      例: 全て NUL（0x00）の配列など。これにより「NULだらけUTF-8可解釈」をテキスト認定しない。
        if(binaryScore >= 0.60) return new FileContentInfo( true, null, detection?.Confidence ?? 0.0, null, content.Length );

        // 6) UtfUnknown の結果をホワイトリストでフィルタし厳格デコード
        if(detection?.Encoding is not null && detection.Confidence >= 0.50) {
            var webName = NormalizeName(detection.Encoding.WebName);
            if(AllowedEncodings.Contains( webName )) {
                try {
                    var text = SafeDecode(content, detection.Encoding);
                    return new FileContentInfo( false, detection.Encoding, detection.Confidence, text, content.Length );
                }
                catch {
                    // 許容内でもデコードできなければバイナリに倒す
                    return new FileContentInfo( true, null, detection.Confidence, null, content.Length );
                }
            }
        }

        // 7) 厳格 UTF-8 デコード（ASCII含む）
        if(TryDecodeUtf8Strict( content, out var utf8Text )) {
            // すべて 0x00–0x7F なら表示上 ASCII に寄せる
            var allAscii = content.All(b => b <= 0x7F);
            var enc = allAscii ? Encoding.ASCII : Encoding.UTF8;
            var conf = Math.Max(0.40, detection?.Confidence ?? 0.0);
            return new FileContentInfo( false, enc, conf, utf8Text, content.Length );
        }

        // 8) 最終的なバイナリ判定
        if(binaryScore >= 0.40) return new FileContentInfo( true, null, detection?.Confidence ?? 0.0, null, content.Length );

        // 規定外はすべてバイナリ
        return new FileContentInfo( true, null, detection?.Confidence ?? 0.0, null, content.Length );
    }

    /// <summary>
    /// BOM によるエンコード検出（UTF-8/UTF-16LE/UTF-16BE のみ）。
    /// </summary>
    /// <param name="bytes">入力バイト列。</param>
    /// <param name="encoding">検出したエンコード。未検出時は null。</param>
    /// <returns>検出に成功したら true。</returns>
    private static bool TryGetBomEncoding( ReadOnlySpan<byte> bytes, out Encoding? encoding ) {
        switch(bytes) {
            // 3-byte BOM (UTF-8)
            case [0xEF, 0xBB, 0xBF, ..]:
                encoding = new UTF8Encoding( encoderShouldEmitUTF8Identifier: true );
                return true;

            // 2-byte BOM (UTF-16 LE/BE)
            case [0xFF, 0xFE, ..]:
                encoding = Encoding.Unicode; // UTF-16LE
                return true;
            case [0xFE, 0xFF, ..]:
                encoding = Encoding.BigEndianUnicode; // UTF-16BE
                return true;

            default:
                encoding = null;
                return false;
        }
    }

    /// <summary>
    /// 先頭が UTF-32 BOM（LE/BE）かどうかを判定する。
    /// </summary>
    /// <param name="bytes">入力バイト列。</param>
    /// <returns>UTF-32 BOM を持つ場合は true。</returns>
    private static bool HasUtf32Bom( ReadOnlySpan<byte> bytes ) {
        return bytes switch
        {
        [0xFF, 0xFE, 0x00, 0x00, ..] => true, // UTF-32LE BOM
        [0x00, 0x00, 0xFE, 0xFF, ..] => true, // UTF-32BE BOM
            _ => false
        };
    }

    /// <summary>
    /// UTF-16LE（BOMなし）らしさをヒューリスティックで判定する。
    /// </summary>
    /// <param name="bytes">入力バイト列。</param>
    /// <param name="encoding">UTF-16LE エンコード。</param>
    /// <param name="confidence">推測信頼度。</param>
    /// <returns>UTF-16LE と推定できれば true。</returns>
    private static bool TryGuessUtf16LeWithoutBom( ReadOnlySpan<byte> bytes, out Encoding encoding, out double confidence ) {
        encoding = null!;
        confidence = 0.0;
        if((bytes.Length & 1) == 1 || bytes.Length < 4) return false;

        var sampleBytes = Math.Min(bytes.Length, 128);
        var pairs = sampleBytes / 2;
        var oddZero = 0;   // LE: 奇数側が上位（NUL が出やすい）
        var evenZero = 0;
        var asciiPairs = 0;

        for(var i = 0; i < pairs; i++) {
            var lo = bytes[i * 2];         // 下位
            var hi = bytes[(i * 2) + 1];   // 上位
            if(hi == 0x00) oddZero++;
            if(lo == 0x00) evenZero++;

            var loAscii = lo is 0x09 or 0x0A or 0x0D || (lo >= 0x20 && lo <= 0x7E);
            if(hi == 0x00 && loAscii) asciiPairs++;
        }

        var oddZeroRatio = (double)oddZero / pairs;
        var asciiPairRatio = (double)asciiPairs / pairs;

        var looksUtf16Le = oddZeroRatio >= 0.55 && asciiPairRatio >= 0.45 && evenZero <= 2;
        if(!looksUtf16Le) return false;

        confidence = Math.Clamp( 0.4 + (0.3 * oddZeroRatio) + (0.3 * asciiPairRatio), 0.35, 0.95 );
        encoding = Encoding.Unicode;
        return true;
    }

    /// <summary>
    /// UTF-16BE（BOMなし）らしさをヒューリスティックで判定する。
    /// </summary>
    /// <param name="bytes">入力バイト列。</param>
    /// <param name="encoding">UTF-16BE エンコード。</param>
    /// <param name="confidence">推測信頼度。</param>
    /// <returns>UTF-16BE と推定できれば true。</returns>
    private static bool TryGuessUtf16BeWithoutBom( ReadOnlySpan<byte> bytes, out Encoding encoding, out double confidence ) {
        encoding = null!;
        confidence = 0.0;
        if((bytes.Length & 1) == 1 || bytes.Length < 4) return false;

        var sampleBytes = Math.Min(bytes.Length, 128);
        var pairs = sampleBytes / 2;
        var evenZero = 0; // BE: 偶数側が上位（NUL が出やすい）
        var oddZero = 0;
        var asciiPairs = 0;

        for(var i = 0; i < pairs; i++) {
            var hi = bytes[i * 2];         // 上位
            var lo = bytes[(i * 2) + 1];   // 下位
            if(hi == 0x00) evenZero++;
            if(lo == 0x00) oddZero++;

            var loAscii = lo is 0x09 or 0x0A or 0x0D || (lo >= 0x20 && lo <= 0x7E);
            if(hi == 0x00 && loAscii) asciiPairs++;
        }

        var evenZeroRatio = (double)evenZero / pairs;
        var asciiPairRatio = (double)asciiPairs / pairs;

        var looksUtf16Be = evenZeroRatio >= 0.55 && asciiPairRatio >= 0.45 && oddZero <= 2;
        if(!looksUtf16Be) return false;

        confidence = Math.Clamp( 0.4 + (0.3 * evenZeroRatio) + (0.3 * asciiPairRatio), 0.35, 0.95 );
        encoding = Encoding.BigEndianUnicode;
        return true;
    }

    /// <summary>
    /// 厳格な UTF-8 デコードを試みる。無効バイトを含む場合は失敗する。
    /// </summary>
    /// <param name="bytes">入力バイト列。</param>
    /// <param name="text">成功時の文字列。</param>
    /// <returns>成功すれば true。</returns>
    private static bool TryDecodeUtf8Strict( ReadOnlySpan<byte> bytes, out string text ) {
        var enc = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        try {
            text = enc.GetString( bytes );
            return true;
        }
        catch {
            text = string.Empty;
            return false;
        }
    }

    /// <summary>
    /// 例外フォールバックで厳格にデコードする。
    /// </summary>
    /// <param name="bytes">入力バイト列。</param>
    /// <param name="encoding">使用するエンコード。</param>
    /// <returns>デコードされた文字列。</returns>
    private static string SafeDecode( ReadOnlySpan<byte> bytes, Encoding encoding ) {
        var enc = (Encoding)encoding.Clone();
        enc.DecoderFallback = DecoderFallback.ExceptionFallback;
        return enc.GetString( bytes );
    }

    /// <summary>
    /// バイナリっぽさを 0.0〜1.0 でスコア化する。
    /// </summary>
    /// <param name="bytes">入力バイト列。</param>
    /// <returns>スコア（高いほどバイナリらしい）。</returns>
    private static double ComputeBinaryScore( ReadOnlySpan<byte> bytes ) {
        var len = bytes.Length;
        int nul = 0, ctrl = 0, longestNonTextRun = 0, currentRun = 0;

        for(var i = 0; i < len; i++) {
            var b = bytes[i];
            var isLikelyText =
                b is >= 0x20 and <= 0x7E ||
                b is (byte)'\r' or (byte)'\n' or (byte)'\t';

            if(!isLikelyText) {
                currentRun++;
                if(currentRun > longestNonTextRun) longestNonTextRun = currentRun;
            }
            else {
                currentRun = 0;
            }

            if(b == 0x00) nul++;
            if(b < 0x20 && b is not ((byte)'\r') and not ((byte)'\n') and not ((byte)'\t'))
                ctrl++;
        }

        var nulRate = (double)nul / len;
        var ctrlRate = (double)ctrl / len;
        var runRate = (double)longestNonTextRun / len;

        var score = (nulRate * 0.6) + (ctrlRate * 0.3) + (runRate * 0.1);
        return Math.Clamp( score, 0.0, 1.0 );
    }

    /// <summary>
    /// WebName を正規化する。小文字化し、アンダースコアをハイフンに置換する。
    /// </summary>
    /// <param name="webName">Encoding.WebName。</param>
    /// <returns>正規化済み名前。</returns>
    private static string NormalizeName( string webName ) => webName.Replace( '_', '-' ).ToLowerInvariant();

    /// <summary>
    /// 許可するエンコードの WebName（正規化後）の集合。
    /// ANSI/Default/UTF-32/UTF-7 は含めない。
    /// </summary>
    private static readonly HashSet<string> AllowedEncodings = new(StringComparer.OrdinalIgnoreCase)
    {
        // Unicode 系
        "utf-8", "utf-16", "utf-16be",

        // 日本語
        "shift-jis", "shift_jis", "euc-jp", "iso-2022-jp",

        // ASCII
        "us-ascii"
    };
}