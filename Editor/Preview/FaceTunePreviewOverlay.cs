using UnityEditor.Overlays;
using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Preview;

[Overlay(typeof(SceneView), FaceTuneConstants.Name, false)]
internal sealed class FaceTunePreviewOverlay : Overlay
{
    private const float PlayButtonSize = 20f;
    private const int PlayButtonIconSize = 12;
    private const float AvatarFieldWidth = 130f;
    private const float TimelineWidth = 130f;
    private const float ContentWidth = 154f;
    private const float CompactWindowMargin = 32f;
    private const int CloseDelayMilliseconds = 300;
    private const float VisemeButtonWidth = 30f;
    private const float SectionLabelHeight = 10f;
    private const int VisemeColumns = 5;

    private SelectedShapesPreview? _preview;
    private bool _expanded;
    private bool _pointerInside;
    private IVisualElementScheduledItem? _closeSchedule;
    private GUIStyle? _compactStyle;
    private GUIStyle? _playButtonStyle;
    private GUIStyle? _visemeButtonStyle;
    private GUIStyle? _sectionLabelStyle;

    public override void OnCreated()
    {
        _preview = DirectBlendShapePreview.Instance.Selected;
        _preview.TargetsChanged += UpdateVisibility;
        UpdateVisibility();
    }

    public override void OnWillBeDestroyed()
    {
        _closeSchedule?.Pause();
        if (_preview != null)
        {
            _preview.TargetsChanged -= UpdateVisibility;
            _preview.SetVisemeHover(VisemeHoverSource.Overlay, -1);
        }
    }

