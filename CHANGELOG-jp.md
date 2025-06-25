# Changelog

## [Unreleased]
### Added
- 汎用的なアニメーションに対応（破壊的変更）
  - マルチフレームアニメーション・ループアニメーションに対応。
- Animation Expressionによる任意アニメーションの再生に対応。
- Motion Timeに対応。
- ネストされたConditionがAND条件として動作するように対応。
- MA MenuItem（Radial）はMotion Timeとして動作するように対応。

### Changed
- Expressionの結合はMerge Expressionで明示的に行うように変更。
- MA MenuItem（Toggle/Button）はOR条件として動作するように変更。
- いずれの条件にも紐づかないExpressionも許可するように変更。
- Prefabを更新。
- GC負荷を軽減。

### Deprecated

### Removed

### Fixed
- 毎フレームNDMF Previewが更新されていた問題を修正。
- 不要なTracking用レイヤーが生成されないように修正。
- floatのParameter Conditionが正常に動作していなかった問題を修正
- アバターの初期化時にTracking Controlが正常に適用されていなかった問題を修正

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