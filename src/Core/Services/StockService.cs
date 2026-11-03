using Core.Domain;

namespace Core.Services;

/// <summary>
/// Правила двох сутностей: товар можна зберігати лише на складі, що приймає його категорію,
/// і лише на одному складі; надходження приймається тільки для розміщеного товару.
/// </summary>
public sealed class StockService
{
    private readonly Dictionary<string, Warehouse> _warehouseBySku = [];

    public void Place(Product product, Warehouse warehouse)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(warehouse);

        if (!warehouse.Accepts(product.Category))
            throw new InvalidOperationException(
                $"Склад {warehouse.Id} не приймає категорію «{product.Category}» ({product.Sku})");
        if (_warehouseBySku.TryGetValue(product.Sku, out Warehouse? current))
            throw new InvalidOperationException($"Товар {product.Sku} уже розміщено на складі {current.Id}");

        _warehouseBySku[product.Sku] = warehouse;
    }

    public void Receive(Product product, string batchNo, int quantity, DateOnly? expiryDate, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(product);

        if (!_warehouseBySku.ContainsKey(product.Sku))
            throw new InvalidOperationException($"Товар {product.Sku} не розміщено на жодному складі");

        product.Receive(batchNo, quantity, expiryDate, today);
    }

    public Warehouse? WarehouseOf(Product product) =>
        _warehouseBySku.GetValueOrDefault(product.Sku);
}
