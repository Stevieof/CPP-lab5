namespace Core.Domain;

/// <summary>Склад, що приймає лише певні категорії товарів (наприклад, холодильний — молочне й заморожене).</summary>
public sealed class Warehouse
{
    private readonly HashSet<string> _categories;

    public string Id { get; }
    public string Name { get; }
    public IReadOnlyCollection<string> Categories => _categories;

    private Warehouse(string id, string name, HashSet<string> categories)
    {
        Id = id;
        Name = name;
        _categories = categories;
    }

    public static Warehouse Create(string id, string name, params string[] categories)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Ідентифікатор складу обов'язковий", nameof(id));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Назва складу не може бути порожньою", nameof(name));

        HashSet<string> set = categories.Where(c => !string.IsNullOrWhiteSpace(c))
                                        .Select(c => c.Trim().ToLowerInvariant())
                                        .ToHashSet();
        if (set.Count == 0)
            throw new ArgumentException("Склад має приймати хоча б одну категорію товарів", nameof(categories));

        return new Warehouse(id.Trim(), name.Trim(), set);
    }

    public bool Accepts(string category) => _categories.Contains(category.ToLowerInvariant());

    public override string ToString() => $"{Id} {Name} [{string.Join(", ", _categories)}]";
}
