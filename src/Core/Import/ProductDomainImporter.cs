using Core.Domain;
using Core.Dto;

namespace Core.Import;

/// <summary>Перетворює DTO на сутності; записи, що порушують інваріанти, потрапляють у помилки.</summary>
public static class ProductDomainImporter
{
    public static ImportResult<Product> Convert(ImportResult<ProductDto> dto)
    {
        List<Product> products = [];
        List<string> errors = [.. dto.Errors];

        foreach (ProductDto item in dto.Items)
        {
            try
            {
                products.Add(Product.FromDto(item));
            }
            catch (ArgumentException err)
            {
                // Без службового хвоста «(Parameter 'x') Actual value was ...»
                errors.Add($"запис '{item.Sku}': {err.Message.Split(" (Parameter")[0]}");
            }
        }
        return new ImportResult<Product>(products, errors);
    }
}
