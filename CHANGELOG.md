# Changelog

## [2.2.0](https://github.com/dcs-translation-japanese/DCS-Translation-Tool/compare/v2.1.0...v2.2.0) (2026-10-29)


### Features

* **update:** アプリ内差分アップデートに対応 ([#159](https://github.com/dcs-translation-japanese/DCS-Translation-Tool/issues/159)) ([b0e9e55](https://github.com/dcs-translation-japanese/DCS-Translation-Tool/commit/b0e9e5532b56ccfe612b00ed01af40219e0bf2ca))


### Bug Fixes

* Bump coverlet.collector from 8.0.1 to 10.0.1 ([#133](https://github.com/dcs-translation-japanese/DCS-Translation-Tool/issues/133)) ([abea0cd](https://github.com/dcs-translation-japanese/DCS-Translation-Tool/commit/abea0cda3f855cc5a9c47ab9de0053934135f9e4))
* Bump Microsoft.Kiota.Abstractions and Microsoft.Kiota.Http.HttpClientLibrary ([#126](https://github.com/dcs-translation-japanese/DCS-Translation-Tool/issues/126)) ([c9e23bd](https://github.com/dcs-translation-japanese/DCS-Translation-Tool/commit/c9e23bd8ae306bafa2f9bdf0d809b79ebdc2d3ee))
* Toolの参照先と更新テストをOrg構成へ変更する ([#189](https://github.com/dcs-translation-japanese/DCS-Translation-Tool/issues/189)) ([604bcff](https://github.com/dcs-translation-japanese/DCS-Translation-Tool/commit/604bcff1a0b4eff458aa4bfb8488f1724612a998))

## [2.1.0](https://github.com/5kdn/DCS-Translation-Tool/compare/v2.0.0...v2.1.0) (2026-04-18)


### Features

* **download:** Download 適用時に差分ファイルごとの適用元を選択できるようにする ([#112](https://github.com/5kdn/DCS-Translation-Tool/issues/112)) ([1dc28f2](https://github.com/5kdn/DCS-Translation-Tool/commit/1dc28f274e25ce138aab5bb987284be73ec87875))


### Bug Fixes

* Bump the testing group with 2 updates ([#101](https://github.com/5kdn/DCS-Translation-Tool/issues/101)) ([e1a69f4](https://github.com/5kdn/DCS-Translation-Tool/commit/e1a69f40d50e5a350098b6c1dfad8acd6dc0bdd6))
* **translation:** 詳細テキストボックスでもCtrl+矢印で選択移動できるように修正 ([#108](https://github.com/5kdn/DCS-Translation-Tool/issues/108)) ([077c387](https://github.com/5kdn/DCS-Translation-Tool/commit/077c3874f695903771ce2017620166c1b289dedf))
* **wpf:** WindowChromeBehaviorを共通化 ([#110](https://github.com/5kdn/DCS-Translation-Tool/issues/110)) ([d308bd7](https://github.com/5kdn/DCS-Translation-Tool/commit/d308bd72e2f67f8b1d6a5851ef31c01c4e758159))
* 翻訳ファイルの出力形式をUTF-8 BOM無しに変更 ([#105](https://github.com/5kdn/DCS-Translation-Tool/issues/105)) ([52a54ed](https://github.com/5kdn/DCS-Translation-Tool/commit/52a54edf52ce6e0417beb8e702a4acb8bd2e01b2))

## [2.0.0](https://github.com/5kdn/DCS-Translation-Tool/compare/v1.4.0...v2.0.0) (2026-03-24)


### ⚠ BREAKING CHANGES

* **settings:** SourceAircraftDir と SourceDlcCampaignDir を廃止し、DcsWorldInstallDir と外部注入先設定へ移行する

### Features

* **settings:** ゲームインストール基準の注入先設定に対応 ([#82](https://github.com/5kdn/DCS-Translation-Tool/issues/82)) ([6307af2](https://github.com/5kdn/DCS-Translation-Tool/commit/6307af2714b01c6e28d57d3d5f5474324578fabf))
* **translation:** 翻訳作成画面を追加 ([#88](https://github.com/5kdn/DCS-Translation-Tool/issues/88)) ([ab269eb](https://github.com/5kdn/DCS-Translation-Tool/commit/ab269eb51914c12242fe876471abdc83ab343906))
* **upload:** PR作成前のLua構文チェックとエラーダイアログを追加 ([#93](https://github.com/5kdn/DCS-Translation-Tool/issues/93)) ([709eca2](https://github.com/5kdn/DCS-Translation-Tool/commit/709eca2642798641617a28f34cc69566d16c8290))


### Bug Fixes

* Bump the app-core group with 3 updates ([#86](https://github.com/5kdn/DCS-Translation-Tool/issues/86)) ([f46537b](https://github.com/5kdn/DCS-Translation-Tool/commit/f46537b2e613d9f26dd4183e2bad6448f873cd31))
* Bump the kiota group with 6 updates ([#89](https://github.com/5kdn/DCS-Translation-Tool/issues/89)) ([e95f455](https://github.com/5kdn/DCS-Translation-Tool/commit/e95f4555fae60e8c45fcb08f6fd0a48968d923cc))
* Bump the testing group with 2 updates ([#87](https://github.com/5kdn/DCS-Translation-Tool/issues/87)) ([33fc3e9](https://github.com/5kdn/DCS-Translation-Tool/commit/33fc3e9365d0ca02313f76aa3573064a0318e6a1))
* **shell:** Settings遷移時のウィンドウサイズ強制変更を防止 ([#95](https://github.com/5kdn/DCS-Translation-Tool/issues/95)) ([ee2e626](https://github.com/5kdn/DCS-Translation-Tool/commit/ee2e6264227125a7c81e2942fdbea669af9f2ac7))

## [1.4.0](https://github.com/5kdn/DCS-Translation-Tool/compare/v1.3.1...v1.4.0) (2026-02-16)


### Features

* **dialog:** PR作成時に流通制御ポリシー同意を必須化 ([#68](https://github.com/5kdn/DCS-Translation-Tool/issues/68)) ([3a70527](https://github.com/5kdn/DCS-Translation-Tool/commit/3a70527bdfed330b25a3f194826e26a97e3bb3b8))
* **infra:** API経路選択をIPv4/IPv6ヘルスレースとフォールバック対応へ拡張 ([#77](https://github.com/5kdn/DCS-Translation-Tool/issues/77)) ([462f320](https://github.com/5kdn/DCS-Translation-Tool/commit/462f3200f78949f50626f939119513540471c29d))
* **perf:** SHAキャッシュ導入とTree API経路最適化で一覧取得とツリー表示を高速化 ([#66](https://github.com/5kdn/DCS-Translation-Tool/issues/66)) ([e24bce0](https://github.com/5kdn/DCS-Translation-Tool/commit/e24bce0664f10226b773c4945b1e1aee44b7f943))
* **update:** 起動時のGitHub Releases更新確認と通知機能を追加 ([#63](https://github.com/5kdn/DCS-Translation-Tool/issues/63)) ([f291a1c](https://github.com/5kdn/DCS-Translation-Tool/commit/f291a1cbe9ec0a55f7b2352cd9f1b59b433b567f))


### Bug Fixes

* Bump Microsoft.Kiota.Abstractions and Microsoft.Kiota.Serialization.Form ([#41](https://github.com/5kdn/DCS-Translation-Tool/issues/41)) ([da539af](https://github.com/5kdn/DCS-Translation-Tool/commit/da539af23ffac943238133caa4652aacfd0a7308))
* Bump NLog from 6.0.7 to 6.1.0 ([#39](https://github.com/5kdn/DCS-Translation-Tool/issues/39)) ([574b5b8](https://github.com/5kdn/DCS-Translation-Tool/commit/574b5b8d164a39b8e337d2f25296d7a72f5f4702))
* Bump the kiota group with 4 updates ([#75](https://github.com/5kdn/DCS-Translation-Tool/issues/75)) ([5c28ea0](https://github.com/5kdn/DCS-Translation-Tool/commit/5c28ea00098ecd6902e73680398dc7dc3415e9c4))

## [1.3.1](https://github.com/5kdn/DCS-Translation-Tool/compare/v1.3.0...v1.3.1) (2025-12-04)


### Bug Fixes

* CreatePullRequest ダイアログの変更するファイルリストが表示されないバグを修正した ([#20](https://github.com/5kdn/DCS-Translation-Tool/issues/20)) ([0b43963](https://github.com/5kdn/DCS-Translation-Tool/commit/0b43963066ff3faa33897152a26bb6d411a49e36))

## [1.3.0](https://github.com/5kdn/DCS-Translation-Tool/compare/v1.2.0...v1.3.0) (2025-12-03)


### Features

* クライアントサイドでファイルをダウンロードするように変更した ([#16](https://github.com/5kdn/DCS-Translation-Tool/issues/16)) ([085437f](https://github.com/5kdn/DCS-Translation-Tool/commit/085437f64a0411cd1dadb6750acb1f39b1d29f8d))


### Bug Fixes

* ダウンロードURL経由の適用に更新し処理後の状態更新を安定化 ([#19](https://github.com/5kdn/DCS-Translation-Tool/issues/19)) ([ff5772b](https://github.com/5kdn/DCS-Translation-Tool/commit/ff5772bdacb5dc2d53e4b49a02ffdac65df5d96f))
* ファイル監視の多重通知を抑制しダウンロード中の更新漏れを防止 ([#18](https://github.com/5kdn/DCS-Translation-Tool/issues/18)) ([2aaf015](https://github.com/5kdn/DCS-Translation-Tool/commit/2aaf0153e1b2e037f6320fb59eac6a5dbb69d0a1))

## [1.2.0](https://github.com/5kdn/DCS-Translation-Tool/compare/v1.1.0...v1.2.0) (2025-12-01)


### Features

* treat .trk file as zip ([#14](https://github.com/5kdn/DCS-Translation-Tool/issues/14)) ([3ce1d91](https://github.com/5kdn/DCS-Translation-Tool/commit/3ce1d9197081847cbc14e64e8594dfc225b199e5)), closes [#8](https://github.com/5kdn/DCS-Translation-Tool/issues/8)


### Bug Fixes

* change branch prefix to feature/ ([#12](https://github.com/5kdn/DCS-Translation-Tool/issues/12)) ([94e67e4](https://github.com/5kdn/DCS-Translation-Tool/commit/94e67e4deb7eeeee6310158386a72d05e20023d2)), closes [#9](https://github.com/5kdn/DCS-Translation-Tool/issues/9)

## [1.1.0](https://github.com/5kdn/DCS-Translation-Tool/compare/v1.0.0...v1.1.0) (2025-11-19)


### Features

* Apply時 *.miz ディレクトリ配下ではないファイルを直接Source*Dirに保存する機能を追加 ([#2](https://github.com/5kdn/DCS-Translation-Tool/issues/2)) ([6327677](https://github.com/5kdn/DCS-Translation-Tool/commit/632767784d1a62dc12548b2c1308fcedca915500))

## [1.0.0](https://github.com/5kdn/DCS-Translation-Tool/compare/v0.0.1...v1.0.0) (2025-11-17)


### Features

* add ApiService ([b4b1ae8](https://github.com/5kdn/DCS-Translation-Tool/commit/b4b1ae83c1f50e4f8e99e547672136892b322b78))
* add AppSettingsService ([89f76e3](https://github.com/5kdn/DCS-Translation-Tool/commit/89f76e33715546e9192c95ba740c70e4ab5cc0f1))
* add EnvironmentProvider ([632a3ac](https://github.com/5kdn/DCS-Translation-Tool/commit/632a3ace2c813579ed82b24640bf8926946c9579))
* add FileEntryService ([8990e0b](https://github.com/5kdn/DCS-Translation-Tool/commit/8990e0b28f5d3ca776bdfc7492c3b30f9569371d))
* add FileService ([f211415](https://github.com/5kdn/DCS-Translation-Tool/commit/f211415bee7bc83f0dd80cc15d0369b05bebe293))
* add LoggingService ([894a090](https://github.com/5kdn/DCS-Translation-Tool/commit/894a09072acb0fc0a0dd66b02567978605152b47))
* add SnackbarService ([4795fbd](https://github.com/5kdn/DCS-Translation-Tool/commit/4795fbd613b875c4b3e05ce5a9e836301b8f5117))
* add SystemService ([02e05a7](https://github.com/5kdn/DCS-Translation-Tool/commit/02e05a774fb7bf2e9f8b2235e858f85b4c6679de))
* add ZipService ([a3bd69a](https://github.com/5kdn/DCS-Translation-Tool/commit/a3bd69a16c212efff6ff7145832f479c08929a9d))
* implement CreatePullRequestView and ([56a6e72](https://github.com/5kdn/DCS-Translation-Tool/commit/56a6e72948d1cd43aba936bf8573ec9c20601815))
* implement DownloadView and DownloadViewModel ([f23fc10](https://github.com/5kdn/DCS-Translation-Tool/commit/f23fc101e4290b106a8a3dfa5f4b2e965b01c306))
* implement MainView and MainViewModel ([b2f7a2c](https://github.com/5kdn/DCS-Translation-Tool/commit/b2f7a2cbf1d1a74e094d7beb69e2060cb21b451b))
* implement SettingsView and SettingsViewModel ([0b4b1b7](https://github.com/5kdn/DCS-Translation-Tool/commit/0b4b1b72330b7234df8a7de2f1267f89884945a9))
* implement ShellView and ShellViewModel ([b9651fc](https://github.com/5kdn/DCS-Translation-Tool/commit/b9651fcd6767945d43508faae0777937baa85f13))
* implement UploadView and UploadViewModel ([ae71aa8](https://github.com/5kdn/DCS-Translation-Tool/commit/ae71aa805672c0ea2c92e2e74c3d699bf5dd4c4e))


### Miscellaneous Chores

* release 1.0.0 ([41b2bdc](https://github.com/5kdn/DCS-Translation-Tool/commit/41b2bdcd138ae875a574a180c5924c7134b5ddc9))
