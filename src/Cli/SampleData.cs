using Core.Domain;
using Core.Dto;
using Core.Import;

/// <summary>
/// Стартові дані для сховища в пам'яті: картки з data/clean.csv (імпорт тижнів 3–4)
/// і по одній партії на кожен товар, щоб список не був порожнім.
/// </summary>
internal static class SampleData
{
    public static IReadOnlyList<Product> Products(string path = "data/clean.csv")
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        ImportResult<Product> imported = ProductDomainImporter.Convert(ProductCsvImporter.Load(path));

        int n = 0;
        foreach (Product product in imported.Items)
        {
            n++;
            int quantity = 10 + n * 7 % 50;
            DateOnly? expiry = product.Category is "молочне" or "заморожене" ? today.AddDays(5 + n) : null;
            product.Receive($"П-START-{n:D2}", quantity, expiry, today);
        }
        return imported.Items;
    }
}
