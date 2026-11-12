# CONTRIBUTING

このリポジトリへの貢献に関心をお寄せいただきありがとうございます。以下のガイドラインに従って、開発・レビュー・リリースに参加してください。

---

## 方針の要約（重要）

- **リポジトリフロー:** GitHub Flow
- **リポジトリ構成:** 単一リポジトリ（モノレポ）
- **デプロイ対象:** `master` ブランチのみ（常にデプロイ可能状態）
- **マージ方式:** **Squash and Merge** をデフォルト
- **コミット/PRタイトル:** [Conventional Commits](https://www.conventionalcommits.org/ja/v1.0.0/) 準拠
- **`master` への取り込み手順:**
  1. 作業ブランチを作成
  2. 作業ブランチにコミット
  3. Pull Request (PR) を作成
  4. テスト・CI・レビューをクリア
  5. `master` へ **Squash Merge**

---

## 1. 開発環境

| 項目                     | 必要なバージョン / 内容                     |
| ------------------------ | ------------------------------------------- |
| .NET SDK                 | 11.0.x                                      |
| IDE                      | Visual Studio 2026 18.12以降                |
| ワークロード             | .NET デスクトップ開発                       |
| ターゲットフレームワーク | `net11.0` 系（WPF は `net11.0-windows`）    |
| C# 言語バージョン        | 15                                          |
| 開発OS                   | .NET 11がサポートするWindows(x64)           |
| シェル                   | PowerShell 7（`pwsh`、アプリ更新E2Eで使用） |

配布対象は`win-x64`・`win-x86`の自己完結型アプリです。対応OSは[.NET 11の対応Windows一覧](https://github.com/dotnet/core/blob/main/release-notes/11.0/supported-os.md#windows)に従い、Windows自体のサポート期間内に限ります。

## 2. Issue と機能要望

- バグ報告や機能提案は [Issues](../../../issues) に登録してください。
- 再現手順、環境、期待動作を明確に記載してください。

## 3. ブランチ運用 (GitHub Flow)

- `master`: 常にデプロイ可能な状態。
- 新機能・修正は `master` から短命ブランチを作成 (`feature/短い説明` や `fix/短い説明`)。
- 作業完了後、Pull Request を作成しレビューを受ける。
- CI が通過しレビュー承認後、`master` にマージ。
- マージ後、必要に応じて即時デプロイ。

## 4. コーディング規約

- **.editorconfig** に従ってください。
- 命名規則: [Microsoft C# ガイドライン](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/identifier-names) に準拠。
- 可能な限り **nullable reference types** を有効化。
- 非同期コードは `async/await` を使用。
- 公開APIには XML ドキュメントコメントを付与。

## 5. コミットメッセージ

- [Conventional Commits](https://www.conventionalcommits.org/ja/v1.0.0/) に従ってください。
- 形式: `<type>[optional scope]: <description>`
  - type: `feat`, `fix`, `release`, `docs`, `chore`, `refactor`, `perf`, `test`, `build`, `ci`, `style`, `revert`
  - 例: `fix: NullReferenceException in MainWindow`

## 6. ビルドとテスト

リポジトリルートの PowerShell 7 で実行します。

```powershell
dotnet restore DCS-Translation-Tool.sln --locked-mode
dotnet build DCS-Translation-Tool.sln --configuration Release --no-restore
dotnet test DCS-Translation-Tool.sln --configuration Release --no-build --no-restore
```

ソリューションのテストには `tests/` 配下の単体テストと結合テストが含まれます。自動更新機能のE2Eテストはソリューションに含まれないため、別途実行します。

### 自動更新機能のテスト

Windows の対話型デスクトップにサインインした状態で実行します。FlaUI がアプリのウィンドウを操作するため、実行中はデスクトップをロックせず、対象ウィンドウを手動操作しないでください。

```powershell
./build/Invoke-ApplicationUpdateE2e.ps1 -RuntimeIdentifier win-x64
./build/Invoke-ApplicationUpdateE2e.ps1 -RuntimeIdentifier win-x86
```

このスクリプトは自己完結型アプリとテスト用更新アセットを生成し、E2E テストに必要な環境変数を設定します。E2E プロジェクトへの `dotnet test` 単独実行では代用できません。生成物・TRX・失敗時の診断情報は `artifacts/e2e/<RID>/` に保存され、同じ RID で再実行するとその出力先は作り直されます。

新機能またはバグ修正には 単体テスト を必ず追加。

UI変更を含む場合はスクリーンショットを PR に添付。

## 7. Pull Request

Pull Request は release-please に従って作成します。

squash-commit として main に取り込まれます。

PR本文に「目的」「変更内容」「テスト方法」を明記してください。

> [!NOTE]
> **PR タイトル** は Squash 時にコミットメッセージとして採用されるため、Conventional Commits に準拠してください。

## 8. ライセンス

提供コードは、このプロジェクトの [LICENSE](../LICENSE) に従います。
