using System.Text.RegularExpressions;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

/// <summary>Parses the separator blend shapes used by face meshes into ordered groups.</summary>
internal sealed class BlendShapeGroupCatalog
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

    public IReadOnlyList<BlendShapeGroupDefinition> Groups { get; }

    public BlendShapeGroupCatalog(IReadOnlyList<string> names)
    {
        var groups = new List<BlendShapeGroupDefinition> { new(DefaultGroupName) };
        for (var index = 0; index < names.Count; index++)
        {
            var match = Regex.Match(names[index], GroupNamePattern);
            if (match.Success)
            {
                var name = match.Groups.Cast<Group>().Skip(1).First(group => group.Success).Value;
                groups.Add(new BlendShapeGroupDefinition(name));
            }
            groups[^1].BlendShapeIndices.Add(index);
        }
        Groups = groups.AsReadOnly();
    }
}

internal sealed class BlendShapeGroupDefinition
{
    public string Name { get; }
    public List<int> BlendShapeIndices { get; } = new();

    public BlendShapeGroupDefinition(string name) => Name = name;
}
