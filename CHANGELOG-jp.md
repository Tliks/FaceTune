# Changelog

## [English](./CHANGELOG.md)

## [Unreleased]
### Added

### Changed
- 表情エディタにおいて、同一のExpression内かつ自分より優先度が高いExpression Dataの内容がベースとして表示されるようになりました。
- UIを調整

### Deprecated

### Removed

### Fixed

### Security

## [0.1.0-beta.13] - 2025-01-04
### Changed
- NDMFの対応バージョンが>=1.9.0に変更

### Fixed
- package.jsonの構文エラーを修正

## [0.1.0-beta.12] - 2025-12-30
### Added
- 右クリックメニューにFXコントローラーの内容がインポートされたテンプレートを生成するメニューを追加

### Changed
- UIを調整

### Fixed
- テンプレートでデフォルト表情の常時プレビューが誤ってOFFになっていた問題を修正

## [0.1.0-beta.11] - 2025-12-26
### Changed
- Animator Controllerのインポート処理を改善
- 一部のコンソールがローカライズ
- NDMFの対応バージョンが>=1.9.0.rc.0に変更

### Fixed
- FacialShapesEditorが更新されない問題を修正
- Inspectorから複数のパラメータを条件としてする際に適切に動作しない問題を修正

## [0.1.0-beta.10] - 2025-08-28
### Fixed
- FacialShapesEditorが開けない問題を修正
- コンパイル時などに、ローカライゼーションが読み込まれない問題を修正

## [0.1.0-beta.9] - 2025-08-28
### Added
- ローカライズ(英語/日本語)を追加しました。 `#80`
- SelectedExpressionPreviewがマルチフレームアニメーションに対応しました。
- FacialStyleコンポーネントの3点メニューに、EditMode上で顔つきをRendererに適用する機能が追加されました。`#89`
- まばたき/リップシンクを強制的に無効化するパラメータが追加されました。
  - これを操作するメニューがテンプレートにデフォルトで含まれるようになります。

### Changed
- Facial Dataコンポーネントの仕様を大きく変更し、Expression Dataコンポーネントへと名称を変更しました。`#82` `#83` `#84` 
  - これまで設定された表情が壊れる可能性があります。その場合はAnimation ClipをNoneにするか、もしくは再設定をしてください。
  - AnimationClip Mode/Manual Modeの分岐を廃止し、Manual優先の形で併用可能としました。
  - これにより、Clipの参照をそのままに一部のみを非破壊的に編集した表情を取り扱えるようになります。
  - Animation Clipは表情以外のアニメーションを取り扱えるようになり、Animation Dataコンポーネントは廃止されました。
  - 適用対象のRenderとパスが一致するブレンドシェイプアニメーションのみが表情アニメーションと扱われるようになりました。
  - 全てのブレンドシェイプアニメーションを表情アニメーションとして扱う高度なオプションが追加されました。
- AnimatorControllerImporterの動作を改善しました。
- UIを改善しました。

### Removed
- Animation Dataコンポーネント

### Fixed
- FacialShapesEditor保存時に結果が重複して保存される問題を修正。 
- FaceTuneAssistantコンポーネントからメニューを用いた制御を追加する際に、MenuInstallerが重複する問題を修正。 `#86`

## [0.1.0-beta.8] - 2025-08-13
### Changed
- FacialShapesEditorを調整。
- AnimatorControllerImporterの動作を改善。

### Fixed
- コンポーネントが存在しない場合にパスが実行されていた問題を修正。

## [0.1.0-beta.7] - 2025-08-12
### Changed
- Assistantの動作を微調整。
- ExportFacialDataWindow._addZeroWeightのデフォルト値をfalseに。

### Fixed
- FacialShapesEditorで保存を行おうとするとInvalid property to resize arrayが発生する問題を修正。
- Presetで生成されるメニューで操作される制御がずれていた問題を修正。
- FacialShapesEditorでAnimationClipを保存出来ない問題を修正。

## [0.1.0-beta.6] - 2025-08-10
### Added
- AdvancedLipSyncSettingsでキャンセラー使用時の遷移時間設定を追加

### Changed
- 名前空間を変更。
- ExpressionコンポーネントのisEqualプロパティを変更。
  - isEqualのデータが失われます。
- 簡略名"FT"を削除し、"FaceTune"に統一。

### Fixed
- テンプレート用のPrefab名の誤字を修正。
- FacialShapesEditorにおいて、選択されたブレンドシェイプの表示領域がスクロールされない問題を修正。
- MMDダンスワールドにおける互換性を修正。

## [0.1.0-beta.5] - 2025-08-05
### Added
- `FaceTune Assistant`にAnimator Controllerのインポート機能が追加されました。
- プラットフォーム別のブルド時の動作を指定しました。
  - Animatorへの適用はVRChat向けビルドでのみ行われ、それ以外のパスは全プラットフォームで動作します。

