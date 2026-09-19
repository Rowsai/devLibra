# Change Party Icons 移植版

元ファイル: devLibra-main.zip / v0.0.0.23

移植版: devLibra v0.0.0.24 / Dalamud API15

## 導入

1. devLibra と単体版 Change Party Icons を無効化します。同じジョブアイコンを書き換えるため、単体版は併用しないでください。
2. `devLibra-v0.0.0.24-ChangePartyIcons.zip` を専用フォルダーに展開します。
3. `/xlsettings` の開発用プラグイン設定（Dev Plugin Locations）に、展開した `devLibra.dll` のフルパスを登録します。既にdevLibraを開発用パスで読み込んでいる場合は、そのパスを更新します。
4. `/xlplugins` の開発用プラグイン一覧からdevLibraを有効にします。
5. `/devlibra` → `Change Party Icons` タブ → 「有効」で切り替えます。

既存devLibraの設定項目を保持し、新しい `ChangePartyIconsEnabled` を追加しました。初期値はtrueです。無効化は次のパーティリスト描画時に反映されます。

## 変更したファイル

- `PartyListTargetMarkerDisplay.cs`: マーカー判定、HUD表示行との対応付け、アイコン変更・復元、破棄時のキャッシュ解除。
- `Plugin.cs`: 表示処理の生成、PreDraw / PreFinalize の登録・解除、アンロード時の復元。
- `Configuration.cs`: 有効設定の追加。
- `Windows/MainWindow.cs`: チェックボックス1個だけのタブ追加。
- `devLibra.csproj` / `devLibra.json`: バージョンを0.0.0.24へ更新。
- `README.md`: 機能とローカル版の説明追加。

既存のHP表示・PartySearch処理は変更していません。新しい表示処理はウィンドウの開閉に関係なく動作します。ターゲットマーカーそのものの付与・解除は行いません。

## ビルドと検証

```powershell
dotnet build devLibra.csproj -c Release
```

.NET 10 SDKとDalamud API15開発用DLLが必要です。標準参照先は `%APPDATA%\XIVLauncher\addon\Hooks\dev`、出力先は `bin\Release` です。

2026-09-19: 移植版と元ソースの両方を同じ環境でReleaseビルドしました。どちらもエラー0、警告5でした。既存のContentId廃止予定警告2件、null参照の可能性に関する警告3件です。今回追加したコードにはビルド警告がありません。

ゲーム内の動作確認は未実施です。実機では次を確認してください。

- 自分と他メンバーに17種のマーカーを付与し、対応する行のアイコンに反映されること。
- 変更、解除、別メンバーへの移動に追従すること。
- 有効チェック解除、アンロードでジョブアイコンに戻ること。
- パーティの並び替え、加入・脱退、ジョブ変更、エリア移動、HUD再読み込みに追従すること。
- Barrier HPを同時に有効にしても、HP表示とマーカー表示がそれぞれ動作すること。
- 再起動後も有効設定が保持されること。
