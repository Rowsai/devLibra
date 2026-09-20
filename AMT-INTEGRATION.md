# devLibra / Auto Make Timeline integration

This local build is based on devLibra 1.0.0.1 from the user's supplied devLibra-v1.0.0.1-dot-text-style-source.zip. It preserves the Barrier HP calculation and publishes the exact same calculated Barrier value to Auto Make Timeline through IPC. The value is an estimate when the underlying devLibra calculation is an estimate; it is not the amount absorbed by one attack.

## 導入

既存devLibraを無効にし、同名プラグインを二重読み込みせず、このZIPを固定フォルダへ展開してDalamudの開発プラグインとして読み込んでください。既存の設定はそのまま利用します。AMTの「連携プラグイン」タブで「接続済み」を確認してください。

Barrier HPタブに「Auto Make Timeline: Barrier HP IPC v1」と表示される版です。devLibraのウィンドウを開いておく必要はありません。パーティリストが更新される状態が必要です。HP表示加算のチェックとIPCは独立しています。

バージョンはベースと同じ1.0.0.1です。この連携ビルドをGitHubには公開していません。元のdevLibra作業フォルダ・リポジトリへの変更も行っていません。将来公式配布版で上書きすると連携機能がなくなる場合があります。

## Contract v1

- `devLibra.BarrierHP.Version`: no arguments, returns integer 1.
- `devLibra.BarrierHP.SnapshotV1`: uint entity ID, returns JSON or an empty string if no fresh sample exists.
- Fields: Version, EntityId, CurrentHp, MaxHp, ShieldPercent, BarrierHp, CalculationSource, SampledAtMilliseconds, IsReplay.
- Samples are published from the same party-list update as the Barrier HP debug view, keyed by entity ID. No slot-based guessing. Timestamp uses Environment.TickCount64. Stale samples expire after 500ms. Calls are synchronous and intended on the Dalamud/game UI thread.
- Both call gates are unregistered on plugin disposal. The party-list finalize clears samples.

The source.zip supplied with AMT includes this source tree. Build using `dotnet build devLibra.csproj -c Release`, or the adjacent AutoMakeTimeline/build.ps1 to package both plugins. API15 / .NET10. Live IPC, native replay, and game UI verification has not been performed. Five inherited warnings remain in the base source.

Supplied DoT styling / party sort / target markers and PvP feature gates are retained. Original archive was not changed. Baseline regression tests: 73,964 sort assertions and 57 DoT assertions passed.
