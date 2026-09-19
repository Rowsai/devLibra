# devLibra v0.0.0.25: パーティのマーカー順ソート

## コマンド

| コマンド | 結果 |
| --- | --- |
| `/sort tm asc` | 攻撃1、2、3、4、5、6、7、8、足止め1、2、3、禁止1、2 |
| `/sort tm desc` | 禁止2、1、足止め3、2、1、攻撃8、7、6、5、4、3、2、1 |
| `/sort tm original` | 保存した独自の優先順位。降順を選ぶと逆順 |
| `/sort default` | ゲーム標準の `/partysort` を実行し、ゲーム側の設定で並び替え |

各コマンドは実行時に1回だけ適用します。マーカーの付け替え後は再実行してください。アイコン変更機能の有効／無効とは独立して使用できます。

通常のパーティリストが対象です。ゲームの通常並び替えに合わせて自分の先頭行は固定します。マーカーなし、四角・丸・バツ・三角のメンバーは最終的な表示位置を維持し、残った対象行の間で並び替えます。禁止マーカーは1・2の2種類です。

例: 自分 / 禁止2 / マーカーなし / 攻撃1 の状態でascを実行すると、自分 / 攻撃1 / マーカーなし / 禁止2 になります。

ゲーム側の制限で並び替えできない状態では適用できません。通常パーティリストを取得できない場合や、並び替え結果を確認できない場合はローカルのエラーチャットを表示します。他のプラグインが `/sort` を既に登録している場合もエラーを表示します。

## オリジナル順

1. `/devlibra` → `Change Party Icons` を開きます。
2. 「オリジナルのソート順を有効化」をチェックします。
3. 表示された13種類のマーカーをドラッグ＆ドロップで並べ替えます。
4. 「並び順」で昇順（上から順）または降順（下から順）を選びます。
5. `/sort tm original` で適用します。

設定変更は自動保存します。無効化すると順序と方向の設定項目は非表示になり、保存済みの順序は保持します。無効時にコマンドを実行すると、指定どおり次の文字列を表示します。

```text
[devLibra]オリジナルのソート順が有効化されていません。
```

## 導入・ビルド

プラグイン名とDLL名は `devLibra`、配布資材名は `latest.zip` です。既存版を無効化してから展開先のDLLを更新し、再度有効にしてください。単体版 Change Party Icons は無効にしておいてください。

```powershell
./build-release.ps1
dotnet run --project tests/SortTests.csproj -c Release
```

`build-release.ps1` は `bin/Release/latest.zip` を生成します。中身は `devLibra.dll`、`devLibra.json`、`devLibra.deps.json` です。

## 検証

- Dalamud API15 / .NET 10でReleaseビルド成功。既存コード由来の警告5件、エラー0件。
- コマンド解釈、13種類の優先順位、昇順・降順・独自順、設定欠損や重複の補正、対象外行の保持を自動テスト。
- 7人分の全5,040通りの順列で、隣接交換が目標順に到達することを検証。
- マーカーのランダムな組み合わせ1,000ケースを検証。合計73,964アサーション成功。
- ネイティブ操作はFramework.Updateで実行し、並び替え後のHUD順序を最大1.5秒確認します。自動再試行はしません。
- ゲーム内の実動作・ドラッグ操作・標準順の適用は未検証です。各コマンド、オリジナル設定の保存と無効時エラー、マーカーのないメンバーが混在するパーティ、エリア移動後の動作を実機確認してください。

## 参照

- [FFXIV公式 /partysort](https://jp.finalfantasyxiv.com/lodestone/playguide/db/text_command/d41591d4b4f/)
- [FFXIVClientStructs InfoProxyPartyMember.ChangeOrder](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/Info/InfoProxyPartyMember.cs)
- [FFXIVClientStructs AgentHUD](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/Agent/AgentHUD.cs)
