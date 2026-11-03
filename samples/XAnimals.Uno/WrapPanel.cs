using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace XAnimals.Uno;

/// <summary>
/// Lays its children out in rows and starts a new row when one no longer fits. Each child keeps its own size:
/// VariableSizedWrapGrid gives every cell the size of the first child, which clips the others.
/// </summary>
public partial class WrapPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        double x = 0, rowHeight = 0, height = 0, width = 0;
        foreach (var child in Children)
        {
            child.Measure(new Size(availableSize.Width, availableSize.Height));
            var size = child.DesiredSize;
            if (x > 0 && x + size.Width > availableSize.Width)
            {
                height += rowHeight;
                x = 0;
                rowHeight = 0;
            }
            x += size.Width;
            rowHeight = Math.Max(rowHeight, size.Height);
            width = Math.Max(width, x);
        }
        return new Size(width, height + rowHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = 0, y = 0, rowHeight = 0;
        foreach (var child in Children)
        {
            var size = child.DesiredSize;
            if (x > 0 && x + size.Width > finalSize.Width)
            {
                y += rowHeight;
                x = 0;
                rowHeight = 0;
            }
            child.Arrange(new Rect(x, y, size.Width, size.Height));
            x += size.Width;
            rowHeight = Math.Max(rowHeight, size.Height);
        }
        return new Size(finalSize.Width, y + rowHeight);
    }
}
