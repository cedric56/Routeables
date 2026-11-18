namespace HomonymSales { public sealed class Customer { public string? Name { get; set; } } public sealed class Patron { public string? Name { get; set; } } }
namespace HomonymSupport { public sealed class Customer { public string? Name { get; set; } } }
// A type with no namespace: its simple name is its full name.
public sealed class Patron { public string? Name { get; set; } }
// Types called like a built-in constraint.
namespace HomonymShadow { public sealed class Guid { public string? Name { get; set; } } public sealed class String { public string? Name { get; set; } } }
