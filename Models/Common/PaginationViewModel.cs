namespace PharmacySystem.Models.Common;

/// <summary>
/// ViewModel used by the reusable _Pagination.cshtml component.
/// Preserves active search, filter, and sort route values across page transitions.
/// </summary>
public class PaginationViewModel
{
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public int PageSize { get; set; }
    public string Action { get; set; } = "Index";
    public string? Controller { get; set; }
    public IDictionary<string, string?> RouteValues { get; set; } = new Dictionary<string, string?>();

    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;

    public int FirstItemIndex => TotalCount == 0 ? 0 : ((CurrentPage - 1) * PageSize) + 1;
    public int LastItemIndex => Math.Min(CurrentPage * PageSize, TotalCount);

    /// <summary>
    /// Calculates window of visible page numbers around current page for clean display.
    /// </summary>
    public IEnumerable<int> GetVisiblePageNumbers(int windowSize = 5)
    {
        if (TotalPages <= windowSize)
        {
            return Enumerable.Range(1, TotalPages);
        }

        int half = windowSize / 2;
        int start = Math.Max(1, CurrentPage - half);
        int end = Math.Min(TotalPages, start + windowSize - 1);

        if (end - start + 1 < windowSize)
        {
            start = Math.Max(1, end - windowSize + 1);
        }

        return Enumerable.Range(start, end - start + 1);
    }

    public IDictionary<string, string?> GetRouteValuesForPage(int page)
    {
        var dict = new Dictionary<string, string?>(RouteValues)
        {
            ["page"] = page.ToString()
        };
        return dict;
    }
}
