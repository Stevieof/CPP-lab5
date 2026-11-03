using System.Globalization;
using Core.Dto;

namespace Core.Import;

/// <summary>Результат розбору одного рядка: готовий запис або причина помилки.</summary>
internal abstract record ParseOutcome;
internal sealed record ProductParsed(ProductDto Value) : ParseOutcome;
internal sealed record BatchParsed(BatchDto Value) : ParseOutcome;
internal sealed record ParseFailed(string Reason) : ParseOutcome;

public static class ProductCsvImporter
{
    private const char Separator = ';';

    public static ImportResult<ProductDto> Load(string path)
    {
        List<ProductDto> items = [];
        List<string> errors = [];

        string[] lines = File.ReadAllLines(path);
        for (int i = 0; i < lines.Length; i++)
        {
            int number = i + 1;
            if (IsSkipped(lines[i], number))
                continue;

            switch (ParseProduct(lines[i].Split(Separator, StringSplitOptions.TrimEntries)))
            {
                case ProductParsed ok:
                    items.Add(ok.Value);
                    break;
                case ParseFailed failed:
                    errors.Add($"рядок {number}: {failed.Reason}");
                    break;
            }
        }
        return new ImportResult<ProductDto>(items, errors);
    }

    /// <summary>
    /// Прибуткова накладна: рядки product;... — товари, batch;... — партії цих товарів.
    /// </summary>
    public static CatalogImportResult LoadInvoice(string path)
    {
        List<ProductDto> products = [];
        List<BatchDto> batches = [];
        List<string> errors = [];

        string[] lines = File.ReadAllLines(path);
        for (int i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]) || lines[i].TrimStart().StartsWith('#'))
                continue;

            switch (ParseInvoiceLine(lines[i].Split(Separator, StringSplitOptions.TrimEntries)))
            {
                case ProductParsed product:
                    products.Add(product.Value);
                    break;
                case BatchParsed batch:
                    batches.Add(batch.Value);
                    break;
                case ParseFailed failed:
                    errors.Add($"рядок {i + 1}: {failed.Reason}");
                    break;
            }
        }
        return new CatalogImportResult(products, batches, errors);
    }

    private static ParseOutcome ParseInvoiceLine(string[] parts) => parts switch
    {
        ["product", .. var rest] => ParseProduct(rest),
        ["batch", _, _, var qty, _] when !int.TryParse(qty, NumberStyles.None, CultureInfo.InvariantCulture, out int q) || q == 0
            => new ParseFailed($"партія: кількість '{qty}' має бути додатним числом"),
        ["batch", var no, var sku, var qty, ""]
            => new BatchParsed(new BatchDto(no, sku, int.Parse(qty, CultureInfo.InvariantCulture))),
        ["batch", var no, var sku, var qty, var date]
            when DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly expiry)
            => new BatchParsed(new BatchDto(no, sku, int.Parse(qty, CultureInfo.InvariantCulture), expiry)),
        ["batch", _, _, _, var date] => new ParseFailed($"партія: дата '{date}' не у форматі РРРР-ММ-ДД"),
        ["batch", ..] => new ParseFailed($"партія: очікую 4 колонки, отримав {parts.Length - 1}"),
        [var type, ..] => new ParseFailed($"невідомий тип запису '{type}'"),
        _ => new ParseFailed("порожній рядок")
    };

    /// <summary>Порожні рядки, коментарі (#) і заголовок не вважаються помилками.</summary>
    private static bool IsSkipped(string line, int number) =>
        string.IsNullOrWhiteSpace(line)
        || line.TrimStart().StartsWith('#')
        || (number == 1 && line.StartsWith("sku;", StringComparison.OrdinalIgnoreCase));

    private static ParseOutcome ParseProduct(string[] parts) => parts switch
    {
        { Length: < 5 or > 6 } => new ParseFailed($"очікую 5 або 6 колонок, отримав {parts.Length}"),
        [var sku, ..] when !sku.StartsWith("SKU-", StringComparison.Ordinal)
            => new ParseFailed($"артикул '{sku}' має починатися з SKU-"),
        [_, "", ..] => new ParseFailed("назва товару порожня"),
        [_, _, not ("молочне" or "заморожене" or "бакалія" or "напої" or "овочі") and var category, ..]
            => new ParseFailed($"невідома категорія '{category}'"),
        [_, _, _, _, var price, ..] when !TryPrice(price, out _)
            => new ParseFailed($"ціна '{price}' не є додатним числом"),
        [_, _, _, _, _, var min] when !int.TryParse(min, CultureInfo.InvariantCulture, out int m) || m is < 0
            => new ParseFailed($"мінімальний залишок '{min}' не є невід'ємним числом"),
        [var sku, var name, var category, var unit, var price]
            => new ProductParsed(new ProductDto(sku, name, category, unit, Price(price))),
        [var sku, var name, var category, var unit, var price, var min]
            => new ProductParsed(new ProductDto(sku, name, category, unit, Price(price),
                                                int.Parse(min, CultureInfo.InvariantCulture))),
        _ => new ParseFailed("нерозпізнаний рядок")
    };

    private static bool TryPrice(string text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value)
        && value > 0;

    private static decimal Price(string text) =>
        decimal.Parse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
}
