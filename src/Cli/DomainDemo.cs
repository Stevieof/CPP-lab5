using Core.Domain;
using Core.Dto;
using Core.Import;
using Core.Services;

/// <summary>Демонстраційні сценарії доменної моделі (лабораторна 4). Лише виклики Core і вивід.</summary>
internal static class DomainDemo
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    public static void Run()
    {
        Title("Сценарій 1: коректні операції");
        Product milk = Product.Create("sku-101", "Молоко 2,5% 900 г", "Молочне", "шт", 42.90m, 30);
        Console.WriteLine($"Create:            {milk}");
        milk.Receive("П-0317-1", 40, Today.AddDays(5), Today);
        milk.Receive("П-0318-1", 60, Today.AddDays(9), Today);
        Console.WriteLine($"Receive 40 + 60:   {milk}");
        milk.Reserve(50);
        Console.WriteLine($"Reserve(50):       {milk}");
        milk.Ship(45);
        Console.WriteLine($"Ship(45):          {milk}");
        Console.WriteLine($"Партії (FEFO):     {string.Join(", ", milk.Batches.Select(b => $"{b.BatchNo} = {b.Remaining}"))}");
        milk.Release(5);
        Console.WriteLine($"Release(5):        {milk}");

        Title("Сценарій 2: спроби порушити інваріанти");
        Try("Create з артикулом 'MLK-1'", () => Product.Create("MLK-1", "Ряжанка", "молочне", "шт", 38m));
        Try("Create з ціною 0", () => Product.Create("SKU-102", "Кефір", "молочне", "шт", 0m));
        Try("Receive простроченої партії", () => milk.Receive("П-0301-1", 10, Today.AddDays(-2), Today));
        Try("Receive тієї ж партії вдруге", () => milk.Receive("П-0318-1", 10, Today.AddDays(9), Today));
        Try("Reserve(100)", () => milk.Reserve(100));
        Try("Ship(10) без резерву", () => milk.Ship(10));
        Try("Release(1) без резерву", () => milk.Release(1));
        Try("ChangePrice(-5)", () => milk.ChangePrice(-5m));
        Console.WriteLine($"Після всіх відмов: {milk}");

        Title("ToDto / FromDto");
        ProductDto dto = milk.ToDto();
        Console.WriteLine($"ToDto:   {dto}");
        Console.WriteLine($"FromDto: {Product.FromDto(dto)}");
        Try("FromDto(dto with { Price = -1 })", () => Product.FromDto(dto with { Price = -1 }));
    }

    public static void Import(string path)
    {
        Title($"Імпорт сутностей: {path}");
        ImportResult<ProductDto> raw = Path.GetExtension(path).ToLowerInvariant() == ".json"
            ? ProductJsonImporter.Load(path)
            : ProductCsvImporter.Load(path);
        ImportResult<Product> result = ProductDomainImporter.Convert(raw);

        Console.WriteLine($"Створено сутностей: {result.Items.Count}");
        foreach (Product product in result.Items)
            Console.WriteLine($"  {product.Sku} {product.Name} ({product.Category}), {product.Price:F2} грн");
        Console.WriteLine($"Не пройшли перевірки: {result.Errors.Count}");
        foreach (string error in result.Errors)
            Console.WriteLine($"  {error}");
    }

    public static void Warehouses()
    {
        Title("Категорії складу (StockService)");
        var service = new StockService();
        Warehouse cold = Warehouse.Create("W-COLD", "Холодильний склад", "молочне", "заморожене");
        Warehouse dry = Warehouse.Create("W-DRY", "Сухий склад", "бакалія", "напої");
        Product milk = Product.Create("SKU-101", "Молоко 2,5% 900 г", "молочне", "шт", 42.90m, 30);
        Product buckwheat = Product.Create("SKU-301", "Гречка ядриця 1 кг", "бакалія", "уп", 56.40m, 40);
        Product carrot = Product.Create("SKU-502", "Морква", "овочі", "кг", 12.40m);

        Console.WriteLine($"Склади: {cold}; {dry}");
        service.Place(milk, cold);
        service.Place(buckwheat, dry);
        Console.WriteLine($"Розміщено: {milk.Sku} → {service.WarehouseOf(milk)!.Id}, {buckwheat.Sku} → {service.WarehouseOf(buckwheat)!.Id}");
        service.Receive(milk, "П-0317-1", 120, Today.AddDays(7), Today);
        Console.WriteLine($"Receive на {cold.Id}: {milk}");

        Try($"Place({milk.Sku}, {dry.Id})", () => service.Place(milk, dry));
        Try($"Place({carrot.Sku} «овочі», {cold.Id})", () => service.Place(carrot, cold));
        Try($"Place({carrot.Sku} «овочі», {dry.Id})", () => service.Place(carrot, dry));
        Try($"Receive({carrot.Sku}) без складу", () => service.Receive(carrot, "П-0320-1", 50, null, Today));
        Try("Warehouse.Create без категорій", () => Warehouse.Create("W-EMPTY", "Порожній склад"));
        Console.WriteLine($"Після відмов: {milk}; {carrot}");
    }

    public static void Orders()
    {
        Title("Таблиця переходів станів замовлення");
        OrderStatus[] states = Enum.GetValues<OrderStatus>();
        Console.WriteLine($"{"з \\ в",-10}" + string.Concat(states.Select(s => $"{s,-11}")));
        foreach (OrderStatus from in states)
            Console.WriteLine($"{from,-10}" + string.Concat(states.Select(to =>
                $"{(Order.CanTransition(from, to) ? "так" : "—"),-11}")));

        Product milk = Product.Create("SKU-101", "Молоко 2,5% 900 г", "молочне", "шт", 42.90m, 30);
        Product dumplings = Product.Create("SKU-201", "Пельмені домашні 800 г", "заморожене", "уп", 129.90m, 10);
        milk.Receive("П-0317-1", 60, Today.AddDays(6), Today);
        dumplings.Receive("П-0317-2", 20, Today.AddDays(120), Today);

        Title("Замовлення З-001: Draft → Reserved → Shipped");
        var order = new Order("З-001", "Магазин «Сільпо», Сихів");
        order.AddLine(milk, 24);
        order.AddLine(dumplings, 8);
        Console.WriteLine($"Сума замовлення: {order.Total:F2} грн");
        order.Reserve();
        Console.WriteLine($"Reserve(): {order.Status}; {Short(milk)}; {Short(dumplings)}");
        Try("AddLine у стані Reserved", () => order.AddLine(milk, 1));
        order.Ship();
        Console.WriteLine($"Ship():    {order.Status}; {Short(milk)}; {Short(dumplings)}");
        Try("Cancel() відвантаженого", order.Cancel);

        Title("Замовлення З-002: «все або нічого» і скасування резерву");
        var big = new Order("З-002", "Кафе «Кава і пиріг»");
        big.AddLine(milk, 10);
        big.AddLine(dumplings, 9);
        big.AddLine(dumplings, 5);
        Try("Reserve()", big.Reserve);
        Console.WriteLine($"Стан: {big.Status}; {Short(milk)}; {Short(dumplings)}");
        var small = new Order("З-003", "Кафе «Кава і пиріг»");
        small.AddLine(milk, 10);
        small.Reserve();
        Console.WriteLine($"З-003 Reserve(): {Short(milk)}");
        small.Cancel();
        Console.WriteLine($"З-003 Cancel():  {small.Status}; {Short(milk)}");
        Try("AddLine з кількістю 0", () => new Order("З-004", "Клієнт").AddLine(milk, 0));
        Try("Reserve() порожнього", new Order("З-005", "Клієнт").Reserve);
    }

    private static string Short(Product p) => $"{p.Sku} залишок {p.Stock}, резерв {p.Reserved}";

    private static void Title(string text)
    {
        Console.WriteLine();
        Console.WriteLine($"=== {text} ===");
    }

    private static void Try(string action, Action body)
    {
        try
        {
            body();
            Console.WriteLine($"  {action}: виконано");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Console.WriteLine($"  {action}: {ex.GetType().Name} — {ex.Message.Split(" (Parameter")[0]}");
        }
    }
}
