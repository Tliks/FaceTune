using System.Reflection;

namespace Aoyon.FaceTune.Gui;

internal static class SerializedPropertyExtensions
{
    internal static void CopyFrom(this SerializedProperty property, object? source)
    {
        if (source != null)
        {
            CopyValue(property, source);
            return;
        }

        if (property.propertyType == SerializedPropertyType.ObjectReference)
            property.objectReferenceValue = null;
        else if (property.propertyType == SerializedPropertyType.ManagedReference)
            property.managedReferenceValue = null;
    }

    /// <summary>
    /// Merges values into an array by key. Existing elements retain their indices;
    /// unmatched values are appended in source order.
    /// </summary>
    internal static void MergeArrayByKey<T>(
        this SerializedProperty property,
        IEnumerable<T> values,
        Func<SerializedProperty, string> getPropertyKey,
        Func<T, string> getValueKey,
        Action<SerializedProperty, T> copyValue,
        bool overwrite = false)
        => UpdateArrayByKey(property, values, getPropertyKey, getValueKey, copyValue, overwrite, false);

    /// <summary>
    /// Synchronizes an array by key while retaining surviving elements in their current order.
    /// Removed elements are deleted and new elements are appended in source order.
    /// </summary>
    internal static void SynchronizeArrayByKey<T>(
        this SerializedProperty property,
        IEnumerable<T> values,
        Func<SerializedProperty, string> getPropertyKey,
        Func<T, string> getValueKey,
        Action<SerializedProperty, T> copyValue,
        bool overwrite = false)
        => UpdateArrayByKey(property, values, getPropertyKey, getValueKey, copyValue, overwrite, true);

    private static void UpdateArrayByKey<T>(
        SerializedProperty property,
        IEnumerable<T> values,
        Func<SerializedProperty, string> getPropertyKey,
        Func<T, string> getValueKey,
        Action<SerializedProperty, T> copyValue,
        bool overwrite,
        bool removeMissing)
    {
        var orderedValues = new List<T>();
        var valuesByKey = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            var key = getValueKey(value);
            if (!valuesByKey.TryAdd(key, value)) continue;
            orderedValues.Add(value);
        }

        if (removeMissing)
        {
            var retainedKeys = new HashSet<string>(StringComparer.Ordinal);
            var removedIndices = new List<int>();
            for (var index = 0; index < property.arraySize; index++)
            {
                var key = getPropertyKey(property.GetArrayElementAtIndex(index));
                if (valuesByKey.ContainsKey(key) && retainedKeys.Add(key)) continue;
                removedIndices.Add(index);
            }
            for (var index = removedIndices.Count - 1; index >= 0; index--)
                property.DeleteArrayElementAtIndex(removedIndices[index]);
        }

        var indicesByKey = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < property.arraySize; index++)
            indicesByKey.TryAdd(getPropertyKey(property.GetArrayElementAtIndex(index)), index);

        foreach (var value in orderedValues)
        {
            var key = getValueKey(value);
            if (indicesByKey.TryGetValue(key, out var existingIndex))
            {
                if (overwrite) copyValue(property.GetArrayElementAtIndex(existingIndex), value);
                continue;
            }

            var index = property.arraySize;
            property.InsertArrayElementAtIndex(index);
            copyValue(property.GetArrayElementAtIndex(index), value);
            indicesByKey.Add(key, index);
        }
    }

    private static void CopyValue(SerializedProperty property, object source)
    {
        if (property.isArray && property.propertyType != SerializedPropertyType.String)
        {
            var values = (IList)source;
            property.arraySize = values.Count;
            for (var i = 0; i < values.Count; i++) CopyValue(property.GetArrayElementAtIndex(i), values[i]);
            return;
        }
        if (property.propertyType != SerializedPropertyType.Generic)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer: property.intValue = Convert.ToInt32(source); break;
                case SerializedPropertyType.Boolean: property.boolValue = (bool)source; break;
                case SerializedPropertyType.Float: property.floatValue = Convert.ToSingle(source); break;
                case SerializedPropertyType.String: property.stringValue = source.ToString(); break;
                case SerializedPropertyType.Enum: property.intValue = Convert.ToInt32(source); break;
                case SerializedPropertyType.ObjectReference: property.objectReferenceValue = source as Object; break;
                case SerializedPropertyType.ManagedReference: property.managedReferenceValue = source; break;
                case SerializedPropertyType.AnimationCurve: property.animationCurveValue = (AnimationCurve)source; break;
                case SerializedPropertyType.Vector2: property.vector2Value = (Vector2)source; break;
                case SerializedPropertyType.Vector3: property.vector3Value = (Vector3)source; break;
            }
            return;
        }

        var child = property.Copy();
        if (!child.Next(true)) return;
        var depth = child.depth;
        var type = source.GetType();
        do
        {
            if (child.depth != depth) break;
            var field = type.GetField(child.name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var value = field?.GetValue(source);
            if (field == null) continue;
            if (value == null)
            {
                if (child.isArray && child.propertyType != SerializedPropertyType.String) child.ClearArray();
                else if (child.propertyType == SerializedPropertyType.ObjectReference) child.objectReferenceValue = null;
                else if (child.propertyType == SerializedPropertyType.ManagedReference) child.managedReferenceValue = null;
                continue;
            }
            CopyValue(child, value);
        } while (child.Next(false));
    }
}
