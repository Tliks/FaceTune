using Aoyon.FaceTune.Gui.Components;
using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal sealed class LipSyncAvailablePanel
{
    private readonly FacialShapesEditorContext _context;
    private readonly LipSyncEditing _editing;
    private readonly BlendShapeOverrideManager _canceller;
    private readonly TextField _search = new PlaceholderTextField();
    private readonly ListView _list = new();
    private readonly List<string> _names = new();
    private readonly Button _addAll;

    public VisualElement Element { get; } = new SpacedVerticalElement();

    public LipSyncAvailablePanel(
        FacialShapesEditorContext context,
        LipSyncEditing editing,
        BlendShapeOverrideManager canceller)
    {
        _context = context;
        _editing = editing;
        _canceller = canceller;
        _search.value = string.Empty;
        if (_search is PlaceholderTextField placeholder)
            placeholder.Placeholder = "facialEditor.search.placeholder".LS();
        _search.RegisterValueChangedCallback(_ => Rebuild());

        var controls = new HorizontalElement();
        _search.style.flexGrow = 1f;
        _addAll = new Button(AddAll);
        _addAll.AddToClassList("compact-control");
        _addAll.Add(new Image { image = EditorGUIUtility.IconContent("d_Toolbar Plus").image });
        controls.Add(_search);
        controls.Add(_addAll);
        Element.Add(controls);
        Element.Add(_list);

        _list.fixedItemHeight = FacialShapeUI.ListItemHeight;
        _list.selectionType = SelectionType.None;
        _list.style.flexGrow = 1f;
        _list.makeItem = MakeItem;
        _list.bindItem = BindItem;
        _list.itemsSource = _names;
        _list.RegisterCallback<MouseLeaveEvent>(_ =>
            _context.PreviewManager.CurrentHoveredIndex = -1);

        editing.StructureChanged += Rebuild;
        canceller.OnSingleShapeAdded += _ => Rebuild();
        canceller.OnMultipleShapesAdded += _ => Rebuild();
        canceller.OnSingleShapeRemoved += _ => Rebuild();
        canceller.OnMultipleShapesRemoved += _ => Rebuild();
        canceller.OnUnknownChange += Rebuild;
        Rebuild();
    }

    private VisualElement MakeItem()
    {
        var element = UnselectedShapeRowUI.Create();
        element.RegisterCallback<ClickEvent>(_ =>
        {
            if (element.userData is not string name || IsSelected(name)) return;
            if (_editing.CancellerSelected)
                _canceller.AddShapeWithWeight(_context.Catalog.IndexOf(name), 0f);
            else
                _editing.Add(name);
        });
        element.RegisterCallback<MouseEnterEvent>(_ =>
        {
            if (element.userData is string name)
                _context.PreviewManager.CurrentHoveredIndex = _context.Catalog.IndexOf(name);
        });
        return element;
    }

    private void BindItem(VisualElement element, int index)
    {
        var name = _names[index];
        element.userData = name;
        element.Q<Label>("name").text = name;
        element.SetEnabled(!IsSelected(name)
                           && (_editing.CancellerSelected
                               || _editing.Draft.Mode == LipSyncSettings.Kind.Custom));
    }

    private void AddAll()
    {
        var names = _names.Where(name => !IsSelected(name)).ToArray();
        if (_editing.CancellerSelected)
        {
            _canceller.AddShapesWithWeight(names.Select(name =>
                (_context.Catalog.IndexOf(name), 0f)));
        }
        else
        {
            _editing.SetShapes(
                names.Select(name => new BlendShapeWeight(name, 100f)),
                replaceExisting: false);
        }
    }

    private bool IsSelected(string name)
        => _editing.CancellerSelected
            ? _canceller.IsInTarget(_context.Catalog.IndexOf(name))
            : _editing.Contains(_editing.SelectedViseme, name);

    public void Rebuild()
    {
        _names.Clear();
        var search = _search.value ?? string.Empty;
        foreach (var name in _context.Catalog.Names)
        {
            var index = _context.Catalog.IndexOf(name);
            var unavailable = _editing.CancellerSelected
                ? _canceller.IsUnavailable(index)
                : _editing.UnavailableNames.Contains(name);
            if (unavailable
                || search.Length > 0
                && name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0
                || _context.GroupManager.IsRightSelected
                && !_context.GroupManager.IsBlendShapeVisible(index))
                continue;
            _names.Add(name);
        }
        _list.Rebuild();
        _addAll.SetEnabled(
            _names.Any(name => !IsSelected(name))
            && (_editing.CancellerSelected
                || _editing.Draft.Mode == LipSyncSettings.Kind.Custom));
    }
}
