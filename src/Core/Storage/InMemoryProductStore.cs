using Core.Abstractions;
using Core.Domain;

namespace Core.Storage;

/// <summary>
/// Сховище в пам'яті: словник «артикул → товар». Дані живуть, поки працює процес.
/// GetBySku повертає той самий об'єкт, тому Update тут лише перевіряє, що товар існує.
/// </summary>
public sealed class InMemoryProductStore : IProductStore
{
    private readonly Dictionary<string, Product> _items = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryProductStore(IEnumerable<Product>? seed = null)
    {
        foreach (Product product in seed ?? [])
            Add(product);
    }

    public IReadOnlyList<Product> GetAll() => [.. _items.Values];

    public Product? GetBySku(string sku) => _items.GetValueOrDefault(sku.Trim());

    public void Add(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);
        if (!_items.TryAdd(product.Sku, product))
            throw new InvalidOperationException($"Товар {product.Sku} уже є у сховищі");
    }

    public void Update(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);
        if (!_items.ContainsKey(product.Sku))
            throw new InvalidOperationException($"Товару {product.Sku} немає у сховищі");
        _items[product.Sku] = product;
    }

    public bool Remove(string sku) => _items.Remove(sku.Trim());
}
