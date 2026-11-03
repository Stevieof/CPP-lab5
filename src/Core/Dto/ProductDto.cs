namespace Core.Dto;

/// <summary>Рядок файлу з товаром: артикул, назва, категорія, одиниця, ціна, мінімальний залишок.</summary>
public record ProductDto(
    string Sku,
    string Name,
    string Category,
    string Unit,
    decimal Price,
    int? MinStock = null);
