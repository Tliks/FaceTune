using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.Components;

internal class PlaceholderTextField : TextField
{
    public new class UxmlFactory : UxmlFactory<PlaceholderTextField, UxmlTraits> { }

    public new class UxmlTraits : TextField.UxmlTraits
    {
        private readonly UxmlStringAttributeDescription _placeholderAttribute = new() { name = "placeholder", defaultValue = "" };

        public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
        {
            base.Init(ve, bag, cc);
            var element = (ve as PlaceholderTextField)!;
            element.Placeholder = _placeholderAttribute.GetValueFromBag(bag, cc);
        }
    }

    private readonly Label _placeholderLabel;
    private string _placeholder = string.Empty;

    public string Placeholder
    {
        get => _placeholder;
        set
        {
            _placeholder = value;
            _placeholderLabel.text = value;
            UpdatePlaceholderVisibility();
        }
    }

    public override string value
    {
        get => base.value;
        set
        {
            base.value = value;
            UpdatePlaceholderVisibility();
        }
    }

    public PlaceholderTextField() : this(null) { }

    public PlaceholderTextField(string? label) : base(label)
    {
        _placeholderLabel = new Label
        {
            pickingMode = PickingMode.Ignore
        };
        
        _placeholderLabel.style.position = Position.Absolute;
        _placeholderLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f, 0.5f));
        _placeholderLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
        _placeholderLabel.style.left = 3;
        _placeholderLabel.style.right = 0;
        _placeholderLabel.style.top = 0;
        _placeholderLabel.style.bottom = 0;

        var inputElement = this.Q(className: "unity-base-text-field__input");
        inputElement?.Add(_placeholderLabel);

        this.RegisterValueChangedCallback(_ => UpdatePlaceholderVisibility());
        
        RegisterCallback<AttachToPanelEvent>(_ => {
            if (_placeholderLabel.parent == null)
            {
                this.Q(className: "unity-base-text-field__input")?.Add(_placeholderLabel);
            }
            UpdatePlaceholderVisibility();
        });
    }

    private void UpdatePlaceholderVisibility()
    {
        if (_placeholderLabel == null) return;
        _placeholderLabel.style.display = string.IsNullOrEmpty(value) ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public override void SetValueWithoutNotify(string newValue)
    {
        base.SetValueWithoutNotify(newValue);
        UpdatePlaceholderVisibility();
    }
}