    public override VisualElement CreatePanelContent()
    {
        var content = new IMGUIContainer(OnGUI);
        content.RegisterCallback<MouseEnterEvent>(_ =>
        {
            _closeSchedule?.Pause();
            _closeSchedule = null;
            _pointerInside = true;
            _expanded = true;
            content.MarkDirtyRepaint();
        });
        content.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            _pointerInside = false;
            _preview?.SetVisemeHover(VisemeHoverSource.Overlay, -1);
            _closeSchedule?.Pause();
            _closeSchedule = content.schedule.Execute(() =>
            {
                _closeSchedule = null;
                _expanded = false;
                content.MarkDirtyRepaint();
            }).StartingIn(CloseDelayMilliseconds);
        });
        return content;
    }

    private void OnGUI()
    {
        var preview = _preview ?? DirectBlendShapePreview.Instance.Selected;
        if (!_expanded)
        {
            _compactStyle ??= new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                padding = new RectOffset(6, 6, 4, 4)
            };
            var label = "previewOverlay.details.label".LS();
            var textWidth = _compactStyle.CalcSize(new GUIContent(label)).x;
            var headerWidth = EditorStyles.label.CalcSize(new GUIContent(displayName)).x + 20f;
            var maxWidth = Mathf.Max(1f, containerWindow.position.width - CompactWindowMargin);
            GUILayout.Label(label, _compactStyle,
                GUILayout.Width(Mathf.Min(Mathf.Max(textWidth, headerWidth), maxWidth)));
            return;
        }

        DrawSectionLabel("previewOverlay.target.label".LS());
        DrawAvatarSelector(preview);
        DrawSource(preview);
        if (preview.HasMultiFrame)
            DrawTimeline(
                FormatStatus(
                    "previewOverlay.multiFrame.label",
                    "previewOverlay.status.present"),
                true,
                preview.IsMultiFramePlaying,
                preview.MultiFrameTime,
                null,
                preview.ToggleMultiFramePlayback,
                preview.SetMultiFrameTime);
        if (preview.EyeBlinkSetting != TrackingSettingDisplay.Hidden)
        {
            var label = FormatTrackingStatus(
                "previewOverlay.eyeBlink.label",
                preview.EyeBlinkBehavior,
                preview.EyeBlinkSetting);
            if (preview.CanPreviewEyeBlink)
                DrawTimeline(
                    label,
                    preview.EyeBlinkBehavior != TrackingBehaviorDisplay.Keep,
                    preview.IsEyeBlinkPlaying,
                    preview.EyeBlinkTime,
                    preview.EyeBlinkClosedRange,
                    preview.ToggleEyeBlinkPlayback,
                    preview.SetEyeBlinkTime,
                    preview.IsEyeBlinkPreviewApplied,
                    preview.ClearEyeBlinkPreview);
            else
                DrawUnavailable(label);
        }
        if (preview.LipSyncSetting != TrackingSettingDisplay.Hidden)
        {
            var label = FormatTrackingStatus(
                "previewOverlay.lipSync.label",
                preview.LipSyncBehavior,
                preview.LipSyncSetting);
            if (preview.CanPreviewLipSync)
                DrawVisemes(
                    preview,
                    label,
                    preview.LipSyncBehavior != TrackingBehaviorDisplay.Keep);
            else
                DrawUnavailable(label);
        }
    }

    private void UpdateVisibility()
    {
        displayed = _preview?.AvatarCount > 0;
    }

    private static void DrawAvatarSelector(SelectedShapesPreview preview)
    {
        using var row = new GUILayout.HorizontalScope();
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField(
                preview.CurrentAvatarRoot,
                typeof(GameObject),
                true,
                GUILayout.Width(AvatarFieldWidth));
        }

        var names = Enumerable.Range(0, preview.AvatarCount)
            .Select(preview.GetAvatarName)
            .ToArray();
        using var popupDisabled = new EditorGUI.DisabledScope(preview.AvatarCount == 1);
        var next = EditorGUILayout.Popup(
            preview.SelectedAvatarIndex,
            names,
            GUILayout.Width(PlayButtonSize));
        if (next != preview.SelectedAvatarIndex) preview.SetAvatar(next);
    }

    private static void DrawSource(SelectedShapesPreview preview)
    {
        using var disabled = new EditorGUI.DisabledScope(true);
        var source = preview.CurrentSource;
        var sourceType = source?.GetType() ?? typeof(Object);
        EditorGUILayout.ObjectField(
            source,
            sourceType,
            true,
            GUILayout.Width(ContentWidth));
    }

    private void DrawTimeline(
        string label,
        bool enabled,
        bool playing,
        float time,
        Vector2? markers,
        Action togglePlayback,
        Action<float> seek,
        bool previewApplied = false,
        Action? clearPreview = null)
    {
        DrawSectionLabel(label);
        using var row = new GUILayout.HorizontalScope();
        using var disabled = new EditorGUI.DisabledScope(!enabled);
        _playButtonStyle ??= new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = PlayButtonIconSize,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(1, 0, 0, 0)
        };
        var icon = playing ? "Ⅱ" : "▶";
        if (GUILayout.Button(
                icon,
                _playButtonStyle,
                GUILayout.Width(PlayButtonSize),
                GUILayout.Height(PlayButtonSize)))
            togglePlayback();

        EditorGUI.BeginChangeCheck();
        var timelineWidth = clearPreview == null
            ? TimelineWidth
            : TimelineWidth - PlayButtonSize - 4f;
        var next = GUILayout.HorizontalSlider(
            time,
            0f,
            1f,
            GUILayout.Width(timelineWidth));
        var sliderRect = GUILayoutUtility.GetLastRect();
        DrawMarkers(sliderRect, markers);
        if (EditorGUI.EndChangeCheck()) seek(next);

        if (clearPreview == null) return;
        using var clearDisabled = new EditorGUI.DisabledScope(!previewApplied);
        if (GUILayout.Button(
                "■",
                _playButtonStyle,
                GUILayout.Width(PlayButtonSize),
                GUILayout.Height(PlayButtonSize)))
            clearPreview();
    }

    private static void DrawMarkers(Rect sliderRect, Vector2? markers)
    {
        if (Event.current.type != EventType.Repaint || markers == null) return;
        var range = markers.Value;
        DrawMarker(sliderRect, range.x);
        DrawMarker(sliderRect, range.y);
    }

    private static void DrawMarker(Rect sliderRect, float time)
    {
        const float width = 2f;
        const float height = 10f;
        var thumbWidth = GUI.skin.horizontalSliderThumb.fixedWidth;
        var x = Mathf.Lerp(
            sliderRect.x + thumbWidth / 2f,
            sliderRect.xMax - thumbWidth / 2f,
            time);
        var barCenterY = sliderRect.yMax + 7f;
        var rect = new Rect(
            x - width / 2f,
            barCenterY - height / 2f,
            width,
            height);
        var color = EditorGUIUtility.isProSkin
            ? new Color32(95, 95, 95, 255)
            : new Color32(120, 120, 120, 255);
        EditorGUI.DrawRect(rect, color);
    }

    private static string FormatTrackingStatus(
        string labelKey,
        TrackingBehaviorDisplay behavior,
        TrackingSettingDisplay setting)
    {
        var label = labelKey.LS();
        var statusKey = behavior switch
        {
            TrackingBehaviorDisplay.NotApplicable => null,
            TrackingBehaviorDisplay.Unset => "previewOverlay.status.unset",
            TrackingBehaviorDisplay.Enabled => "previewOverlay.status.enabled",
            TrackingBehaviorDisplay.Disabled => "previewOverlay.status.disabled",
            TrackingBehaviorDisplay.Keep => "previewOverlay.status.keep",
            _ => throw new ArgumentOutOfRangeException(nameof(behavior), behavior, null)
        };
        var qualifiers = new List<string>(1);
        if (setting == TrackingSettingDisplay.Estimated)
            qualifiers.Add("previewOverlay.qualifier.estimated".LS());

        if (statusKey == null)
            return qualifiers.Count == 0
                ? label
                : $"{label}: {string.Join(" / ", qualifiers)}";
        var status = statusKey.LS();
        return qualifiers.Count == 0
            ? $"{label}: {status}"
            : $"{label}: {status}（{string.Join(" / ", qualifiers)}）";
    }

    private static string FormatStatus(string labelKey, string statusKey)
        => $"{labelKey.LS()}: {statusKey.LS()}";

    private void DrawSectionLabel(string label)
    {
        if (_sectionLabelStyle == null)
        {
            _sectionLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 10,
                fixedHeight = SectionLabelHeight,
                margin = new RectOffset(),
                padding = new RectOffset()
            };
            var color = _sectionLabelStyle.normal.textColor;
            _sectionLabelStyle.normal.textColor = color;
        }
        GUILayout.Label(label, _sectionLabelStyle, GUILayout.Height(SectionLabelHeight));
    }

    private void DrawUnavailable(string label)
    {
        DrawSectionLabel(label);
        using var disabled = new EditorGUI.DisabledScope(true);
        GUILayout.Label(
            "previewOverlay.unavailable.label".LS(),
            EditorStyles.miniLabel);
    }

    private void DrawVisemes(
        SelectedShapesPreview preview,
        string label,
        bool enabled)
    {
        DrawSectionLabel(label);
        using var disabled = new EditorGUI.DisabledScope(!enabled);
        using var grid = new GUILayout.VerticalScope();

        _visemeButtonStyle ??= new GUIStyle(EditorStyles.miniButton)
        {
            fontSize = 10
        };
        var hovered = -1;
        var rowCount = VrcVisemeLipSyncShapes.Count / VisemeColumns;
        for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            using var row = new GUILayout.HorizontalScope();
            for (var column = 0; column < VisemeColumns; column++)
            {
                var index = rowIndex * VisemeColumns + column;
                var selected = preview.SelectedViseme == index;
                var visemeName = VrcVisemeLipSyncShapes.Names[index];
                var next = GUILayout.Toggle(
                    selected,
                    visemeName,
                    _visemeButtonStyle,
                    GUILayout.Width(VisemeButtonWidth));
                if (next != selected)
                    preview.SetVisemeSelection(selected ? -1 : index);
                if (GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    hovered = index;
            }
        }

        var eventType = Event.current.type;
        if (eventType != EventType.Repaint
            && eventType != EventType.MouseMove
            && eventType != EventType.MouseLeaveWindow)
            return;

        var nextHover = enabled && _pointerInside && eventType != EventType.MouseLeaveWindow
            ? hovered
            : -1;
        preview.SetVisemeHover(VisemeHoverSource.Overlay, nextHover);
    }
}
