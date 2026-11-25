using Core.Abstractions;
using Core.Domain;
using Core.Dto;

namespace Core.Services;

/// <summary>
/// Сервіс складського обліку. Залежить лише від абстракції IProductStore, яку отримує через
/// конструктор (ручний DI). Правила товару перевіряє сам Product — сервіс їх не дублює,
/// а лише знаходить товар, викликає метод сутності і зберігає результат.
/// </summary>
public sealed class InventoryService(IProductStore store, TimeProvider? clock = null)
{
    private readonly IProductStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    private DateOnly Today => DateOnly.FromDateTime(_clock.GetLocalNow().DateTime);

    /// <summary>Нова картка товару: інваріанти перевіряє Product.Create, унікальність — сховище.</summary>
    public Product Register(ProductDto dto)
    {
        Product product = Product.FromDto(dto);
        _store.Add(product);
        return product;
    }

    public Product Receive(string sku, string batchNo, int quantity, DateOnly? expiryDate)
    {
        Product product = Get(sku);
        product.Receive(batchNo, quantity, expiryDate, Today);
        _store.Update(product);
        return product;
    }

    public Product Reserve(string sku, int quantity)
    {
        Product product = Get(sku);
        product.Reserve(quantity);
        _store.Update(product);
        return product;
    }

    public Product Release(string sku, int quantity)
    {
        Product product = Get(sku);
        product.Release(quantity);
        _store.Update(product);
        return product;
    }

    public Product Ship(string sku, int quantity)
    {
        Product product = Get(sku);
        product.Ship(quantity);
        _store.Update(product);
        return product;
    }

    public Product ChangePrice(string sku, decimal price)
    {
        Product product = Get(sku);
        product.ChangePrice(price);
        _store.Update(product);
        return product;
    }

    /// <summary>Правило сервісу: картку можна видалити лише тоді, коли товару на складі немає.</summary>
    public void Remove(string sku)
    {
        Product product = Get(sku);
        if (product.Stock > 0)
            throw new InvalidOperationException(
                $"Не можна видалити {product.Sku}: на складі ще {product.Stock} {product.Unit}");
        _store.Remove(product.Sku);
    }

    public IReadOnlyList<Product> All() => _store.GetAll();

    public Product? Find(string sku) => _store.GetBySku(sku);

    /// <summary>Пошук за умовою, яку передає той, хто викликає (додаткове завдання 2).</summary>
    public IReadOnlyList<Product> Search(Func<Product, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return [.. _store.GetAll().Where(predicate)];
    }

    private Product Get(string sku) =>
        _store.GetBySku(sku) ?? throw new InvalidOperationException($"Немає товару з артикулом {sku}");
}
