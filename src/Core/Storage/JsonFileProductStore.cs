using System.Text.Encodings.Web;
using System.Text.Json;
using Core.Abstractions;
using Core.Domain;
using Core.Dto;

namespace Core.Storage;

/// <summary>
/// Сховище у JSON-файлі. Файл — єдине джерело правди: кожне читання завантажує його заново,
/// кожна зміна одразу перезаписує файл. Тому GetBySku повертає нову копію товару, і сервіс
/// мусить викликати Update, щоб зміна потрапила на диск.
/// </summary>
public sealed class JsonFileProductStore : IProductStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping   // кирилиця у файлі читається як є
    };

    public JsonFileProductStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        FilePath = Path.GetFullPath(path);
    }

    public string FilePath { get; }

    /// <summary>Скільки разів файл читався з диска (для порівняння з кешем).</summary>
    public int FileReads { get; private set; }

    public IReadOnlyList<Product> GetAll() => [.. Load().Values];

    public Product? GetBySku(string sku) => Load().GetValueOrDefault(sku.Trim());

    public void Add(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);
        Dictionary<string, Product> items = Load();
        if (!items.TryAdd(product.Sku, product))
            throw new InvalidOperationException($"Товар {product.Sku} уже є у сховищі");
        Save(items);
    }

    public void Update(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);
        Dictionary<string, Product> items = Load();
        if (!items.ContainsKey(product.Sku))
            throw new InvalidOperationException($"Товару {product.Sku} немає у сховищі");
        items[product.Sku] = product;
        Save(items);
    }

    public bool Remove(string sku)
    {
        Dictionary<string, Product> items = Load();
        if (!items.Remove(sku.Trim()))
            return false;
        Save(items);
        return true;
    }

    private Dictionary<string, Product> Load()
    {
        var items = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(FilePath))
            return items;

        FileReads++;
        List<ProductStateDto> states =
            JsonSerializer.Deserialize<List<ProductStateDto>>(File.ReadAllText(FilePath), Options) ?? [];
        foreach (ProductStateDto state in states)
        {
            Product product = Product.FromState(state);   // дані з файлу теж проходять інваріанти
            items[product.Sku] = product;
        }
        return items;
    }

    /// <summary>Запис через тимчасовий файл: якщо процес упаде посередині, старий файл лишиться цілим.</summary>
    private void Save(Dictionary<string, Product> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(items.Values.Select(p => p.ToState()), Options));
        File.Move(temp, FilePath, overwrite: true);
    }
}
