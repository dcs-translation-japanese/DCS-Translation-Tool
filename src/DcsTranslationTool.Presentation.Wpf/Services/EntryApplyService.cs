using System.IO;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Features.CoachMarks;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;

namespace DcsTranslationTool.Presentation.Wpf.Services;

/// <summary>
/// 翻訳エントリ適用処理を提供する。
/// </summary>
public sealed class EntryApplyService(
    ILoggingService logger,
    IPathSafetyGuard pathSafetyGuard,
    IZipService zipService,
    CoachMarkService? coachMarks = null
) : IEntryApplyService {
    /// <inheritdoc/>
    public async Task<bool> ApplyEntriesAsync(
        IReadOnlyList<IFileEntryViewModel> targetEntries,
        string rootFullPath,
        string rootWithSeparator,
        string translateFullPath,
        string translateRootWithSeparator,
        Func<string, Task> showSnackbarAsync,
        Func<double, Task> progressCallback,
        CancellationToken cancellationToken = default
    ) {
        var injectedMiz = false;
        var progressed = 0;
        var success = 0;
        var failed = 0;
        if(targetEntries.Count == 0) {
            await progressCallback( 100 );
            await showSnackbarAsync( "適用完了 成功:0 件 失敗:0 件" );
            logger.Info( "適用処理が完了した。成功=0, 失敗=0" );
            return true;
        }

        foreach(var entry in targetEntries) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                string? snackbarMessage = null;

                if(!pathSafetyGuard.TryResolvePathWithinRoot( translateFullPath, translateRootWithSeparator, entry.Path, out var sourceFilePath )) {
                    failed++;
                    snackbarMessage = $"不正な翻訳ファイル: {entry.Path}";
                    logger.Warn( $"翻訳ディレクトリ外のファイルが指定されたためスキップする。Path={entry.Path}" );
                }
                else if(!File.Exists( sourceFilePath )) {
                    failed++;
                    snackbarMessage = $"翻訳ファイルが見つかりません: {entry.Path}";
                    logger.Warn( $"翻訳ファイルが存在しないため適用できない。Path={sourceFilePath}" );
                }
                else {
                    var parts = entry.Path.Split( '/', StringSplitOptions.RemoveEmptyEntries );
                    var archiveIndex = Array.FindIndex( parts, pathSafetyGuard.IsZipLikeEntrySegment );
                    var rootSkipCount = pathSafetyGuard.GetRootSegmentSkipCount( parts );

                    if(archiveIndex == -1) {
                        var relativeSegments = parts.Skip( rootSkipCount ).ToArray();
                        if(relativeSegments.Length == 0) {
                            failed++;
                            snackbarMessage = $"不正なパス構造: {entry.Path}";
                            logger.Warn( $"ZIP対象拡張子を含まないエントリの相対パスが空のため適用できない。Path={entry.Path}" );
                        }
                        else {
                            var relativePath = string.Join( '/', relativeSegments );
                            if(!pathSafetyGuard.TryResolvePathWithinRoot( rootFullPath, rootWithSeparator, relativePath, out var destinationPath )) {
                                failed++;
                                snackbarMessage = $"不正な適用先: {entry.Path}";
                                logger.Warn( $"ZIP対象拡張子を含まないエントリがルート外を指しているため拒否した。Entry={entry.Path}, Relative={relativePath}" );
                            }
                            else {
                                try {
                                    var directoryName = Path.GetDirectoryName( destinationPath );
                                    if(!string.IsNullOrEmpty( directoryName )) {
                                        Directory.CreateDirectory( directoryName );
                                    }
                                    File.Copy( sourceFilePath, destinationPath, overwrite: true );
                                    logger.Info( $"ZIP対象拡張子を含まないエントリを直接保存した。Destination={destinationPath}" );
                                    success++;
                                }
                                catch(Exception copyEx) {
                                    failed++;
                                    snackbarMessage = $"適用失敗: {entry.Path}";
                                    logger.Error( $"ZIP対象拡張子を含まないエントリの保存に失敗した。Entry={entry.Path}, Destination={destinationPath}", copyEx );
                                }
                            }
                        }
                    }
                    else {
                        var archiveSegments = parts.Take( archiveIndex + 1 ).Skip( rootSkipCount ).ToArray();
                        if(archiveSegments.Length == 0) {
                            failed++;
                            snackbarMessage = $"不正なパス構造: {entry.Path}";
                            logger.Warn( $"パス構造が不正のため適用に失敗した。Path={entry.Path}" );
                        }
                        else {
                            var archiveRelativePath = string.Join( '/', archiveSegments );
                            if(!pathSafetyGuard.TryResolvePathWithinRoot( rootFullPath, rootWithSeparator, archiveRelativePath, out var archivePath )) {
                                failed++;
                                snackbarMessage = $"不正な適用先: {entry.Path}";
                                logger.Warn( $"適用先がルート外を指しているため拒否した。Entry={entry.Path}, ArchiveRelative={archiveRelativePath}" );
                            }
                            else if(!File.Exists( archivePath )) {
                                failed++;
                                snackbarMessage = $"圧縮ファイルが存在しません: {entry.Path}";
                                logger.Warn( $"適用先の圧縮ファイルが存在しない。ArchivePath={archivePath}" );
                            }
                            else {
                                var entryPathSegments = parts.Skip( archiveIndex + 1 ).ToArray();
                                if(entryPathSegments.Length == 0) {
                                    failed++;
                                    snackbarMessage = $"圧縮ファイル内パスが不正です: {entry.Path}";
                                    logger.Warn( $"miz/trk 内のパスが空のため適用できない。Path={entry.Path}" );
                                }
                                else {
                                    var entryPath = string.Join( '/', entryPathSegments );
                                    var addResult = zipService.AddEntry( archivePath, entryPath, sourceFilePath );
                                    if(addResult.IsFailed) {
                                        failed++;
                                        snackbarMessage = $"適用失敗: {entry.Path}";
                                        var reason = string.Join( ", ", addResult.Errors.Select( e => e.Message ) );
                                        logger.Warn( $"圧縮ファイルへの適用に失敗した。ArchivePath={archivePath}, EntryPath={entryPath}, Reason={reason}" );
                                    }
                                    else {
                                        logger.Info( $"圧縮ファイルへ適用した。ArchivePath={archivePath}, EntryPath={entryPath}" );
                                        injectedMiz |= Path.GetExtension( archivePath ).Equals( ".miz", StringComparison.OrdinalIgnoreCase );
                                        success++;
                                    }
                                }
                            }
                        }
                    }
                }

                if(snackbarMessage is not null) {
                    await showSnackbarAsync( snackbarMessage );
                }
            }
            catch(Exception ex) {
                failed++;
                logger.Error( $"適用処理で例外が発生した。Path={entry.Path}", ex );
                await showSnackbarAsync( $"適用失敗: {entry.Path}" );
            }

            progressed++;
            await progressCallback( Math.Min( 100, (double)progressed / targetEntries.Count * 100 ) );
        }

        coachMarks?.RecordApplyResult( injectedMiz, failed > 0 );
        await progressCallback( 100 );
        await showSnackbarAsync( $"適用完了 成功:{success} 件 失敗:{failed} 件" );
        logger.Info( $"適用処理が完了した。成功={success}, 失敗={failed}" );
        return true;
    }
}