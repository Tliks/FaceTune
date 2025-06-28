# Changelog

## [Unreleased]

### FaceTuneのほぼ全てのコンポーネントのデータが失われます。

### Added
- `FT Expression`及び`FT Facial Data`を追加。
  - `FT Facial Expression`から置き換わります。
  - `FT Expression`がアタッチされたGameObject以下の`FT Facial Data`が紐付きます。
  - 同一の`FT Expression`の影響下にある複数の`FT Facial Data`は結合されます。
  - Hierarchyにおいて、下にあるほど優先されます。
- 汎用的なアニメーションに対応。
  - マルチフレームアニメーション・ループアニメーションに対応。
- `FT Animation Data`による任意アニメーションの再生に対応。
  - 基本的な動作は`FT Facial Data`と同一です。
  - 表情用のブレンドシェイプ以外の定義を可能にします。
- Motion Timeに対応。
- ネストされた`FT Condition`がAND条件として動作するように対応。
  - 同一のGameObjectに複数の`FT Condition`がアタッチされた際は引き続きOR条件として動作します。
- MA MenuItem（Radial）はMotion Timeとして動作するように対応。
- `FT Facial Style`を追加。
  - 顔つきを定義するコンポーネントとして、`FT Default Facial Expression`から置き換わります。
  - アタッチされたGameObject以下のExpressionに対し、定義されたブレンドシェイプの適用を行います。
  - また、`AsDefault`が有効な場合、デフォルト表情(追加の条件を満たさないときの表情)としても機能します。
  - デフォルト表情においては設定されていないブレンドシェイプは全て0として扱われ、適用対象のRendererの値は使用されません。
- `Assets/FaceTune/SelectedClipsToExclusiveMenu`にメニューアイテムを追加。
  - 選択された複数のAnimationClipから、排他制御のMenuItemを条件とするExpressionを生成します。
- `GameObject/FaceTune/Import from FX Layer`にメニューアイテムを追加。
  - アバターに現在適用されているFXレイヤーからFaceTuneの設定を追加します。

### Changed
- MA MenuItem（Toggle/Button）はOR条件として動作するように変更。
- いずれの条件にも紐付かないExpressionも許可するように変更。
- Prefabを更新。
- GC負荷を軽減。

### Deprecated

### Removed
- `FT Facial Expression`を削除。
- `FT Default Facial Expression`を削除。

### Fixed
- 毎フレームNDMF Previewが更新されていた問題を修正。
- 不要なTracking用レイヤーが生成されないように修正。
- floatのParameter Conditionが正常に動作していなかった問題を修正。
- アバターの初期化時にTracking Controlが正常に適用されていなかった問題を修正。

### Security

## [0.1.0-beta.1] - 2025-06-03
### Added
- 初回リリース

## [0.1.0-beta.2] - 2025-06-03
### Fixed
- Blendingが正常に動作していなかった問題を修正。

## [0.1.0-beta.3] - 2025-06-14
### Changed
- Prefabを更新。
- FacialShapesEditorの終了時にUndoを単一イベントとしてまとめるように変更。
- ctrl/cmd-sでFacialShapesEditorを保存できるように対応。
- DefaultFacialExpressionComponentにFacialShapesEditorを追加。

### Fixed
- 複数のExpressionが正常に結合されない問題を修正。
- Undoが一部効かない問題を修正。
- メッシュが一部解放されていなかった問題を修正。
- プリセットが正常に動作していなかった問題を修正。