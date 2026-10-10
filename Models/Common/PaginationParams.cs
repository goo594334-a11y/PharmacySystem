namespace PharmacySystem.Models.Common;

/// <summary>
/// Common query parameters for search, filtering, sorting, and pagination.
/// </summary>
public class PaginationParams
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? SearchTerm { get; set; }
    public string? SortColumn { get; set; }
    public string? SortDirection { get; set; } = "asc";

    public bool IsDescending => string.Equals(SortDirection, "desc", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Generates a dictionary of active route values to preserve during page navigation.
    /// </summary>
    public IDictionary<string, string?> ToRouteDictionary()
    {
        var dict = new Dictionary<string, string?>();

        if (PageSize != 10) dict["pageSize"] = PageSize.ToString();
        if (!string.IsNullOrWhiteSpace(SearchTerm)) dict["searchTerm"] = SearchTerm.Trim();
        if (!string.IsNullOrWhiteSpace(SortColumn)) dict["sortColumn"] = SortColumn;
        if (!string.IsNullOrWhiteSpace(SortDirection) && SortDirection != "asc") dict["sortDirection"] = SortDirection;

        return dict;
    }
}
