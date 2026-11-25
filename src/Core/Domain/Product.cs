using Core.Dto;

namespace Core.Domain;

/// <summary>Партія товару на складі. Залишок змінює лише Product.</summary>
public sealed class StockBatch
{
    public string BatchNo { get; }
    public DateOnly? ExpiryDate { get; }
    public int Remaining { get; internal set; }

    internal StockBatch(string batchNo, int quantity, DateOnly? expiryDate)
    {
        BatchNo = batchNo;
        Remaining = quantity;
        ExpiryDate = expiryDate;
    }
}

/// <summary>
/// Товар із залишком, що складається з партій. Інваріант: 0 ≤ Reserved ≤ Stock.
/// Стан змінюється лише через Receive, Reserve, Release, Ship і ChangePrice.
/// </summary>
public sealed class Product
{
    private readonly List<StockBatch> _batches = [];
    private int _reserved;

    public string Sku { get; }
    public string Name { get; }
    public string Category { get; }
    public string Unit { get; }
    public decimal Price { get; private set; }
    public int? MinStock { get; }

    public int Stock => _batches.Sum(b => b.Remaining);
    public int Reserved => _reserved;
    public int Available => Stock - _reserved;
    public bool IsBelowMinimum => MinStock is int min && Stock < min;
    public IReadOnlyList<StockBatch> Batches => _batches.AsReadOnly();

    private Product(string sku, string name, string category, string unit, decimal price, int? minStock)
    {
        Sku = sku;
        Name = name;
        Category = category;
        Unit = unit;
        Price = price;
        MinStock = minStock;
    }

    public static Product Create(string sku, string name, string category, string unit,
                                 decimal price, int? minStock = null)
    {
        if (string.IsNullOrWhiteSpace(sku) || !sku.Trim().StartsWith("SKU-", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Артикул '{sku}' має бути у форматі SKU-…", nameof(sku));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Назва товару не може бути порожньою", nameof(name));
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Категорія товару обов'язкова", nameof(category));
        if (string.IsNullOrWhiteSpace(unit))
            throw new ArgumentException("Одиниця виміру не може бути порожньою", nameof(unit));
        if (price <= 0)
            throw new ArgumentOutOfRangeException(nameof(price), price, "Ціна має бути більшою за нуль");
        if (minStock is < 0)
            throw new ArgumentOutOfRangeException(nameof(minStock), minStock,
                "Мінімальний залишок не може бути від'ємним");

        return new Product(sku.Trim().ToUpperInvariant(), name.Trim(), category.Trim().ToLowerInvariant(),
                           unit.Trim(), decimal.Round(price, 2), minStock);
    }

    /// <summary>Надходження партії. Прострочену партію прийняти не можна.</summary>
    public void Receive(string batchNo, int quantity, DateOnly? expiryDate, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(batchNo))
            throw new ArgumentException("Номер партії обов'язковий", nameof(batchNo));
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity,
                "Кількість у партії має бути більшою за нуль");
        if (expiryDate < today)
            throw new InvalidOperationException(
                $"Партія {batchNo} прострочена ({expiryDate:dd.MM.yyyy}), прийняти не можна");
        if (_batches.Any(b => b.BatchNo == batchNo.Trim()))
            throw new InvalidOperationException($"Партія {batchNo} товару {Sku} уже оприбуткована");

        _batches.Add(new StockBatch(batchNo.Trim(), quantity, expiryDate));
    }

    /// <summary>Резерв під замовлення: не більше, ніж вільний залишок.</summary>
    public void Reserve(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity,
                "Кількість для резерву має бути більшою за нуль");
        if (quantity > Available)
            throw new InvalidOperationException(
                $"Не можна зарезервувати {quantity} {Unit} {Sku}: вільно {Available}");

        _reserved += quantity;
    }

    public void Release(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity,
                "Кількість для зняття резерву має бути більшою за нуль");
        if (quantity > _reserved)
            throw new InvalidOperationException(
                $"Не можна зняти резерв {quantity} {Unit} {Sku}: зарезервовано {_reserved}");

        _reserved -= quantity;
    }

    /// <summary>Відвантаження зарезервованого товару: спершу партії з найближчим терміном (FEFO).</summary>
    public void Ship(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity,
                "Кількість для відвантаження має бути більшою за нуль");
        if (quantity > _reserved)
            throw new InvalidOperationException(
                $"Не можна відвантажити {quantity} {Unit} {Sku}: зарезервовано лише {_reserved}");

        int left = quantity;
        foreach (StockBatch batch in _batches.Where(b => b.Remaining > 0)
                                             .OrderBy(b => b.ExpiryDate ?? DateOnly.MaxValue))
        {
            int take = Math.Min(left, batch.Remaining);
            batch.Remaining -= take;
            left -= take;
            if (left == 0)
                break;
        }
        _batches.RemoveAll(b => b.Remaining == 0);
        _reserved -= quantity;
    }

    public void ChangePrice(decimal newPrice)
    {
        if (newPrice <= 0)
            throw new ArgumentOutOfRangeException(nameof(newPrice), newPrice, "Ціна має бути більшою за нуль");

        Price = decimal.Round(newPrice, 2);
    }

    public ProductDto ToDto() => new(Sku, Name, Category, Unit, Price, MinStock);

    /// <summary>Відновлення з DTO проходить ті самі перевірки, що й Create.</summary>
    public static Product FromDto(ProductDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return Create(dto.Sku, dto.Name, dto.Category, dto.Unit, dto.Price, dto.MinStock);
    }

    /// <summary>Знімок повного стану для сховища (лабораторна 5).</summary>
    public ProductStateDto ToState() =>
        new(Sku, Name, Category, Unit, Price, MinStock, _reserved,
            [.. _batches.Select(b => new BatchStateDto(b.BatchNo, b.Remaining, b.ExpiryDate))]);

    /// <summary>
    /// Відновлення зі сховища. Картка проходить Create, партії і резерв — ті самі інваріанти,
    /// тож пошкоджений файл не створить товар у некоректному стані. Термін придатності тут
    /// не перевіряється: партію вже прийнято раніше, а прострочення — це факт, а не помилка даних.
    /// </summary>
    public static Product FromState(ProductStateDto state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Product product = Create(state.Sku, state.Name, state.Category, state.Unit, state.Price, state.MinStock);

        foreach (BatchStateDto batch in state.Batches ?? [])
        {
            if (string.IsNullOrWhiteSpace(batch.BatchNo))
                throw new ArgumentException($"Партія товару {product.Sku} без номера", nameof(state));
            if (batch.Remaining <= 0)
                throw new ArgumentOutOfRangeException(nameof(state), batch.Remaining,
                    $"Залишок партії {batch.BatchNo} має бути більшим за нуль");
            if (product._batches.Any(b => b.BatchNo == batch.BatchNo.Trim()))
                throw new InvalidOperationException($"Партія {batch.BatchNo} товару {product.Sku} повторюється");

            product._batches.Add(new StockBatch(batch.BatchNo.Trim(), batch.Remaining, batch.ExpiryDate));
        }

        if (state.Reserved < 0 || state.Reserved > product.Stock)
            throw new InvalidOperationException(
                $"Резерв {state.Reserved} товару {product.Sku} поза межами 0…{product.Stock}");
        product._reserved = state.Reserved;
        return product;
    }

    public override string ToString() =>
        $"{Sku} {Name}: залишок {Stock}, резерв {Reserved}, вільно {Available} {Unit}" +
        (IsBelowMinimum ? $" (нижче мінімуму {MinStock})" : "");
}
