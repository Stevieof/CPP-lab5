using Core.Domain;

namespace Core.Abstractions;

/// <summary>
/// Контракт сховища товарів. Містить лише операції, потрібні сервісу, і нічого не каже
/// про те, де лежать дані: у пам'яті, у файлі чи (згодом) у базі даних.
/// </summary>
public interface IProductStore
{
    /// <summary>Усі товари. Список лише для читання: змінювати набір можна тільки через Add/Remove.</summary>
    IReadOnlyList<Product> GetAll();

    /// <summary>Товар за артикулом або null, якщо такого немає (це звичайний результат пошуку, не збій).</summary>
    Product? GetBySku(string sku);

    /// <summary>Додає новий товар. Сховище перевіряє лише унікальність артикула.</summary>
    void Add(Product product);

    /// <summary>Зберігає змінений стан товару (після надходження, резерву, відвантаження).</summary>
    void Update(Product product);

    /// <summary>Видаляє товар; false — якщо видаляти було нічого.</summary>
    bool Remove(string sku);
}
