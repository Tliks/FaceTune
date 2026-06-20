namespace Aoyon.FaceTune.Preview;

abstract class MultiFramePreviewBase : IDisposable
{
    private const double UpdateIntervalSeconds = 1.0 / 30.0; // 30fpsにスロットリング

    private readonly bool _isLooping;
    private readonly float _duration;
    private readonly double _startTime;
    private double _lastUpdateTime;
    private bool _isActive;

    protected MultiFramePreviewBase(float duration, bool isLooping)
    {
        _isLooping = isLooping;
        _duration = duration;
        _startTime = EditorApplication.timeSinceStartup;
        _lastUpdateTime = 0;
        _isActive = _duration > 0f;

        EditorApplication.update += OnEditorUpdate;
    }

    protected abstract void ApplyFrame(float time);

    private void Stop()
    {
        _isActive = false;
    }

    private void OnEditorUpdate()
    {
        if (!_isActive) return;

        var now = EditorApplication.timeSinceStartup;
        if (now - _lastUpdateTime < UpdateIntervalSeconds) return;
        _lastUpdateTime = now;

        var elapsed = now - _startTime;
        var endReached = false;
        float time;
        if (_isLooping)
        {
            time = (float)(elapsed % _duration);
        }
        else if (elapsed >= _duration)
        {
            time = _duration;
            endReached = true;
        }
        else
        {
            time = (float)elapsed;
        }

        ApplyFrame(time);

        if (endReached)
        {
            Stop();
        }
    }

    public void Dispose()
    {
        EditorApplication.update -= OnEditorUpdate;
        Stop();
    }
}

sealed class BlendShapeMultiFramePreview : MultiFramePreviewBase
{
    private readonly SkinnedMeshRenderer _renderer;
    private readonly IReadOnlyList<BlendShapeWeightAnimation> _animations;
    private readonly Action<SkinnedMeshRenderer, IReadOnlyBlendShapeSet> _editAction;

    public BlendShapeMultiFramePreview(
        SkinnedMeshRenderer renderer,
        IReadOnlyList<BlendShapeWeightAnimation> animations,
        bool isLooping,
        Action<SkinnedMeshRenderer, IReadOnlyBlendShapeSet> editAction)
        : base(GetDuration(animations), isLooping)
    {
        _renderer = renderer;
        _animations = animations;
        _editAction = editAction;
    }

    private static float GetDuration(IReadOnlyList<BlendShapeWeightAnimation> animations)
    {
        return animations.Count == 0 ? 0f : animations.Max(animation => animation.Time);
    }

    protected override void ApplyFrame(float time)
    {
        using var _frameSet = BlendShapeSetPool.Get(out var frameSet);
        foreach (var animation in _animations)
        {
            frameSet.Add(new BlendShapeWeight(animation.Name, animation.Weight(time)));
        }

        _editAction(_renderer, frameSet);
    }
}
