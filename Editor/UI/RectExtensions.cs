namespace Aoyon.FaceTune.Gui;

/// <summary>Mutating operations for a vertical IMGUI layout cursor.</summary>
internal static class RectExtensions
{
    private const float DefaultSpace = 6f;
    private const float IndentWidth = 15f;

    public static Rect SetSingleHeight(this ref Rect rect)
    {
        rect.height = EditorGUIUtility.singleLineHeight;
        return rect;
    }

    public static Rect SetHeight(this ref Rect rect, float height)
    {
        rect.height = height;
        return rect;
    }

    public static Rect NewLine(this ref Rect rect)
    {
        rect.y = rect.yMax + EditorGUIUtility.standardVerticalSpacing;
        return rect;
    }

    public static Rect Space(this ref Rect rect, float amount = DefaultSpace)
    {
        rect.y += amount + EditorGUIUtility.standardVerticalSpacing;
        return rect;
    }

    public static Rect Indent(this ref Rect rect, int count = 1)
    {
        var offset = IndentWidth * count;
        rect.x += offset;
        rect.width = Mathf.Max(0f, rect.width - offset);
        return rect;
    }

    public static Rect Back(this ref Rect rect, int count = 1)
    {
        var offset = IndentWidth * count;
        rect.x -= offset;
        rect.width += offset;
        return rect;
    }

    public static (Rect left, Rect right) SplitLeft(this Rect source, float width)
    {
        width = Mathf.Clamp(width, 0f, source.width);
        var left = new Rect(source.x, source.y, width, source.height);
        var rightX = Mathf.Min(source.xMax, left.xMax + EditorGUIUtility.standardVerticalSpacing);
        return (left, new Rect(rightX, source.y, Mathf.Max(0f, source.xMax - rightX), source.height));
    }

    public static (Rect left, Rect right) SplitRight(this Rect source, float width)
    {
        width = Mathf.Clamp(width, 0f, source.width);
        var right = new Rect(source.xMax - width, source.y, width, source.height);
        var leftWidth = Mathf.Max(0f, right.x - EditorGUIUtility.standardVerticalSpacing - source.x);
        return (new Rect(source.x, source.y, leftWidth, source.height), right);
    }

    public static (Rect left, Rect right) SplitRatio(this Rect source, float ratio)
        => source.SplitLeft(source.width * Mathf.Clamp01(ratio));
}
