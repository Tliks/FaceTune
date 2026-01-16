using System.Text.RegularExpressions;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal class BlendShapeGrouping
{
    private const string DefaultGroupName = "Default";

    private static readonly string GroupNameSymbolPattern = string.Join("|", new[]
    {
        @"\W",
        @"\p{Pc}",
        @"ー",
        @"ｰ",
    });

    private static readonly string GroupNamePattern = string.Join("|", new[]
    {
        $"^(?:(?:{GroupNameSymbolPattern}){{3,}})(.*?)(?:(?:{GroupNameSymbolPattern}){{3,}})?$",
        $"^(?:(?:{GroupNameSymbolPattern}){{3,}})?(.*?)(?:(?:{GroupNameSymbolPattern}){{3,}})$",
    });

    public IReadOnlyList<BlendShapeGroup> Groups { get; private set; }
    public event Action<IReadOnlyList<(BlendShapeGroup Group, bool Selected)>>? OnGroupSelectionChanged;

    private bool _isLeftSelected = false;
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

    public BlendShapeGrouping(TargetManager targetManager, BlendShapeOverrideManager dataManager)
    {
        Groups = new List<BlendShapeGroup>(){ new(DefaultGroupName) };
    }

    public void Refresh(IReadOnlyList<string> allKeys)
    {
        var groups = new List<BlendShapeGroup>(){ new(DefaultGroupName) };

        for (var index = 0; index < allKeys.Count; index++)
        {
            var key = allKeys[index];
            var match = Regex.Match(key, GroupNamePattern);
            if (match.Success)
            {
                var extractedName = match.Groups.Cast<Group>().Skip(1).First(x => x.Success).Value;
                groups.Add(new BlendShapeGroup(extractedName));
            }
            groups.Last().BlendShapeIndices.Add(index);
        }
        Groups = groups.AsReadOnly();
        foreach (var group in Groups)
        {
            group.OnSelectionChanged += (selected) => OnGroupSelectionChanged?.Invoke(new[] { (group, selected) });
        }
    }

    public bool IsBlendShapeVisible(int index)
    {
        foreach (var group in Groups)
        {
            if (group.BlendShapeIndices.Contains(index))
            {
                return group.IsSelected;
            }
        }
        return false;
    }
    
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
