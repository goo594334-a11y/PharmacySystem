using Microsoft.EntityFrameworkCore;

namespace PharmacySystem.Models.Common;

/// <summary>
/// Reusable async pagination wrapper for EF Core queries.
/// Preserves memory and bandwidth by fetching only requested page records.
/// </summary>
/// <typeparam name="T">Entity or ViewModel type</typeparam>
public class PaginatedList<T>
{
    public List<T> Items { get; }
    public int PageIndex { get; }
    public int TotalPages { get; }
    public int TotalCount { get; }
    public int PageSize { get; }

    public bool HasPreviousPage => PageIndex > 1;
    public bool HasNextPage => PageIndex < TotalPages;

    public PaginatedList(List<T> items, int count, int pageIndex, int pageSize)
    {
        PageIndex = pageIndex;
        PageSize = pageSize;
        TotalCount = count;
        TotalPages = (int)Math.Ceiling(count / (double)pageSize);
        if (TotalPages == 0 && count == 0)
        {
            TotalPages = 1;
        }
        Items = items;
    }

    public static async Task<PaginatedList<T>> CreateAsync(
        IQueryable<T> source,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (pageIndex < 1) pageIndex = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100; // Protection against excessive page size requests

        var count = await source.CountAsync(cancellationToken);
        var items = await source
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedList<T>(items, count, pageIndex, pageSize);
    }

    public PaginationViewModel ToPaginationViewModel(string action = "Index", string? controller = null, IDictionary<string, string?>? routeValues = null)
    {
        return new PaginationViewModel
        {
            CurrentPage = PageIndex,
            PageSize = PageSize,
            TotalCount = TotalCount,
            TotalPages = TotalPages,
            Action = action,
            Controller = controller,
            RouteValues = routeValues ?? new Dictionary<string, string?>()
        };
    }
}
