using Core;
using Core.Abstractions;
using Core.Dto;
using Core.Import;
using Core.Services;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

Console.OutputEncoding = Encoding.UTF8;

switch (args)
{
    case ["--env", ..] or ["--json"]:
        PrintEnvironment(args.Contains("--json", StringComparer.OrdinalIgnoreCase));
        return 0;
    case ["--domain"]:
        DomainDemo.Run();
        return 0;
    case ["--domain-import", .. var rest]:
        DomainDemo.Import(rest.FirstOrDefault() ?? Path.Combine("data", "domain.json"));
        return 0;
    case ["--warehouse"]:
        DomainDemo.Warehouses();
        return 0;
    case ["--order"]:
        DomainDemo.Orders();
        return 0;
    case ["--service", .. var options]:
    {
        // Composition root: конкретне сховище обирає StoreFactory, сервіс отримує лише інтерфейс.
        IProductStore store = StoreFactory.Create(options);
        var service = new InventoryService(store);
        return ServiceDemo.Run(store, service);
    }
}

bool invoice = args is ["--invoice", ..];
string path = (invoice ? args.Skip(1) : args).FirstOrDefault()
              ?? Path.Combine("data", invoice ? "invoice.csv" : "products.csv");

if (!File.Exists(path))
{
    Console.WriteLine($"Файл не знайдено: {Path.GetFullPath(path)}");
    return 1;
}

Console.WriteLine($"Імпорт: {path}");
Console.WriteLine(new string('-', 72));

if (invoice)
{
    CatalogImportResult result = ProductCsvImporter.LoadInvoice(path);
    PrintProducts(result.Products);
    Console.WriteLine($"Партій: {result.Batches.Count}");
    foreach (BatchDto batch in result.Batches)
        Console.WriteLine($"  {batch.BatchNo,-10} {batch.Sku,-8} {batch.Quantity,5} " +
                          $"{(batch.ExpiryDate is DateOnly date ? $"придатне до {date:dd.MM.yyyy}" : "без терміну придатності")}");
    PrintErrors(result.Errors);
    PrintStats(result.Products.Count + result.Batches.Count, result.Errors.Count);
}
else
{
    ImportResult<ProductDto> result = Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".json" => ProductJsonImporter.Load(path),
        _ => ProductCsvImporter.Load(path)
    };
    PrintProducts(result.Items);
    PrintErrors(result.Errors);
    PrintStats(result.Items.Count, result.Errors.Count);
}
return 0;

static void PrintProducts(IReadOnlyList<ProductDto> products)
{
    Console.WriteLine($"Завантажено товарів: {products.Count}. Перші 5:");
    foreach (ProductDto p in products.Take(5))
        Console.WriteLine($"  {p.Sku,-8} {p.Name,-26} {p.Category,-11} {p.Price,8:F2} грн/{p.Unit,-3} " +
                          $"{(p.MinStock is int min ? $"мін. {min}" : "мін. не задано")}");
}

static void PrintErrors(IReadOnlyList<string> errors)
{
    if (errors.Count == 0)
        return;
    Console.WriteLine($"Пропущено рядків: {errors.Count}");
    foreach (string error in errors)
        Console.WriteLine($"  {error}");
}

static void PrintStats(int accepted, int skipped)
{
    int total = accepted + skipped;
    double errorPercent = total == 0 ? 0 : 100.0 * skipped / total;
    Console.WriteLine(new string('-', 72));
    Console.WriteLine($"Усього: {total} / прийнято: {accepted} / пропущено: {skipped} / помилок: {errorPercent:F1}%");
}

static void PrintEnvironment(bool json)
{
    var appInfo = EnvironmentInfo.Collect();
    if (json)
    {
        var options = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        Console.WriteLine(JsonSerializer.Serialize(appInfo, options));
        return;
    }

    Console.WriteLine("CrossApp — практикум з крос-платформного програмування");
    Console.WriteLine($"Студент: {appInfo.Student}");
    Console.WriteLine(new string('-', 72));
    Console.WriteLine($"{"Параметр",-24} | Значення");
    Console.WriteLine(new string('-', 72));
    Console.WriteLine($"{"ОС (OSDescription)",-24} | {appInfo.OSDescription}");
    Console.WriteLine($"{"Архітектура процесу",-24} | {appInfo.ProcessArchitecture}");
    Console.WriteLine($"{"Runtime",-24} | {appInfo.Framework}");
    Console.WriteLine($"{"Предметна область",-24} | {appInfo.Domain}");
    Console.WriteLine($"{"Визначений RID",-24} | {appInfo.DetectedRid}");
    Console.WriteLine($"{"RID середовища",-24} | {appInfo.ReportedRid}");
}
