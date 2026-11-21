using Microsoft.Extensions.DependencyInjection;
using Routeables.Abstraction.Registrations;
using System.Reflection;

namespace Routeables.Tests.Abstraction;

/// <summary>How the generated routes are marked as present, and what a marker of one assembly says of another's.</summary>
public class AutoRegistrationEdgeTests
{
    private static readonly Assembly Library = typeof(object).Assembly;
    private static readonly Assembly Application = typeof(AutoRegistrationEdgeTests).Assembly;

    private static int Markers(IServiceCollection services) => services.Count(d => d.ServiceType == typeof(GeneratedRoutesMarker));

    private static (Assembly? Owner, Action<IServiceCollection> Run)[] Registrations(Assembly? owner, Action<IServiceCollection> run) => [(owner, run)];

    [Fact]
    public void Mark_adds_one_marker_and_a_second_call_adds_none()
    {
        var services = new ServiceCollection();

        RouteableAutoRegistration.Mark(services);
        RouteableAutoRegistration.Mark(services);

        Assert.Equal(1, Markers(services));
    }

    [Fact]
    public void Mark_refuses_a_missing_collection()
    {
        Assert.Throws<ArgumentNullException>(() => RouteableAutoRegistration.Mark(null!));
        Assert.Throws<ArgumentNullException>(() => RouteableAutoRegistration.Mark(null!, Application));
    }

    [Fact]
    public void Apply_refuses_a_missing_collection()
    {
        Assert.Throws<ArgumentNullException>(() => RouteableAutoRegistration.Apply(null!, Registrations(null, _ => { })));
    }

    [Fact]
    public void A_marker_without_an_owner_says_every_assembly_is_there()
    {
        var services = new ServiceCollection();
        RouteableAutoRegistration.Mark(services);

        Assert.False(RouteableAutoRegistration.Apply(services, Registrations(Application, _ => throw new InvalidOperationException("must not run"))));
    }

    [Fact]
    public void A_marker_of_one_assembly_does_not_hide_the_routes_of_another()
    {
        var services = new ServiceCollection();
        RouteableAutoRegistration.Mark(services, Library);
        var ran = false;

        var applied = RouteableAutoRegistration.Apply(services, Registrations(Application, _ => ran = true));

        Assert.True(applied);
        Assert.True(ran);
    }

    [Fact]
    public void A_marker_of_the_same_assembly_hides_its_routes()
    {
        var services = new ServiceCollection();
        RouteableAutoRegistration.Mark(services, Application);

        Assert.False(RouteableAutoRegistration.Apply(services, Registrations(Application, _ => throw new InvalidOperationException("must not run"))));
    }

    [Fact]
    public void A_marker_written_by_a_generated_registration_is_carried_to_the_services()
    {
        var services = new ServiceCollection();

        Assert.True(RouteableAutoRegistration.Apply(services, Registrations(Application, generated => RouteableAutoRegistration.Mark(generated, Application))));

        Assert.Equal(1, Markers(services));
        Assert.False(RouteableAutoRegistration.Apply(services, Registrations(Application, _ => throw new InvalidOperationException("must not run"))));
    }

    [Fact]
    public void A_service_the_application_registered_is_kept_and_a_new_one_is_added()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFormatProvider>(System.Globalization.CultureInfo.InvariantCulture);
        var mine = services.Single().ImplementationInstance;

        RouteableAutoRegistration.Apply(services, Registrations(null, generated =>
        {
            generated.AddSingleton<IFormatProvider>(System.Globalization.CultureInfo.CurrentCulture);
            generated.AddSingleton<IDisposable>(new MemoryStream());
        }));

        Assert.Same(mine, services.Single(d => d.ServiceType == typeof(IFormatProvider)).ImplementationInstance);
        Assert.Single(services, d => d.ServiceType == typeof(IDisposable));
    }

    [Fact]
    public void A_keyed_service_is_the_same_when_its_type_and_key_are_the_same()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IDisposable>("a", new MemoryStream());

        RouteableAutoRegistration.Apply(services, Registrations(null, generated =>
        {
            generated.AddKeyedSingleton<IDisposable>("a", new MemoryStream());
            generated.AddKeyedSingleton<IDisposable>("b", new MemoryStream());
        }));

        Assert.Equal(new object?[] { "a", "b" }, services.Where(d => d.IsKeyedService).Select(d => d.ServiceKey).ToArray());
    }
}
