using Aoyon.FaceTune.Gui.Components;
using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal sealed class PreviewTimelineElement : IDisposable
{
    private const float PlaybackDurationSeconds = 0.5f;

    private readonly Button _play;
    private readonly Slider _slider;
    private readonly Action<float> _seek;
    private IVisualElementScheduledItem? _schedule;
    private double _startedAt;
    private float _startValue;
    private bool _playing;

    public VisualElement Element { get; } = new SpacedHorizontalElement();

    public PreviewTimelineElement(float initialValue, Action<float> seek)
    {
        _seek = seek;
        Element.style.flexDirection = FlexDirection.Row;
        Element.style.alignItems = Align.Center;
        Element.style.width = Length.Percent(100f);
        _play = new Button(TogglePlayback) { text = "▶" };
        _play.style.width = 20f;
        _play.style.height = 20f;
        _play.style.flexShrink = 0f;
        _play.style.fontSize = 12f;
        _play.style.unityFontStyleAndWeight = FontStyle.Bold;
        _play.style.unityTextAlign = TextAnchor.MiddleCenter;
        _play.style.paddingLeft = 0f;
        _play.style.paddingRight = 0f;
        _play.style.paddingTop = 0f;
        _play.style.paddingBottom = 0f;
        _slider = new Slider(0f, 1f) { value = initialValue };
        _slider.style.flexGrow = 1f;
        _slider.style.flexShrink = 1f;
        _slider.style.flexBasis = 0f;
        _slider.style.minWidth = 0f;
        _slider.style.height = 18f;
        _slider.RegisterValueChangedCallback(evt =>
        {
            Pause();
            _seek(evt.newValue);
        });
        Element.Add(_play);
        Element.Add(_slider);
    }

    private void TogglePlayback()
    {
        if (_playing)
        {
            Pause();
            return;
        }
        _playing = true;
        _play.text = "Ⅱ";
        _startValue = _slider.value >= 1f ? 0f : _slider.value;
        _startedAt = EditorApplication.timeSinceStartup;
        _schedule = Element.schedule.Execute(Tick).Every(16);
    }

    private void Tick()
    {
        var value = _startValue
                    + (float)(EditorApplication.timeSinceStartup - _startedAt)
                    / PlaybackDurationSeconds;
        if (value >= 1f)
        {
            Pause();
            _slider.SetValueWithoutNotify(0f);
            _seek(0f);
            return;
        }
        _slider.SetValueWithoutNotify(value);
        _seek(value);
    }

    private void Pause()
    {
        if (!_playing) return;
        _playing = false;
        _play.text = "▶";
        _schedule?.Pause();
    }

    public void Dispose()
    {
        Pause();
        _schedule = null;
    }
}