### Changed
- Animator Controllerのインポートの仕様を変更しました。
  - 表情用のブレンドシェイプが含まれている場合のみExpressionが生成されるようになりました。
  - 表情アニメーションと表情以外のアニメーションの両方が含まれている場合は`Animation Data`を生成するようになりました。
  - 生成される`Facial Data`はManual Modeをデフォルトとするように変更されました。
- `FaceTune Assistant`からパターンを生成する際に、完全にPrefabをUnpackするようになりました。

### Removed
- `GameObject/FaceTune/Import from FX Layer`のメニューアイテムを削除しました。
- FaceMorphFirstパターンを削除しました。

## [0.1.0-beta.4] - 2025-08-03
### FaceTuneのほぼ全てのコンポーネントのデータが失われます。

### Added
- `FT Expression`および`FT Facial Data`を追加。
  - `FT Facial Expression`から置き換わります。
  - `FT Expression`がアタッチされたGameObject以下の`FT Facial Data`が紐付きます。
  - 同一の`FT Expression`の影響下にある複数の`FT Facial Data`は結合されます。
  - Hierarchyにおいて、下にあるほど優先されます。
- 汎用的なアニメーションに対応。
  - マルチフレームアニメーション・ループアニメーションに対応。
- `FT Animation Data`による任意アニメーションの再生に対応。
  - 基本的な動作は`FT Facial Data`と同一です。
  - 表情用のブレンドシェイプ以外の定義も可能にします。
- Motion Timeに対応。
  - マルチフレームのアニメーションの設定および`FT Expression`におけるパラメーターの設定が必要です。
- ネストされた`FT Condition`がAND条件として動作するように対応。
  - 同一のGameObjectに複数の`FT Condition`がアタッチされた際は引き続きOR条件として動作します。
- MA MenuItem（Radial）はMotion Timeとして動作するように対応。
- `FT Facial Style`を追加。
  - 顔つきを定義するコンポーネントとして、`FT Default Facial Expression`から置き換わります。
  - アタッチされたGameObject以下のEnable Blendingが無効なExpressionに対し、定義されたブレンドシェイプの適用を行います。
- `Assets/FaceTune/SelectedClipsToExclusiveMenu`にメニューアイテムを追加。
  - 選択された複数のAnimationClipから、排他制御のMenuItemを条件とするExpressionを生成します。
- `GameObject/FaceTune/Import from FX Layer`にメニューアイテムを追加。
  - アバターに現在適用されているFXレイヤーからFaceTuneの設定を追加します。
- `FT Advanced EyeBlink`を追加
  - アタッチされたGameObject以下のExpressionに対して影響を及ぼします。
  - 瞬きのアニメーション制御への変更・アニメーションの内容・瞬きの頻度などを設定できます。
  - また、干渉対策として任意のブレンドシェイプをキャンセラーとして使用できます。瞬きの進行に合わせて現在の表情とブレンドされます。
- `FT Advanced LipSync`を追加
  - アタッチされたGameObject以下のExpressionに対して影響を及ぼします。
  - 干渉対策として任意のブレンドシェイプをキャンセラーとして使用できます。発話の開始時に適用されます。
- `FacialShapesEditor`にブレンドシェイプのグルーピングや、一括変更、影響を受ける顔つきの表示、AnimatonClipに対する編集機能など追加しました。
- `FT Facial Data`のMenuItemにあるAnimation Clipとしての出力機能に対しオプションを追加しました。
- `Tools/FaceTune/FacialShapesEditor`から`FacialShapesEditor`を開くメニューアイテムを追加。

### Changed
- MA MenuItem（Toggle/Button）はOR条件として動作するように変更。
- いずれの条件にも紐付かないExpressionも許可するように変更。
- Prefabを更新。
- GC負荷を軽減。
- `FacialShapesEditor`が軽量に動作するようになりました。
- AnimationClipのImport方法を変更・修正。

### Removed
- `FT Facial Expression`を削除。
- `FT Default Facial Expression`を削除。
- `FT Dsiable Exisiting Control`を削除
  - ブレンドシェイプの上書きに関しては追加の条件を設定しない `FT Expression`で対応することとし、廃止します。
  - `FT Animation Data`により操作されるプロパティの無効化は行いません。
- `FT Gesture Smoothing`を削除。

### Fixed
- 毎フレームNDMF Previewが更新されていた問題を修正。
- 不要なTracking用レイヤーが生成されないように修正。
- floatのParameter Conditionが正常に動作していなかった問題を修正。
- アバターの初期化時にTracking Controlが正常に適用されていなかった問題を修正。
- 選択されているExpressionが正常にプレビューされないことがある問題を修正。

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

## [0.1.0-beta.2] - 2025-06-03
### Fixed
- Blendingが正常に動作していなかった問題を修正。

## [0.1.0-beta.1] - 2025-06-03
### Added
- 初回リリース