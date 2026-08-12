# devLibra

![devLibra icon](image/icon.png)

`devLibra` は、FINAL FANTASY XIV の Dalamud プラグイン開発を支援するユーティリティです。

## 主な機能

### Barrier HP

- パーティリストの現在 HP 表示にバリア量を加算して表示します。
- バリア量を含む HP 数値は緑色で表示します。
- 鼓舞激励の策のバリアは、付与時に観測した回復量の 180% から算出します。
- 戦闘外では、現在 HP・最大 HP・バリア量・計算結果・計算元などを Barrier HP タブで確認できます。
- デバッグ情報は戦闘中には表示しません。

### PartySearch

- 自キャラ以外の100m以内にいる「コンテンツ参加中」アイコンのプレイヤーをソロ対象として扱います。戦闘中は無効です。
- PartySearch タブから、対象のローカル表示名・名前色・自キャラから引くターゲット線の色と太さを設定できます。

### 開発支援タブ

- PartyMember: ObjectTable 上のプレイヤー情報とステータスを確認します。
- EnemyCasting: 敵のキャスト情報を確認します。
- EnemyStatus: 敵に付与されているステータスを確認します。
- StatusSearch: ステータスを名前または ID で検索します。
- ActionSearch: アクションを名前または ID で検索します。

## 設定を開く

ゲーム内で `/devlibra` を実行するか、Dalamud のプラグイン設定画面から開きます。

## カスタムリポジトリ

Dalamud のカスタムプラグインリポジトリに次の URL を追加してください。

```text
https://raw.githubusercontent.com/Rowsai/Rowsai-Plugins/refs/heads/main/pluginmaster.json
```

## 最新版

現在の配布バージョンは **v0.0.0.21** です。配布ファイルと更新履歴は [Releases](https://github.com/Rowsai/devLibra/releases) から確認できます。
