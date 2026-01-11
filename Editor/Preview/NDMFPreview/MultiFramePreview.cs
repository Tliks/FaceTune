namespace Aoyon.FaceTune.Preview;

class MultiFramePreview : IDisposable
{
    private readonly Action<IReadOnlyBlendShapeSet> _editAction;
    
    public bool IsActive { get; private set; } = false;
    public IReadOnlyList<BlendShapeWeightAnimation>? Animations { get; private set; } = null;
    public float Duration { get; private set; } = 0f;
    public bool IsLooping { get; private set; } = false;

    public double StartTime { get; private set; } = 0;
    public double LastUpdateTime { get; private set; } = 0;

    public const double UpdateIntervalSeconds = 1.0 / 30.0; // 30fpsにスロットリング
    
    public MultiFramePreview(Action<IReadOnlyBlendShapeSet> editAction)
    {
        EditorApplication.update += OnEditorUpdate;
        _editAction = editAction;
    }

    public void Start(IReadOnlyList<BlendShapeWeightAnimation> animations, bool isLooping)
    {
        IsActive = true;
        Animations = animations;
        Duration = animations.Max(a => a.Time);
        IsLooping = isLooping;
        StartTime = EditorApplication.timeSinceStartup;
        LastUpdateTime = 0;

        if (Duration <= 0f) IsActive = false;
    }

    public void Stop()
    {
        IsActive = false;
    }

    private void OnEditorUpdate()
    {
        if (!IsActive) return;
        if (Animations == null) return;

        var now = EditorApplication.timeSinceStartup;
        if (now - LastUpdateTime < UpdateIntervalSeconds) return; // スロットリング
        LastUpdateTime = now;

        var elapsed = now - StartTime;
        var endReached = false;
        float t;
        if (IsLooping)
        {
            t = (float)(elapsed % Duration);
        }
        else
        {
            if (elapsed >= Duration)
            {
                t = Duration;
                endReached = true;
            }
            else
            {
                t = (float)elapsed;
            }
        }

        using var _frameSet = BlendShapeSetPool.Get(out var frameSet);
        foreach (var anim in Animations)
        {
            frameSet.Add(new BlendShapeWeight(anim.Name, anim.Weight(t)));
        }

        _editAction(frameSet);

        if (endReached)
        {
            Stop();
        }
    }

    public void Dispose()
    {
        EditorApplication.update -= OnEditorUpdate;
    }
}