using UnityEngine.UIElements;

namespace aoyon.facetune.ui.components;

internal class SimpleToggle : VisualElement
{
    public new class UxmlFactory : UxmlFactory<SimpleToggle, UxmlTraits> { }

    public new class UxmlTraits : VisualElement.UxmlTraits
    {
        readonly UxmlBoolAttributeDescription value = new() { name = "value", defaultValue = false };
        readonly UxmlStringAttributeDescription text = new() { name = "text", defaultValue = "" };

        public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
        {
            base.Init(ve, bag, cc);
            var element = (ve as SimpleToggle)!;
            element.value = value.GetValueFromBag(bag, cc);
            element.text = text.GetValueFromBag(bag, cc);
        }
    }

    private readonly Button _button;
    private bool _value;
    private string _text = "";

    public bool value
    {
        get => _value;
        set
        {
            _value = value;
            UpdateVisualState();
        }
    }

    public string text
    {
        get => _text;
        set
        {
            _text = value;
            _button.text = value;
        }
    }

    public SimpleToggle()
    {
        _button = new Button()
        {
            text = ""
        };

        Add(_button);

        _button.clicked += () =>
        {
            var previousValue = _value;
            _value = !_value;
            UpdateVisualState();

            using var changeEvent = ChangeEvent<bool>.GetPooled(previousValue, _value);
            changeEvent.target = this;
            SendEvent(changeEvent);
        };

        UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        if (_value)
        {
            _button.style.color = new StyleColor(new Color(0.55f, 0.75f, 0.9f));
        }
        else
        {
            _button.style.color = new StyleColor(new Color(1f, 1f, 1f, 0.7f));
        }
    }

    public void SetValueWithoutNotify(bool newValue)
    {
        _value = newValue;
        UpdateVisualState();
    }

    public void RegisterValueChangedCallback(EventCallback<ChangeEvent<bool>> callback)
    {
        RegisterCallback(callback);
    }

    public void UnregisterValueChangedCallback(EventCallback<ChangeEvent<bool>> callback)
    {
        UnregisterCallback(callback);
    }
} 