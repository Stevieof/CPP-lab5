using Core.Abstractions;
using Core.Domain;

namespace Core.Storage;

/// <summary>
/// Декоратор: той самий контракт IProductStore, нова поведінка — кеш читань.
/// Перше читання завантажує знімок з внутрішнього сховища, далі GetAll/GetBySku
/// відповідають з пам'яті. Зміни йдуть у внутрішнє сховище і одразу оновлюють знімок
/// (write-through), тож кеш не застаріває. Що всередині — пам'ять чи файл — декоратор не знає.
/// </summary>
public sealed class CachingProductStore(IProductStore inner) : IProductStore
{
    private readonly IProductStore _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private Dictionary<string, Product>? _snapshot;

    public int InnerReads { get; private set; }
    public int CacheHits { get; private set; }
    public IProductStore Inner => _inner;
    public string InnerName => _inner.GetType().Name;

    public IReadOnlyList<Product> GetAll() => [.. Snapshot().Values];

    public Product? GetBySku(string sku) => Snapshot().GetValueOrDefault(sku.Trim());

    public void Add(Product product)
    {
        _inner.Add(product);
        Snapshot()[product.Sku] = product;
    }

    public void Update(Product product)
    {
        _inner.Update(product);
        Snapshot()[product.Sku] = product;
    }

    public bool Remove(string sku)
    {
        bool removed = _inner.Remove(sku);
        Snapshot().Remove(sku.Trim());
        return removed;
    }

    private Dictionary<string, Product> Snapshot()
    {
        if (_snapshot is not null)
        {
            CacheHits++;
            return _snapshot;
        }
        InnerReads++;
        _snapshot = _inner.GetAll().ToDictionary(p => p.Sku, StringComparer.OrdinalIgnoreCase);
        return _snapshot;
    }
}
