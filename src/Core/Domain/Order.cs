namespace Core.Domain;

public enum OrderStatus { Draft, Reserved, Shipped, Cancelled }

public sealed record OrderLine(Product Product, int Quantity);

/// <summary>
/// Замовлення клієнта на відвантаження. Життєвий цикл:
/// Draft ──Reserve()──► Reserved ──Ship()──► Shipped;
/// Draft або Reserved ──Cancel()──► Cancelled (резерв знімається).
/// </summary>
public sealed class Order
{
    private readonly List<OrderLine> _lines = [];

    public string Number { get; }
    public string Customer { get; }
    public OrderStatus Status { get; private set; } = OrderStatus.Draft;
    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();
    public decimal Total => _lines.Sum(l => l.Product.Price * l.Quantity);

    public Order(string number, string customer)
    {
        if (string.IsNullOrWhiteSpace(number))
            throw new ArgumentException("Номер замовлення обов'язковий", nameof(number));
        if (string.IsNullOrWhiteSpace(customer))
            throw new ArgumentException("Клієнт обов'язковий", nameof(customer));

        Number = number.Trim();
        Customer = customer.Trim();
    }

    public static bool CanTransition(OrderStatus from, OrderStatus to) => (from, to) switch
    {
        (OrderStatus.Draft, OrderStatus.Reserved) => true,
        (OrderStatus.Reserved, OrderStatus.Shipped) => true,
        (OrderStatus.Draft or OrderStatus.Reserved, OrderStatus.Cancelled) => true,
        _ => false   // відвантажене замовлення вже не скасовується
    };

    public void AddLine(Product product, int quantity)
    {
        ArgumentNullException.ThrowIfNull(product);
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity,
                "Кількість у рядку замовлення має бути більшою за нуль");
        if (Status != OrderStatus.Draft)
            throw new InvalidOperationException($"Замовлення {Number} у стані {Status}: рядки додаються лише в Draft");

        _lines.Add(new OrderLine(product, quantity));
    }

    /// <summary>Резервування за принципом «все або нічого»: спершу перевіряються всі рядки.</summary>
    public void Reserve()
    {
        EnsureTransition(OrderStatus.Reserved);
        if (_lines.Count == 0)
            throw new InvalidOperationException($"Замовлення {Number} порожнє, резервувати нічого");

        foreach (var group in _lines.GroupBy(l => l.Product))
        {
            int needed = group.Sum(l => l.Quantity);
            if (needed > group.Key.Available)
                throw new InvalidOperationException(
                    $"Замовлення {Number}: {group.Key.Sku} потрібно {needed}, вільно {group.Key.Available}");
        }

        foreach (OrderLine line in _lines)
            line.Product.Reserve(line.Quantity);
        Status = OrderStatus.Reserved;
    }

    public void Ship()
    {
        EnsureTransition(OrderStatus.Shipped);
        foreach (OrderLine line in _lines)
            line.Product.Ship(line.Quantity);
        Status = OrderStatus.Shipped;
    }

    public void Cancel()
    {
        EnsureTransition(OrderStatus.Cancelled);
        if (Status == OrderStatus.Reserved)
            foreach (OrderLine line in _lines)
                line.Product.Release(line.Quantity);
        Status = OrderStatus.Cancelled;
    }

    private void EnsureTransition(OrderStatus to)
    {
        if (!CanTransition(Status, to))
            throw new InvalidOperationException($"Замовлення {Number}: перехід {Status} → {to} заборонено");
    }
}
