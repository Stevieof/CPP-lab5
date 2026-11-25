using Core.Abstractions;
using Core.Domain;
using Core.Dto;
using Core.Services;
using Core.Storage;

/// <summary>
/// Сценарій лабораторної 5: однакові операції сервісу над будь-яким сховищем.
/// Конкретні класи сховищ тут не створюються — їх передає composition root.
/// </summary>
internal static class ServiceDemo
{
    private const string Sku = "SKU-901";
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    public static int Run(IProductStore store, InventoryService service)
    {
        Console.WriteLine($"Сховище: {store.GetType().Name}");
        if (store is JsonFileProductStore file)
            Console.WriteLine($"Файл: {Path.GetRelativePath(Environment.CurrentDirectory, file.FilePath)}");

        Title("Операції сервісу");
        var yogurt = new ProductDto(Sku, "Йогурт питний 1,5% 400 г", "молочне", "шт", 34.50m, 20);
        if (service.Find(Sku) is null)
            Console.WriteLine($"Register:        {service.Register(yogurt)}");
        else
            Console.WriteLine($"Register:        {Sku} уже є у сховищі — картку не створюємо");

        string batch = $"П-{DateTime.Now:HHmmss}";
        Console.WriteLine($"Receive(40):     {service.Receive(Sku, batch, 40, Today.AddDays(10))}");
        Console.WriteLine($"Reserve(15):     {service.Reserve(Sku, 15)}");
        Console.WriteLine($"Ship(15):        {service.Ship(Sku, 15)}");

        IReadOnlyList<Product> all = service.All();
        Title($"Список товарів ({all.Count})");
        foreach (Product p in all)
            Console.WriteLine($"  {p.Sku,-8} {p.Name,-26} залишок {p.Stock,4}  резерв {p.Reserved,3}  " +
                              $"партій {p.Batches.Count}");

        Product? found = service.Find(Sku.ToLowerInvariant());
        Console.WriteLine($"Find(\"{Sku.ToLowerInvariant()}\"): {found}");

        Title("Сценарій відмови");
        Try("Receive для SKU-999", () => service.Receive("SKU-999", "П-1", 5, null));
        Try("Reserve(1000)", () => service.Reserve(Sku, 1000));
        Try($"Register {Sku} повторно", () => service.Register(yogurt));
        Try("Register з ціною 0", () => service.Register(yogurt with { Sku = "SKU-902", Price = 0 }));
        Try($"Remove {Sku} із залишком", () => service.Remove(Sku));
        Console.WriteLine($"Після відмов:    {service.Find(Sku)}");
        return 0;
    }

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
