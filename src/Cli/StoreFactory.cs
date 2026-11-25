using Core.Abstractions;
using Core.Storage;

/// <summary>
/// Вибір сховища за аргументами (додаткове завдання 3). Частина composition root, тому лежить
/// у Cli: лише тут дозволено знати конкретні класи сховищ. Core про фабрику нічого не знає.
/// </summary>
internal static class StoreFactory
{
    public static readonly string DataPath = Path.Combine("data", "store", "inventory.json");

    public static IProductStore Create(IReadOnlyCollection<string> options)
    {
        IProductStore store = options.Contains("--file")
            ? new JsonFileProductStore(DataPath)
            : new InMemoryProductStore(SampleData.Products());

        return options.Contains("--cache") ? new CachingProductStore(store) : store;
    }
}
