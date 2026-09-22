using Aoyon.FaceTune.Gui.Components;
using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal sealed class LipSyncPanel
{
    private readonly FacialShapesEditorContext _context;
    private readonly LipSyncEditing _editing;
    private readonly TextField _selectedSearch = new PlaceholderTextField();
    private readonly TextField _availableSearch = new PlaceholderTextField();
    private readonly ScrollView _sections = new();
    private readonly ListView _available = new();
    private readonly List<string> _availableNames = new();
    private readonly List<SimpleToggle> _visemeButtons = new();
    private readonly List<VisualElement> _visemeContents = new();

    public VisualElement SelectedElement { get; } = new SpacedVerticalElement();
    public VisualElement AvailableElement { get; } = new SpacedVerticalElement();

    public LipSyncPanel(
        FacialShapesEditorContext context,
        LipSyncEditing editing)
    {
        _context = context;
        _editing = editing;
        _selectedSearch.value = string.Empty;
        _availableSearch.value = string.Empty;
        if (_selectedSearch is PlaceholderTextField selected)
            selected.Placeholder = "facialEditor.search.placeholder".LS();
        if (_availableSearch is PlaceholderTextField available)
            available.Placeholder = "facialEditor.search.placeholder".LS();
        _selectedSearch.RegisterValueChangedCallback(_ => RebuildSections());
        _availableSearch.RegisterValueChangedCallback(_ => RebuildAvailable());

        _sections.style.flexGrow = 1f;
        _available.style.flexGrow = 1f;
        SelectedElement.Add(CreateModeField());
        SelectedElement.Add(_selectedSearch);
        SelectedElement.Add(_sections);
        AvailableElement.Add(_availableSearch);
        AvailableElement.Add(_available);

        _available.fixedItemHeight = FacialShapeUI.ListItemHeight;
        _available.selectionType = SelectionType.None;
        _available.makeItem = MakeAvailableItem;
        _available.bindItem = BindAvailableItem;
        _available.RegisterCallback<MouseLeaveEvent>(_ =>
            _context.PreviewManager.CurrentHoveredIndex = -1);
        context.GroupManager.OnGroupSelectionChanged += _ => Rebuild();
        context.GroupManager.OnLeftSelectionChanged += _ => RebuildSections();
        context.GroupManager.OnRightSelectionChanged += _ => RebuildAvailable();

        Rebuild();
    }

    private VisualElement CreateModeField()
    {
        var labels = new[]
        {
            "lipSync.mode.option.builtIn".LS(),
            "lipSync.mode.option.custom".LS()
        };
        var selected = _editing.Draft.Mode == LipSyncSettings.Kind.Custom ? 1 : 0;
        var field = new PopupField<string>(
            "lipSync.mode.label".LS(),
            labels.ToList(),
            selected);
        field.RegisterValueChangedCallback(evt =>
        {
            var custom = evt.newValue == labels[1];
            SetMode(custom ? LipSyncSettings.Kind.Custom : LipSyncSettings.Kind.BuiltIn);
        });
        return field;
    }

    private void SetMode(LipSyncSettings.Kind mode)
    {
        if (_editing.Draft.Mode == mode) return;
        _editing.SetMode(mode);
        Rebuild();
    }

    public void Rebuild()
    {
        RebuildSections();
        RebuildAvailable();
    }

    private void RebuildSections()
    {
        _sections.Clear();
        _visemeButtons.Clear();
        _visemeContents.Clear();
        var values = (_editing.PreviewShapes ?? new VrcVisemeLipSyncShapes())
            .GetOrderedShapes();
        var search = _selectedSearch.value ?? string.Empty;
        for (var index = 0; index < VrcVisemeLipSyncShapes.Count; index++)
        {
            var visemeIndex = index;
            var section = new HorizontalElement();
            section.style.alignItems = Align.FlexStart;
            section.style.marginBottom = FacialShapeUI.Spacing;
            var button = new SimpleToggle
            {
                text = VrcVisemeLipSyncShapes.Names[index],
                value = index == _editing.SelectedViseme
            };
            button.style.width = 44f;
            button.style.minWidth = 44f;
            button.style.maxWidth = 44f;
            button.style.height = FacialShapeUI.RowHeight;
            button.style.marginRight = FacialShapeUI.Spacing;
            button.RegisterValueChangedCallback(evt =>
            {
                if (!evt.newValue)
                {
                    button.SetValueWithoutNotify(true);
                    return;
                }
                _editing.SetSelectedViseme(visemeIndex);
                UpdateSelection();
                _available.RefreshItems();
            });
            button.RegisterCallback<MouseEnterEvent>(_ =>
                _editing.SetHoveredViseme(visemeIndex));
            button.RegisterCallback<MouseLeaveEvent>(_ =>
                _editing.SetHoveredViseme(-1));
            section.Add(button);
            _visemeButtons.Add(button);

            var content = new SpacedVerticalElement();
            content.style.flexGrow = 1f;
            content.SetEnabled(_editing.Draft.Mode == LipSyncSettings.Kind.Custom
                               && index == _editing.SelectedViseme);
            foreach (var shape in values[index])
            {
                var shapeIndex = _context.Catalog.IndexOf(shape.Name);
                if (search.Length > 0
                    && shape.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0
                    || _context.GroupManager.IsLeftSelected
                    && !_context.GroupManager.IsBlendShapeVisible(shapeIndex))
                    continue;
                content.Add(CreateShapeRow(index, shape));
            }
            if (content.childCount == 0)
            {
                var empty = new VisualElement();
                empty.style.height = FacialShapeUI.RowHeight;
                content.Add(empty);
            }
            section.Add(content);
            _visemeContents.Add(content);
            _sections.Add(section);
        }
    }

    private void UpdateSelection()
    {
        var editable = _editing.Draft.Mode == LipSyncSettings.Kind.Custom;
        for (var index = 0; index < _visemeButtons.Count; index++)
        {
            _visemeButtons[index].SetValueWithoutNotify(index == _editing.SelectedViseme);
            _visemeContents[index].SetEnabled(editable && index == _editing.SelectedViseme);
        }
    }

    private VisualElement CreateShapeRow(int visemeIndex, BlendShapeWeight shape)
    {
        var row = new SelectedShapeRow();
        row.SetChanged(_editing.IsChanged(visemeIndex, shape));
        row.FacialRail.style.opacity = 0f;
        row.Metadata.RegisterCallback<ClickEvent>(_ =>
        {
            _editing.Restore(visemeIndex, shape.Name);
            Rebuild();
        });
        row.NameLabel.text = shape.Name;
        var shapeIndex = _context.Catalog.IndexOf(shape.Name);
        row.SetWarning(shapeIndex < 0, _editing.UnavailableNames.Contains(shape.Name));
        void ApplyWeight(float value)
        {
            _editing.SetWeight(visemeIndex, shape.Name, value);
            row.Weight.SetValueWithoutNotify(value);
            row.SetChanged(
                _editing.IsChanged(visemeIndex, new BlendShapeWeight(shape.Name, value)));
        }

        row.Weight.SetValueWithoutNotify(shape.Weight);
        row.Weight.RegisterValueChangedCallback(evt => ApplyWeight(evt.newValue));
        row.Curve.SetVisible(false);
        row.CurveToggle.SetVisible(false);
        row.WeightToggle.clicked += () => ApplyWeight(
            Mathf.Approximately(_editing.GetWeight(visemeIndex, shape.Name), 0f) ? 100f : 0f);
        row.Remove.clicked += () =>
        {
            _editing.Remove(visemeIndex, shape.Name);
            Rebuild();
        };
        return row;
    }

    private void RebuildAvailable()
    {
        _availableNames.Clear();
        var search = _availableSearch.value ?? string.Empty;
        foreach (var name in _context.Catalog.Names)
        {
            var shapeIndex = _context.Catalog.IndexOf(name);
            if (_editing.UnavailableNames.Contains(name)
                || search.Length > 0
                && name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0
                || _context.GroupManager.IsRightSelected
                && !_context.GroupManager.IsBlendShapeVisible(shapeIndex))
                continue;
            _availableNames.Add(name);
        }
        _available.itemsSource = _availableNames;
        _available.RefreshItems();
    }

    private VisualElement MakeAvailableItem()
    {
        var element = UnselectedShapeRowUI.Create();
        element.RegisterCallback<ClickEvent>(_ =>
        {
            if (element.userData is not string name) return;
            _editing.Add(name);
            Rebuild();
        });
        element.RegisterCallback<MouseEnterEvent>(_ =>
        {
            if (element.userData is not string name) return;
            _context.PreviewManager.CurrentHoveredIndex =
                _context.Catalog.IndexOf(name);
        });
        return element;
    }

    private void BindAvailableItem(VisualElement element, int index)
    {
        var name = _availableNames[index];
        element.userData = name;
        element.Q<Label>("name").text = name;
        element.SetEnabled(
            _editing.Draft.Mode == LipSyncSettings.Kind.Custom
            && !_editing.Contains(_editing.SelectedViseme, name));
    }
}
