using QuickGridColumns = Microsoft.AspNetCore.Components.QuickGrid;

namespace NflCoverage.Web.Components.Shared;

// MudBlazor also has PropertyColumn and TemplateColumn components, so QuickGrid's are exposed under distinct names.

/// <summary>QuickGrid's <c>PropertyColumn</c> for numbers: right-aligned unless <c>Align</c> says otherwise.</summary>
public sealed class NumberColumn<TGridItem, TProp> : QuickGridColumns.PropertyColumn<TGridItem, TProp>
{
    public NumberColumn() => Align = QuickGridColumns.Align.End;
}

/// <summary>QuickGrid's <c>TemplateColumn</c>.</summary>
public sealed class GridTemplateColumn<TGridItem> : QuickGridColumns.TemplateColumn<TGridItem>;
