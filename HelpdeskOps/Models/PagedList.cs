using Microsoft.EntityFrameworkCore;

namespace HelpdeskOps.Models;

// One page of a long list, plus what the pager needs to link to the other pages
public class PagedList<T> : IReadOnlyList<T>, IPagedList
{
    private readonly List<T> _items;

    private PagedList(List<T> items, int page, int pageSize, int total)
    {
        _items = items;
        Page = page;
        PageSize = pageSize;
        Total = total;
    }

    public int Page { get; }

    public int PageSize { get; }

    public int Total { get; }

    public int PageCount => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));

    public int Count => _items.Count;

    public T this[int index] => _items[index];

    // Counts and loads only the requested page in the database. A page number past the end shows the last page.
    public static async Task<PagedList<T>> CreateAsync(IQueryable<T> source, int page, int pageSize)
    {
        var total = await source.CountAsync();
        page = ClampPage(page, total, pageSize);
        var items = await source.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PagedList<T>(items, page, pageSize, total);
    }

    // For lists that are already in memory, such as rows filtered after loading
    public static PagedList<T> Create(IEnumerable<T> source, int page, int pageSize)
    {
        var all = source.ToList();
        page = ClampPage(page, all.Count, pageSize);
        return new PagedList<T>(all.Skip((page - 1) * pageSize).Take(pageSize).ToList(), page, pageSize, all.Count);
    }

    private static int ClampPage(int page, int total, int pageSize) =>
        Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)));

    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

public interface IPagedList
{
    int Page { get; }

    int PageCount { get; }

    int Total { get; }
}
