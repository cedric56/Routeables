// Routeables.Generated/ProtoStateAttribute.cs
using System;

namespace Routeables.Generated.Abstraction;

/// <summary>
/// Marque un ViewModel comme source d'un contrat .proto.
/// Le source generator Routeables.Generated génère le .proto correspondant
/// (message XxxState + service de streaming/commandes).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ProtoStateAttribute : Attribute
{
    /// <summary>Nom du fichier .proto produit (ex. "counter.proto").</summary>
    public string FileName { get; }

    /// <summary>package proto (défaut : "routeables").</summary>
    public string Package { get; set; } = "routeables";

    /// <summary>Nom du service gRPC généré (défaut : nom de la classe + "ViewModel").</summary>
    public string? Service { get; set; }

    /// <summary>Nom du message d'état (défaut : nom de la classe + "State").</summary>
    public string? StateMessage { get; set; }

    public ProtoStateAttribute(string fileName) => FileName = fileName;
}

/// <summary>
/// Exposes a ViewModel method as a remote Protobuf command.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class ProtoCommandAttribute : Attribute
{
    /// <summary>
    /// Optional command name. Defaults to the method name.
    /// </summary>
    public string? Name { get; set; }
}