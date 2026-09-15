namespace Aoyon.FaceTune.Preview;

internal sealed class PreviewTimeline : IDisposable
{
    private const double UpdateIntervalSeconds = 1.0 / 30.0;

    private float _duration;
    private bool _isLooping;
    private double _lastUpdateTime;

    internal float NormalizedTime { get; private set; }
    internal bool IsPlaying { get; private set; }
    internal bool IsAvailable => _duration > 0f;

    internal event Action<float>? TimeChanged;
    internal event Action? Completed;
    internal event Action? StateChanged;

    internal PreviewTimeline()
    {
        EditorApplication.update += OnEditorUpdate;
    }

    internal void Configure(float duration, bool isLooping)
    {
        _duration = Mathf.Max(0f, duration);
        _isLooping = isLooping;
        if (IsAvailable)
        {
            if (IsPlaying) _lastUpdateTime = EditorApplication.timeSinceStartup;
            return;
        }
        NormalizedTime = 0f;
        IsPlaying = false;
    }

    internal void Restart()
    {
        NormalizedTime = 0f;
        IsPlaying = IsAvailable;
        _lastUpdateTime = EditorApplication.timeSinceStartup;
        TimeChanged?.Invoke(NormalizedTime);
        StateChanged?.Invoke();
    }

    internal void TogglePlayback()
    {
        if (!IsAvailable) return;
        if (IsPlaying)
        {
            IsPlaying = false;
        }
        else
        {
            if (NormalizedTime >= 1f)
            {
                NormalizedTime = 0f;
                TimeChanged?.Invoke(NormalizedTime);
            }
            IsPlaying = true;
            _lastUpdateTime = EditorApplication.timeSinceStartup;
        }
        StateChanged?.Invoke();
    }

    internal void Seek(float normalizedTime)
    {
        if (!IsAvailable) return;
        NormalizedTime = Mathf.Clamp01(normalizedTime);
        IsPlaying = false;
        TimeChanged?.Invoke(NormalizedTime);
        StateChanged?.Invoke();
    }

    private void OnEditorUpdate()
    {
        if (!IsPlaying) return;
        var now = EditorApplication.timeSinceStartup;
        if (now - _lastUpdateTime < UpdateIntervalSeconds) return;
        var delta = (float)(now - _lastUpdateTime);
        _lastUpdateTime = now;

        NormalizedTime += delta / _duration;
        if (NormalizedTime >= 1f)
        {
            if (_isLooping)
            {
                NormalizedTime %= 1f;
            }
            else
            {
                NormalizedTime = 1f;
                IsPlaying = false;
            }
        }

        TimeChanged?.Invoke(NormalizedTime);
        if (!IsPlaying) Completed?.Invoke();
        StateChanged?.Invoke();
    }

    public void Dispose()
    {
        EditorApplication.update -= OnEditorUpdate;
        IsPlaying = false;
    }
}

internal static class BlendShapeAnimationPreview
{
    internal static float GetDuration(IEnumerable<BlendShapeWeightAnimation> animations)
        => animations.Select(animation => animation.Time).DefaultIfEmpty().Max();

    internal static ImmutableBlendShapeWeightSet Evaluate(
        IReadOnlyList<BlendShapeWeightAnimation> animations,
        float time)
        => new(
            animations.Select(animation => new BlendShapeWeight(animation.Name, animation.Weight(time))),
            animations.Count);
}
