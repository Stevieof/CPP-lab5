namespace Core.Dto;

/// <summary>Результат імпорту: прочитані записи та помилки з номерами рядків.</summary>
public sealed record ImportResult<T>(IReadOnlyList<T> Items, IReadOnlyList<string> Errors);

/// <summary>Результат імпорту накладної, де в одному файлі є і товари, і партії.</summary>
public sealed record CatalogImportResult(
    IReadOnlyList<ProductDto> Products,
    IReadOnlyList<BatchDto> Batches,
    IReadOnlyList<string> Errors);
