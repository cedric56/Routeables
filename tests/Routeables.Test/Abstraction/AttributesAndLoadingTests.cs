using Routeables.Abstraction;
using Routeables.Abstraction.Initializers;
using Routeables.Abstraction.Lifecycle;
using Routeables.Routing;
using System.Reflection;

namespace Routeables.Test.Abstraction;

/// <summary>The defaults of the routing attributes, the typed initializer, the cache of singletons and the load context of modules.</summary>
public class AttributesAndLoadingTests
{
    // ------------------------------------------------------------------------------------------------ attributes

    [Fact]
    public void A_routeable_is_transient_and_has_no_region_unless_told()
    {
        var attribute = new RouteableAttribute("/cats");

        Assert.Equal("/cats", attribute.Path);
        Assert.Null(attribute.Region);
        Assert.Equal(ServiceLifetime.Transient, attribute.Lifetime);
    }

    [Fact]
    public void A_routeable_keeps_the_region_and_lifetime_it_is_given()
    {
        var attribute = new RouteableAttribute("/cats", "side", ServiceLifetime.Singleton);

        Assert.Equal("side", attribute.Region);
        Assert.Equal(ServiceLifetime.Singleton, attribute.Lifetime);
    }

    [Fact]
    public void A_routeable_view_lifetime_applies_to_the_view_and_the_view_model_unless_each_is_set()
    {
        var shared = new RouteableViewAttribute(typeof(object), "/x", "side", ServiceLifetime.Scoped);
        var split = new RouteableViewAttribute(typeof(object)) { ViewModelLifetime = ServiceLifetime.Singleton };

        Assert.Equal("/x", shared.Route);
        Assert.Equal("side", shared.Region);
        Assert.Equal(ServiceLifetime.Scoped, shared.ViewLifetime);
        Assert.Equal(ServiceLifetime.Scoped, shared.ViewModelLifetime);
        Assert.Equal(ServiceLifetime.Singleton, split.ViewModelLifetime);
        Assert.Equal(ServiceLifetime.Transient, split.ViewLifetime);
    }

    [Fact]
    public void A_view_lifetime_set_alone_leaves_the_view_model_on_the_general_one()
    {
        var attribute = new RouteableViewAttribute(typeof(object), serviceLifetime: ServiceLifetime.Scoped) { ViewLifetime = ServiceLifetime.Singleton };

        Assert.Equal(ServiceLifetime.Singleton, attribute.ViewLifetime);
        Assert.Equal(ServiceLifetime.Scoped, attribute.ViewModelLifetime);
        Assert.Equal(ServiceLifetime.Scoped, attribute.Lifetime);
    }

    [Fact]
    public void A_routeable_view_model_starts_without_route_template_or_method_name()
    {
        var attribute = new RouteableViewModelAttribute(typeof(string), typeof(int)) { Route = "/r", Template = "/t/{id}", MethodName = "Open" };
        var bare = new RouteableViewModelAttribute();

        Assert.Equal(["/r", "/t/{id}", "Open"], [attribute.Route, attribute.Template, attribute.MethodName]);
        Assert.Null(bare.Route);
        Assert.Null(bare.Template);
        Assert.Null(bare.MethodName);
    }

    [Theory]
    [InlineData(typeof(RouteableAttribute), AttributeTargets.Class, true)]
    [InlineData(typeof(RouteableViewAttribute), AttributeTargets.Class, true)]
    [InlineData(typeof(RouteableViewModelAttribute), AttributeTargets.Class, true)]
    [InlineData(typeof(FromContainerAttribute), AttributeTargets.Parameter, false)]
    [InlineData(typeof(InjectPropertyAttribute), AttributeTargets.Parameter, false)]
    public void The_attributes_are_declared_on_what_they_mark(Type attribute, AttributeTargets targets, bool allowMultiple)
    {
        var usage = attribute.GetCustomAttribute<AttributeUsageAttribute>()!;

        Assert.Equal(targets, usage.ValidOn);
        Assert.Equal(allowMultiple, usage.AllowMultiple);
    }

    // ------------------------------------------------------------------------------------------------ typed initializer

    private sealed class Receiver : IInitializeAsync<int>
    {
        public int? Received { get; private set; }
        public CancellationToken Token { get; private set; }

        public Task InitializeAsync(int data, CancellationToken ct)
        {
            Received = data;
            Token = ct;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void A_typed_initializer_accepts_only_a_target_that_takes_its_data_type()
    {
        var initializer = new RouteableTypedInitializer<int>(7);

        Assert.True(initializer.Accepts(new Receiver()));
        Assert.False(initializer.Accepts(new object()));
        Assert.False(new RouteableTypedInitializer<string>("x").Accepts(new Receiver()));
    }

    [Fact]
    public async Task A_typed_initializer_hands_its_data_and_the_token_to_the_target()
    {
        var target = new Receiver();
        using var source = new CancellationTokenSource();

        await new RouteableTypedInitializer<int>(7).InitializeAsync(target, source.Token);

        Assert.Equal(7, target.Received);
        Assert.Equal(source.Token, target.Token);
    }

    [Fact]
    public async Task A_typed_initializer_refuses_a_target_that_cannot_take_its_data()
    {
        await Assert.ThrowsAsync<InvalidCastException>(() => new RouteableTypedInitializer<int>(1).InitializeAsync(new object(), CancellationToken.None));
    }

    // ------------------------------------------------------------------------------------------------ singleton cache

    [Fact]
    public void The_singleton_cache_is_a_cache_of_its_own_that_disposes_what_it_created()
    {
        var singletons = new SingletonViewCache();
        var scoped = new ScopedViewCache();
        var disposable = new DisposableItem();

        var first = singletons.GetOrCreate("k", () => disposable);
        var other = scoped.GetOrCreate("k", () => new object());

        Assert.IsAssignableFrom<ScopedViewCache>(singletons);
        Assert.Same(disposable, first);
        Assert.NotSame(first, other);
        Assert.True(singletons.Owns(disposable));
        Assert.False(scoped.Owns(disposable));

        singletons.Dispose();

        Assert.Equal(1, disposable.Disposed);
        Assert.Throws<ObjectDisposedException>(() => singletons.GetOrCreate("again", () => new object()));
    }

    private sealed class DisposableItem : IDisposable
    {
        public int Disposed { get; private set; }
        public void Dispose() => Disposed++;
    }

    // ------------------------------------------------------------------------------------------------ module load context

    [Fact]
    public void The_load_context_is_named_after_the_module_file()
    {
        // The module must exist: the dependency resolver refuses a path that is not an assembly.
        var path = typeof(AttributesAndLoadingTests).Assembly.Location;

        var context = new RouteableModuleLoadContext(path);

        Assert.Equal(Path.GetFileNameWithoutExtension(path), context.Name);
    }

    [Fact]
    public void An_assembly_the_application_has_comes_from_the_default_context_so_the_types_are_the_same()
    {
        var context = new RouteableModuleLoadContext(typeof(AttributesAndLoadingTests).Assembly.Location);

        var loaded = context.LoadFromAssemblyName(typeof(RouteableAttribute).Assembly.GetName());

        Assert.Same(typeof(RouteableAttribute).Assembly, loaded);
    }

    [Fact]
    public void An_assembly_found_nowhere_is_not_loaded()
    {
        var context = new RouteableModuleLoadContext(typeof(AttributesAndLoadingTests).Assembly.Location);

        Assert.Throws<FileNotFoundException>(() => context.LoadFromAssemblyName(new AssemblyName("Routeables.Missing.Module")));
    }
}
