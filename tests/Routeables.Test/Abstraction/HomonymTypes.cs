namespace HomonymSales { public sealed class Customer { public string? Name { get; set; } } public sealed class Patron { public string? Name { get; set; } } }
namespace HomonymSupport { public sealed class Customer { public string? Name { get; set; } } }
// A type with no namespace: its simple name is its full name.
public sealed class Patron { public string? Name { get; set; } }
