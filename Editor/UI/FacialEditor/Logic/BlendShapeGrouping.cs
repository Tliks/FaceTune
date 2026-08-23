namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal class BlendShapeGrouping
{

    public IReadOnlyList<BlendShapeGroup> Groups { get; }
    private readonly BlendShapeGroup[] _groupByBlendShapeIndex;
    public event Action<IReadOnlyList<(BlendShapeGroup Group, bool Selected)>>? OnGroupSelectionChanged;

    private bool _isLeftSelected = true;
    public event Action<bool>? OnLeftSelectionChanged;
    public bool IsLeftSelected
    {
        get => _isLeftSelected;
        set
        {
            if (value != _isLeftSelected)
            {
                _isLeftSelected = value;
                OnLeftSelectionChanged?.Invoke(value);
            }
        }
    }

    private bool _isRightSelected = true;
    public event Action<bool>? OnRightSelectionChanged;
    public bool IsRightSelected
    {
        get => _isRightSelected;
        set
        {
            if (value != _isRightSelected)
            {
                _isRightSelected = value;
                OnRightSelectionChanged?.Invoke(value);
            }
        }
    }

    public BlendShapeGrouping(BlendShapeOverrideManager dataManager)
    {
        Groups = BuildGroups(dataManager.AllKeys);
        _groupByBlendShapeIndex = new BlendShapeGroup[dataManager.AllKeys.Count];
        foreach (var group in Groups)
        {
            foreach (var index in group.BlendShapeIndices)
                _groupByBlendShapeIndex[index] = group;
            group.OnSelectionChanged += selected =>
                OnGroupSelectionChanged?.Invoke(new[] { (group, selected) });
        }
    }

    private static IReadOnlyList<BlendShapeGroup> BuildGroups(IReadOnlyList<string> allKeys)
        => new BlendShapeGroupCatalog(allKeys).Groups
            .Select(definition =>
            {
                var group = new BlendShapeGroup(definition.Name);
                group.BlendShapeIndices.UnionWith(definition.BlendShapeIndices);
                return group;
            })
            .ToArray();

    public bool IsBlendShapeVisible(int index)
        => (uint)index < (uint)_groupByBlendShapeIndex.Length
        && _groupByBlendShapeIndex[index].IsSelected;
    
    public void SelectAll(bool selected)
    {
        var changes = new List<(BlendShapeGroup Group, bool Selected)>();
        
        foreach (var group in Groups)
        {
            if (group.IsSelected != selected)
            {
                group.SetSelectedSilently(selected);
                changes.Add((group, selected));
            }
        }
        
        if (changes.Count > 0)
        {
            OnGroupSelectionChanged?.Invoke(changes);
        }
    }
}

internal class BlendShapeGroup
{
    public readonly string Name;
    public readonly HashSet<int> BlendShapeIndices;
    private bool _isSelected = true;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (value != _isSelected)
            {
                _isSelected = value;
                OnSelectionChanged?.Invoke(value);
            }
        }
    }
    public event Action<bool>? OnSelectionChanged;

    public void SetSelectedSilently(bool value)
    {
        _isSelected = value;
    }

    public BlendShapeGroup(string name)
    {
        Name = name;
        BlendShapeIndices = new();
    }
}
