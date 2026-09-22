namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal sealed class LipSyncEditing
{
    private readonly SerializedObject _serializedObject;
    private readonly SerializedProperty _property;
    private LipSyncSettings _initial;
    private LipSyncSettings _observed;

    public LipSyncSettings Draft { get; }
    public VrcVisemeLipSyncShapes? BuiltIn { get; }
    public ISet<string> UnavailableNames { get; }
    public int SelectedViseme { get; private set; }
    public bool CancellerSelected { get; private set; }
    public int HoveredViseme { get; private set; } = -1;
    public int PreviewViseme => HoveredViseme >= 0 ? HoveredViseme : SelectedViseme;
    public VrcVisemeLipSyncShapes? PreviewShapes
        => Draft.Mode == LipSyncSettings.Kind.Custom ? Draft.Shapes : BuiltIn;
    public bool HasChanges => !Draft.Equals(_initial);

    public event Action? Changed;

    public LipSyncEditing(
        SerializedObject serializedObject,
        LipSyncSettings draft,
        VrcVisemeLipSyncShapes? builtIn,
        ISet<string> unavailableNames)
    {
        _serializedObject = serializedObject;
        _property = serializedObject.FindProperty("_lipSyncDraft");
        Draft = draft;
        BuiltIn = builtIn;
        UnavailableNames = unavailableNames;
        _initial = draft.Clone();
        _observed = draft.Clone();
    }

    public void SetSelectedViseme(int index)
    {
        index = Mathf.Clamp(index, 0, VrcVisemeLipSyncShapes.Count - 1);
        if (SelectedViseme == index && !CancellerSelected) return;
        SelectedViseme = index;
        CancellerSelected = false;
        NotifyChanged();
    }

    public void SelectCanceller()
    {
        if (CancellerSelected) return;
        CancellerSelected = true;
        NotifyChanged();
    }

    public void SetHoveredViseme(int index)
    {
        index = Mathf.Clamp(index, -1, VrcVisemeLipSyncShapes.Count - 1);
        if (HoveredViseme == index) return;
        HoveredViseme = index;
        NotifyChanged();
    }

    public void SetMode(LipSyncSettings.Kind mode)
    {
        var previous = Draft.Mode;
        if (previous == mode) return;
        _serializedObject.UpdateIfRequiredOrScript();
        _property.FindPropertyRelative(nameof(LipSyncSettings.Mode)).intValue = (int)mode;
        if (mode == LipSyncSettings.Kind.Custom
            && previous == LipSyncSettings.Kind.BuiltIn
            && BuiltIn != null)
        {
            _property.FindPropertyRelative(nameof(LipSyncSettings.Shapes)).CopyFrom(BuiltIn);
        }
        Apply();
    }

    public bool TryGetInitialShape(int visemeIndex, string name, out BlendShapeWeight shape)
    {
        foreach (var candidate in _initial.Shapes.GetOrderedShapes()[visemeIndex])
        {
            if (candidate.Name != name) continue;
            shape = candidate;
            return true;
        }
        shape = default;
        return false;
    }

    public bool Contains(int visemeIndex, string name)
        => Draft.Shapes.GetOrderedShapes()[visemeIndex]
            .Any(shape => shape.Name == name);

    public float GetWeight(int visemeIndex, string name)
    {
        foreach (var shape in Draft.Shapes.GetOrderedShapes()[visemeIndex])
        {
            if (shape.Name == name) return shape.Weight;
        }
        return 0f;
    }

    public bool IsChanged(int visemeIndex, BlendShapeWeight shape)
        => !TryGetInitialShape(visemeIndex, shape.Name, out var initial)
           || !Mathf.Approximately(initial.Weight, shape.Weight);

    public void Restore(int visemeIndex, string name)
    {
        if (TryGetInitialShape(visemeIndex, name, out var initial))
            SetWeight(visemeIndex, name, initial.Weight);
        else
            Remove(visemeIndex, name);
    }

    public void SetWeight(int visemeIndex, string name, float weight)
    {
        var property = GetVisemeProperty(visemeIndex);
        for (var index = 0; index < property.arraySize; index++)
        {
            var element = property.GetArrayElementAtIndex(index);
            if (element.FindPropertyRelative(BlendShapeWeight.NamePropName).stringValue != name)
                continue;
            element.FindPropertyRelative(BlendShapeWeight.WeightPropName).floatValue = weight;
            Apply();
            return;
        }
    }

    public void Remove(int visemeIndex, string name)
    {
        var property = GetVisemeProperty(visemeIndex);
        for (var index = 0; index < property.arraySize; index++)
        {
            if (property.GetArrayElementAtIndex(index)
                    .FindPropertyRelative(BlendShapeWeight.NamePropName).stringValue != name)
                continue;
            property.DeleteArrayElementAtIndex(index);
            Apply();
            return;
        }
    }

    public void Add(string name)
    {
        SetShapes(new[] { new BlendShapeWeight(name, 100f) }, replaceExisting: false);
    }

    public void SetShapes(
        IEnumerable<BlendShapeWeight> shapes,
        bool replaceExisting)
    {
        if (Draft.Mode != LipSyncSettings.Kind.Custom) return;
        var property = GetVisemeProperty(SelectedViseme);
        var values = shapes
            .Where(shape => !UnavailableNames.Contains(shape.Name))
            .GroupBy(shape => shape.Name, StringComparer.Ordinal)
            .Select(group => group.Last())
            .ToArray();
        if (replaceExisting) property.ClearArray();
        var indices = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < property.arraySize; index++)
        {
            var name = property.GetArrayElementAtIndex(index)
                .FindPropertyRelative(BlendShapeWeight.NamePropName).stringValue;
            indices[name] = index;
        }
        foreach (var shape in values)
        {
            if (!indices.TryGetValue(shape.Name, out var index))
            {
                index = property.arraySize;
                property.InsertArrayElementAtIndex(index);
                indices.Add(shape.Name, index);
            }
            property.GetArrayElementAtIndex(index).CopyFrom(shape);
        }
        Apply();
    }

    public bool SynchronizeAfterUndo()
    {
        if (Draft.Equals(_observed)) return false;
        _observed = Draft.Clone();
        Changed?.Invoke();
        return true;
    }

    public void MarkSaved()
    {
        _initial = Draft.Clone();
        _observed = Draft.Clone();
        Changed?.Invoke();
    }

    private SerializedProperty GetVisemeProperty(int index)
    {
        _serializedObject.UpdateIfRequiredOrScript();
        return _property
            .FindPropertyRelative(nameof(LipSyncSettings.Shapes))
            .FindPropertyRelative(VrcVisemeLipSyncShapes.PropertyNames[index]);
    }

    private void Apply()
    {
        _serializedObject.ApplyModifiedProperties();
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        _observed = Draft.Clone();
        Changed?.Invoke();
    }
}
