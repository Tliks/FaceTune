# Changelog

## [Japanese version](./CHANGELOG-jp.md)

## [Unreleased]
### Added

### Changed

### Deprecated

### Removed

### Fixed

### Security

## [0.1.0-beta.4] - 2025-08-03
### Data for almost all FaceTune components will be lost.

### Added
- Added `FT Expression` and `FT Facial Data`.
  - Replaces `FT Facial Expression`.
  - `FT Facial Data` under the GameObject to which `FT Expression` is attached will be linked.
  - Multiple `FT Facial Data` under the influence of the same `FT Expression` will be merged.
  - In the Hierarchy, the lower ones have higher priority.
- Supports generic animations.
  - Supports multi-frame and loop animations.
- Supports playback of arbitrary animations with `FT Animation Data`.
  - The basic operation is the same as `FT Facial Data`.
  - Allows definitions other than blendshapes for facial expressions.
- Supports Motion Time.
  - Requires setting up multi-frame animations and parameters in `FT Expression`.
- Nested `FT Condition`s now work as AND conditions.
  - When multiple `FT Condition`s are attached to the same GameObject, they continue to work as OR conditions.
- MA MenuItem (Radial) now works as Motion Time.
- Added `FT Facial Style`.
  - Replaces `FT Default Facial Expression` as the component for defining facial features.
  - Applies the defined blendshapes to Expressions with Enable Blending disabled under the attached GameObject.
- Added a menu item to `Assets/FaceTune/SelectedClipsToExclusiveMenu`.
  - Generates an Expression with an exclusive MenuItem condition from multiple selected AnimationClips.
- Added a menu item to `GameObject/FaceTune/Import from FX Layer`.
  - Adds FaceTune settings from the FX layer currently applied to the avatar.
- Added `FT Advanced EyeBlink`.
  - Affects Expressions under the attached GameObject.
  - You can change the animation control for blinking, the content of the animation, and the frequency of blinking.
  - Also, you can use any blendshape as a canceller for interference prevention. It blends with the current expression as the blink progresses.
- Added `FT Advanced LipSync`.
  - Affects Expressions under the attached GameObject.
  - You can use any blendshape as a canceller for interference prevention. It is applied at the start of speech.
- Added blendshape grouping, batch changes, display of affected facial features, and editing functions for AnimationClips to `FacialShapesEditor`.
- Added options to the output function as Animation Clip in the MenuItem of `FT Facial Data`.
- Added a menu item to open `FacialShapesEditor` from `Tools/FaceTune/FacialShapesEditor`.

### Changed
- MA MenuItem (Toggle/Button) now works as an OR condition.
- Expressions not tied to any condition are now allowed.
- Updated Prefab.
- Reduced GC load.
- `FacialShapesEditor` now runs more lightly.
- Changed and fixed the AnimationClip import method.

### Removed
- Removed `FT Facial Expression`.
- Removed `FT Default Facial Expression`.
- Removed `FT Disable Existing Control`.
  - Overwriting blendshapes will be handled by `FT Expression` without additional conditions, so this is obsolete.
  - Properties manipulated by `FT Animation Data` will not be disabled.
- Removed `FT Gesture Smoothing`.

### Fixed
- Fixed an issue where the NDMF Preview was updated every frame.
- Fixed an issue where unnecessary Tracking layers were generated.
- Fixed an issue where the float Parameter Condition was not working correctly.
- Fixed an issue where Tracking Control was not applied correctly when initializing the avatar.
- Fixed an issue where the selected Expression was sometimes not previewed correctly.

## [0.1.0-beta.3] - 2025-06-14
### Changed
- Updated Prefab.
- Changed Undo to be a single event when closing FacialShapesEditor.
- Added support for saving FacialShapesEditor with ctrl/cmd-s.
- Added FacialShapesEditor to DefaultFacialExpressionComponent.

### Fixed
- Fixed an issue where multiple Expressions were not merged correctly.
- Fixed an issue where Undo was not working in some cases.
- Fixed an issue where some meshes were not released.
- Fixed an issue where presets were not working correctly.

## [0.1.0-beta.2] - 2025-06-03
### Fixed
- Fixed an issue where Blending was not working correctly.

## [0.1.0-beta.1] - 2025-06-03
### Added
- Initial release.

## [0.1.0] - 2025-01-01
