using Aoyon.FaceTune.Gui.Components;
using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal sealed class LipSyncPanel
{
    private readonly FacialShapesEditorContext _context;
    private readonly TextField _selectedSearch = new PlaceholderTextField();
    private readonly TextField _availableSearch = new PlaceholderTextField();
    private readonly ScrollView _sections = new();
    private readonly ListView _available = new();
    private readonly List<string> _availableNames = new();
    private readonly List<SimpleToggle> _visemeButtons = new();
    private readonly List<VisualElement> _visemeContents = new();

    public VisualElement SelectedElement { get; } = new SpacedVerticalElement();
    public VisualElement AvailableElement { get; } = new SpacedVerticalElement();

    public LipSyncPanel(FacialShapesEditorContext context)
    {
        _context = context;
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
        var selected = _context.LipSync?.Mode == LipSyncSettings.Kind.Custom ? 1 : 0;
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
        if (_context.LipSyncProperty == null || _context.LipSync?.Mode == mode) return;
        var serialized = _context.SerializedObject;
        serialized.UpdateIfRequiredOrScript();
        var modeProperty = _context.LipSyncProperty.FindPropertyRelative(nameof(LipSyncSettings.Mode));
        modeProperty.intValue = (int)mode;
        if (mode == LipSyncSettings.Kind.Custom
            && _context.LipSync?.Mode == LipSyncSettings.Kind.BuiltIn
            && _context.BuiltInLipSync != null)
        {
            _context.LipSyncProperty
                .FindPropertyRelative(nameof(LipSyncSettings.Shapes))
                .CopyFrom(_context.BuiltInLipSync);
        }
        serialized.ApplyModifiedProperties();
        _context.NotifyLipSyncChanged();
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
        var values = (_context.PreviewLipSync ?? new VrcVisemeLipSyncShapes())
            .GetOrderedShapes();
        var search = _selectedSearch.value ?? string.Empty;
        for (var index = 0; index < VrcVisemeLipSyncShapes.Count; index++)
        {
            var visemeIndex = index;
            var section = new SpacedVerticalElement();
            section.AddToClassList("section-frame");
            var button = new SimpleToggle
            {
                text = VrcVisemeLipSyncShapes.Names[index],
                value = index == _context.SelectedViseme
            };
            button.RegisterValueChangedCallback(evt =>
            {
                if (!evt.newValue)
                {
                    button.SetValueWithoutNotify(true);
                    return;
                }
                _context.SetSelectedViseme(visemeIndex);
                UpdateSelection();
            });
            button.RegisterCallback<MouseEnterEvent>(_ =>
                _context.SetHoveredViseme(visemeIndex));
            button.RegisterCallback<MouseLeaveEvent>(_ =>
                _context.SetHoveredViseme(-1));
            section.Add(button);
            _visemeButtons.Add(button);

            var content = new SpacedVerticalElement();
            content.SetEnabled(_context.LipSync?.Mode == LipSyncSettings.Kind.Custom
                               && index == _context.SelectedViseme);
            foreach (var shape in values[index])
            {
                var shapeIndex = _context.DataManager.GetIndexForShape(shape.Name);
                if (search.Length > 0
                    && shape.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0
                    || _context.GroupManager.IsLeftSelected
                    && !_context.GroupManager.IsBlendShapeVisible(shapeIndex))
                    continue;
                content.Add(CreateShapeRow(index, shape));
            }
            section.Add(content);
            _visemeContents.Add(content);
            _sections.Add(section);
        }
    }

    private void UpdateSelection()
    {
        var editable = _context.LipSync?.Mode == LipSyncSettings.Kind.Custom;
        for (var index = 0; index < _visemeButtons.Count; index++)
        {
            _visemeButtons[index].SetValueWithoutNotify(index == _context.SelectedViseme);
            _visemeContents[index].SetEnabled(editable && index == _context.SelectedViseme);
        }
    }

    private VisualElement CreateShapeRow(int visemeIndex, BlendShapeWeight shape)
    {
        var row = new HorizontalElement();
        var name = new Label(shape.Name);
        name.style.flexGrow = 1f;
        var weight = new SliderFloatField { value = shape.Weight };
        weight.style.width = 180f;
        weight.RegisterValueChangedCallback(evt =>
            SetWeight(visemeIndex, shape.Name, evt.newValue));
        var remove = new Button(() => Remove(visemeIndex, shape.Name)) { text = "−" };
        remove.AddToClassList("compact-control");
        row.Add(name);
        row.Add(weight);
        row.Add(remove);
        return row;
    }

    private void SetWeight(int visemeIndex, string name, float weight)
    {
        if (GetVisemeProperty(visemeIndex) is not { } property) return;
        for (var index = 0; index < property.arraySize; index++)
        {
            var element = property.GetArrayElementAtIndex(index);
            if (element.FindPropertyRelative(BlendShapeWeight.NamePropName).stringValue != name)
                continue;
            element.FindPropertyRelative(BlendShapeWeight.WeightPropName).floatValue = weight;
            property.serializedObject.ApplyModifiedProperties();
            _context.NotifyLipSyncChanged();
            return;
        }
    }

    private void Remove(int visemeIndex, string name)
    {
        if (GetVisemeProperty(visemeIndex) is not { } property) return;
        for (var index = 0; index < property.arraySize; index++)
        {
            if (property.GetArrayElementAtIndex(index)
                    .FindPropertyRelative(BlendShapeWeight.NamePropName).stringValue != name)
                continue;
            property.DeleteArrayElementAtIndex(index);
            property.serializedObject.ApplyModifiedProperties();
            _context.NotifyLipSyncChanged();
            Rebuild();
            return;
        }
    }

    private void Add(string name)
    {
        if (_context.LipSync?.Mode != LipSyncSettings.Kind.Custom
            || GetVisemeProperty(_context.SelectedViseme) is not { } property)
            return;
        for (var index = 0; index < property.arraySize; index++)
        {
            if (property.GetArrayElementAtIndex(index)
                    .FindPropertyRelative(BlendShapeWeight.NamePropName).stringValue == name)
                return;
        }
        property.InsertArrayElementAtIndex(property.arraySize);
        property.GetArrayElementAtIndex(property.arraySize - 1)
            .CopyFrom(new BlendShapeWeight(name, 100f));
        property.serializedObject.ApplyModifiedProperties();
        _context.NotifyLipSyncChanged();
        Rebuild();
    }

    private SerializedProperty? GetVisemeProperty(int index)
    {
        if (_context.LipSyncProperty == null) return null;
        _context.SerializedObject.UpdateIfRequiredOrScript();
        return _context.LipSyncProperty
            .FindPropertyRelative(nameof(LipSyncSettings.Shapes))
            .FindPropertyRelative(VrcVisemeLipSyncShapes.PropertyNames[index]);
    }

    private void RebuildAvailable()
    {
        _availableNames.Clear();
        var search = _availableSearch.value ?? string.Empty;
        foreach (var name in _context.DataManager.AllKeys)
        {
            var shapeIndex = _context.DataManager.GetIndexForShape(name);
            if (_context.LipSyncUnavailableNames.Contains(name)
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
        var label = new Label();
        label.RegisterCallback<ClickEvent>(_ =>
        {
            if (label.userData is string name) Add(name);
        });
        label.RegisterCallback<MouseEnterEvent>(_ =>
        {
            if (label.userData is not string name) return;
            _context.PreviewManager.CurrentHoveredIndex =
                _context.DataManager.GetIndexForShape(name);
        });
        return label;
    }

    private void BindAvailableItem(VisualElement element, int index)
    {
        var name = _availableNames[index];
        element.userData = name;
        ((Label)element).text = name;
        element.SetEnabled(_context.LipSync?.Mode == LipSyncSettings.Kind.Custom);
    }
}
