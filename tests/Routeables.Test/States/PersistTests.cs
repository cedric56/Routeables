using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Routeables.Abstraction;
using Routeables.Abstraction.Bases;
using Routeables.Abstraction.Routing;
using Routeables.Abstraction.States;
using Routeables.Routing;
using Routeables.Services;

namespace Routeables.Tests.States;

/// <summary>What <c>[Persist]</c> relies on at run time: the codec, the limits and the way the snapshots carry the values.</summary>
public class PersistTests
{
    public enum Colour { Red, Green }

    [Flags]
    public enum Access { None = 0, Read = 1, Write = 2 }

    public sealed record Filter(string Text, int Page);

    public sealed class FormView : IRouteableFor { public object? Context { get; set; } }

    public class BaseVm
    {
        public string Title { get; set; } = "";
    }

    public sealed class FormVm : BaseVm
    {
        public int Count { get; set; }
        public double Ratio { get; set; }
        public bool On { get; set; }
        public Colour Colour { get; set; }
        public Guid Id { get; set; }
        public DateTime When { get; set; }
        public TimeSpan Span { get; set; }
        public int? Maybe { get; set; }
        public string? Note { get; set; }
        public Filter? Filter { get; set; }
    }

    private static void RegisterForm()
    {
        RouteablePersistence.Register<BaseVm>(
            static (m, bag) => bag.Capture<BaseVm, string>(m, "Title", static x => x.Title),
            static (m, bag) => bag.Restore<BaseVm, string>(m, "Title", static (x, v) => x.Title = v));
        RouteablePersistence.Register<FormVm>(
            static (m, bag) =>
            {
                bag.Capture<FormVm, int>(m, "Count", static x => x.Count);
                bag.Capture<FormVm, double>(m, "Ratio", static x => x.Ratio);
                bag.Capture<FormVm, bool>(m, "On", static x => x.On);
                bag.Capture<FormVm, Colour>(m, "Colour", static x => x.Colour);
                bag.Capture<FormVm, Guid>(m, "Id", static x => x.Id);
                bag.Capture<FormVm, DateTime>(m, "When", static x => x.When);
                bag.Capture<FormVm, TimeSpan>(m, "Span", static x => x.Span);
                bag.Capture<FormVm, int?>(m, "Maybe", static x => x.Maybe);
                bag.Capture<FormVm, string?>(m, "Note", static x => x.Note);
                bag.Capture<FormVm, Filter?>(m, "Filter", static x => x.Filter);
            },
            static (m, bag) =>
            {
                bag.Restore<FormVm, int>(m, "Count", static (x, v) => x.Count = v);
                bag.Restore<FormVm, double>(m, "Ratio", static (x, v) => x.Ratio = v);
                bag.Restore<FormVm, bool>(m, "On", static (x, v) => x.On = v);
                bag.Restore<FormVm, Colour>(m, "Colour", static (x, v) => x.Colour = v);
                bag.Restore<FormVm, Guid>(m, "Id", static (x, v) => x.Id = v);
                bag.Restore<FormVm, DateTime>(m, "When", static (x, v) => x.When = v);
                bag.Restore<FormVm, TimeSpan>(m, "Span", static (x, v) => x.Span = v);
                bag.Restore<FormVm, int?>(m, "Maybe", static (x, v) => x.Maybe = v);
                bag.Restore<FormVm, string?>(m, "Note", static (x, v) => x.Note = v);
                bag.Restore<FormVm, Filter?>(m, "Filter", static (x, v) => x.Filter = v);
            });
        RouteablePersistence.RegisterJson((JsonTypeInfo<Filter>)new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() }.GetTypeInfo(typeof(Filter)));
    }

    private static ServiceProvider Build()
    {
        RegisterForm();
        var services = new ServiceCollection();
        services.AddRoutes(n => n.Page<FormView, FormVm>("/form"));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task The_properties_come_back_after_the_snapshots_went_through_text()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        var ct = TestContext.Current.CancellationToken;
        await router.GoToAsync("/form", cancellationToken: ct);
        var vm = Assert.IsType<FormVm>(router.CurrentViewModel);
        var id = Guid.NewGuid();
        var when = new DateTime(2026, 11, 7, 10, 30, 0, DateTimeKind.Utc);
        vm.Title = "a|b=c\nd"; vm.Count = 3; vm.Ratio = 1.25; vm.On = true; vm.Colour = Colour.Green; vm.Id = id; vm.When = when;
        vm.Span = TimeSpan.FromMinutes(90); vm.Maybe = 7; vm.Note = null; vm.Filter = new Filter("x y", 2);

        var text = RouteableState.Serialize(RouteableState.CaptureSnapshots(router));
        await router.EraseHistoryAsync(ct);
        await RouteableState.RestoreSnapshotsAsync(router, RouteableState.DeserializeSnapshots(text), ct);

        var back = Assert.IsType<FormVm>(router.CurrentViewModel);
        Assert.Equal("a|b=c\nd", back.Title);
        Assert.Equal(3, back.Count);
        Assert.Equal(1.25, back.Ratio);
        Assert.True(back.On);
        Assert.Equal(Colour.Green, back.Colour);
        Assert.Equal(id, back.Id);
        Assert.Equal(when, back.When);
        Assert.Equal(TimeSpan.FromMinutes(90), back.Span);
        Assert.Equal(7, back.Maybe);
        Assert.Null(back.Note);
        Assert.Equal(new Filter("x y", 2), back.Filter);
    }

    [Fact]
    public async Task A_null_is_kept_as_a_null()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/form", cancellationToken: TestContext.Current.CancellationToken);
        var vm = Assert.IsType<FormVm>(router.CurrentViewModel);
        vm.Count = 1;
        var snapshot = RouteableState.CaptureSnapshots(router).Single();

        Assert.Equal("v1", snapshot.State["persist:Count"]);
        Assert.Equal("n", snapshot.State["persist:Note"]);
    }

    [Fact]
    public void A_type_without_JSON_information_is_not_written()
    {
        var into = new Dictionary<string, string>();

        Assert.False(new PersistBag((IDictionary<string, string>)into).Set("Odd", new List<string> { "a" }));
        Assert.Empty(into);
    }

    [Fact]
    public void A_text_longer_than_the_limit_is_not_kept()
    {
        var into = new Dictionary<string, string>();
        var bag = new PersistBag((IDictionary<string, string>)into);
        var old = RouteablePersistence.MaxValueLength;
        try
        {
            RouteablePersistence.MaxValueLength = 10;
            Assert.False(bag.Set("Big", new string('x', 11)));
            Assert.True(bag.Set("Small", "short"));
        }
        finally { RouteablePersistence.MaxValueLength = old; }

        Assert.False(into.ContainsKey("persist:Big"));
        Assert.Equal("vshort", into["persist:Small"]);
    }

    [Fact]
    public void A_value_that_cannot_be_read_back_leaves_the_property_alone()
    {
        var from = new Dictionary<string, string> { ["persist:Count"] = "vnot a number", ["persist:Colour"] = "v99" };
        var bag = new PersistBag((IReadOnlyDictionary<string, string>)from);

        Assert.False(bag.TryGet<int>("Count", out _));
        Assert.False(bag.TryGet<Colour>("Colour", out _));
        Assert.False(bag.TryGet<int>("Missing", out _));
    }

    [Fact]
    public async Task The_properties_of_the_base_class_are_kept_with_those_of_the_class()
    {
        using var provider = Build();
        var router = provider.GetRequiredService<IRouter>();
        await router.GoToAsync("/form", cancellationToken: TestContext.Current.CancellationToken);
        Assert.IsType<FormVm>(router.CurrentViewModel).Title = "inherited";

        var snapshot = RouteableState.CaptureSnapshots(router).Single();

        Assert.Equal("vinherited", snapshot.State["persist:Title"]);
        Assert.True(RouteablePersistence.Has(typeof(FormVm)));
        Assert.False(RouteablePersistence.Has(typeof(string)));
    }

    private sealed class CsvConverter : IPersistConverter<Filter>
    {
        public string? Write(Filter value) => value.Text + ";" + value.Page;

        public bool TryRead(string text, out Filter value)
        {
            var parts = text.Split(';');
            if (parts.Length == 2 && int.TryParse(parts[1], out var page))
            {
                value = new Filter(parts[0], page);
                return true;
            }

            value = null!;
            return false;
        }
    }

    [Fact]
    public void A_converter_given_to_the_property_chooses_the_format()
    {
        var into = new Dictionary<string, string>();
        var converter = new CsvConverter();

        Assert.True(new PersistBag((IDictionary<string, string>)into).Set("F", new Filter("a b", 4), converter));
        Assert.Equal("va b;4", into["persist:F"]);

        var read = new PersistBag((IReadOnlyDictionary<string, string>)into);
        Assert.True(read.TryGet<Filter>("F", converter, out var back));
        Assert.Equal(new Filter("a b", 4), back);
        Assert.False(new PersistBag((IReadOnlyDictionary<string, string>)new Dictionary<string, string> { ["persist:F"] = "vnope" }).TryGet<Filter>("F", converter, out _));
    }

    [Fact]
    public void A_converter_registered_for_a_type_is_used_by_every_property_of_that_type_and_can_replace_a_built_in_format()
    {
        RouteablePersistence.RegisterConverter<Guid>(g => g.ToString("N"), (string text, out Guid g) => Guid.TryParseExact(text, "N", out g));
        try
        {
            var id = Guid.NewGuid();
            var into = new Dictionary<string, string>();
            new PersistBag((IDictionary<string, string>)into).Set("Id", id);
            new PersistBag((IDictionary<string, string>)into).Set<Guid?>("Maybe", id);

            Assert.Equal("v" + id.ToString("N"), into["persist:Id"]);
            var read = new PersistBag((IReadOnlyDictionary<string, string>)into);
            Assert.True(read.TryGet<Guid>("Id", out var back));
            Assert.Equal(id, back);
            Assert.True(read.TryGet<Guid?>("Maybe", out var maybe));
            Assert.Equal(id, maybe);
        }
        finally
        {
            RouteablePersistence.RegisterConverter<Guid>(g => g.ToString("D"), (string text, out Guid g) => Guid.TryParse(text, out g));
        }
    }

    [Fact]
    public void A_converter_that_returns_null_leaves_the_value_out()
    {
        var into = new Dictionary<string, string>();

        Assert.False(new PersistBag((IDictionary<string, string>)into).Set("F", new Filter("x", 1), new NullConverter()));
        Assert.Empty(into);
    }

    private sealed class NullConverter : IPersistConverter<Filter>
    {
        public string? Write(Filter value) => null;
        public bool TryRead(string text, out Filter value) { value = null!; return false; }
    }

    [Fact]
    public void A_flags_enum_comes_back_with_its_combination_but_a_number_that_names_nothing_is_refused()
    {
        var into = new Dictionary<string, string>();
        new PersistBag((IDictionary<string, string>)into).Set("A", Access.Read | Access.Write);
        var read = new PersistBag((IReadOnlyDictionary<string, string>)into);

        Assert.True(read.TryGet<Access>("A", out var back));
        Assert.Equal(Access.Read | Access.Write, back);

        var odd = new PersistBag((IReadOnlyDictionary<string, string>)new Dictionary<string, string> { ["persist:A"] = "v8" });
        Assert.False(odd.TryGet<Access>("A", out _));
    }
}
