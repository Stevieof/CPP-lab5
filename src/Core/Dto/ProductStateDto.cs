namespace Core.Dto;

/// <summary>Партія у сховищі: номер, скільки ще лишилось, термін придатності.</summary>
public sealed record BatchStateDto(string BatchNo, int Remaining, DateOnly? ExpiryDate);

/// <summary>
/// Повний стан товару для збереження у файлі: картка товару, резерв і партії.
/// Сутність Product не серіалізується напряму — у неї приватний конструктор і закриті поля.
/// </summary>
public sealed record ProductStateDto(
    string Sku,
    string Name,
    string Category,
    string Unit,
    decimal Price,
    int? MinStock,
    int Reserved,
    IReadOnlyList<BatchStateDto> Batches);
