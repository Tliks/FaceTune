using System.Text.RegularExpressions;

namespace aoyon.facetune.ui;

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

    public readonly List<BlendShapeGroup> Groups;

    public BlendShapeGrouping(IReadOnlyList<string> allKeys)
    {
        Groups = new List<BlendShapeGroup>();
        BuildGroups(allKeys);
    }

    private void BuildGroups(IReadOnlyList<string> allKeys)
    {
        Groups.Clear();
        Groups.Add(new BlendShapeGroup(DefaultGroupName));

        for (var index = 0; index < allKeys.Count; index++)
        {
            var key = allKeys[index];
            var match = Regex.Match(key, GroupNamePattern);
            if (match.Success)
            {
                var extractedName = match.Groups.Cast<Group>().Skip(1).First(x => x.Success).Value;
                Groups.Add(new BlendShapeGroup(extractedName));
            }
            Groups.Last().BlendShapeIndices.Add(index);
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
        foreach (var group in Groups)
        {
            group.IsSelected = selected;
        }
    }
}

internal class BlendShapeGroup
{
    public readonly string Name;
    public readonly HashSet<int> BlendShapeIndices;
    public bool IsSelected { get; set; } = true;

    public BlendShapeGroup(string name)
    {
        Name = name;
        BlendShapeIndices = new();
    }
}
