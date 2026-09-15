using UnityEditor.Overlays;

namespace Aoyon.FaceTune.Preview;

[Overlay(typeof(SceneView), "FaceTune Preview", true)]
internal sealed class FaceTunePreviewOverlay : IMGUIOverlay
{
    private const float PlayButtonSize = 20f;
    private const int PlayButtonIconSize = 12;
    private const float AvatarFieldWidth = 130f;
    private const float TimelineWidth = 130f;
    private const float ContentWidth = 154f;
    private const float VisemeButtonWidth = 30f;
    private const float SectionLabelHeight = 10f;
    private const int VisemeColumns = 5;

    private SelectedShapesPreview? _preview;
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
        if (_preview != null)
            _preview.TargetsChanged -= UpdateVisibility;
    }

    public override void OnGUI()
    {
        var preview = _preview ?? DirectBlendShapePreview.Instance.Selected;
        DrawAvatarSelector(preview);
        DrawSource(preview);
        DrawTimeline(
            "previewOverlay.multiFrame.label".LS(),
            preview.HasMultiFrame,
            preview.IsMultiFramePlaying,
            preview.MultiFrameTime,
            null,
            preview.ToggleMultiFramePlayback,
            preview.SetMultiFrameTime);
        DrawTimeline(
            "previewOverlay.eyeBlink.label".LS(),
            preview.HasEyeBlink,
            preview.IsEyeBlinkPlaying,
            preview.EyeBlinkTime,
            preview.EyeBlinkClosedRange,
            preview.ToggleEyeBlinkPlayback,
            preview.SetEyeBlinkTime);
        DrawVisemes(preview);
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
        Action<float> seek)
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
        var next = GUILayout.HorizontalSlider(
            time,
            0f,
            1f,
            GUILayout.Width(TimelineWidth));
        var sliderRect = GUILayoutUtility.GetLastRect();
        DrawMarkers(sliderRect, markers);
        if (EditorGUI.EndChangeCheck()) seek(next);
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

    private void DrawVisemes(SelectedShapesPreview preview)
    {
        DrawSectionLabel("previewOverlay.lipSync.label".LS());
        using var disabled = new EditorGUI.DisabledScope(!preview.HasLipSync);
        using var grid = new GUILayout.VerticalScope();

        _visemeButtonStyle ??= new GUIStyle(EditorStyles.miniButton)
        {
            fontSize = 10
        };
        var hovered = -1;
        var rowCount = LipSyncPreviewData.VisemeCount / VisemeColumns;
        for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {
            using var row = new GUILayout.HorizontalScope();
            for (var column = 0; column < VisemeColumns; column++)
            {
                var index = rowIndex * VisemeColumns + column;
                var selected = preview.SelectedViseme == index;
                var visemeName = LipSyncPreviewData.VisemeNames[index];
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

        var nextHover = preview.HasLipSync && eventType != EventType.MouseLeaveWindow
            ? hovered
            : -1;
        preview.SetVisemeHover(VisemeHoverSource.Overlay, nextHover);
    }
}
