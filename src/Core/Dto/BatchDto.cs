namespace Core.Dto;

/// <summary>Партія товару, що надійшла на склад.</summary>
public record BatchDto(
    string BatchNo,
    string Sku,
    int Quantity,
    DateOnly? ExpiryDate = null);
