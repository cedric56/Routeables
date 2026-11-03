using System.Globalization;
using Routeables.Abstraction;
using Routeables.Maui;
using MauiBinding = Microsoft.Maui.Controls.Binding;

namespace Routeables.Tests.Maui;

public class MauiHelpersTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData(" \t", true)]
    [InlineData("value", false)]
    [InlineData(0, false)]
    public void NullConverter_reports_null_or_whitespace(object? value, bool expected)
    {
        var converter = new RouteableNullConverter();

        var result = converter.Convert(value, typeof(bool), null, CultureInfo.InvariantCulture);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void NullConverter_does_not_convert_back()
    {
        var converter = new RouteableNullConverter();

        var result = converter.ConvertBack(true, typeof(string), null, CultureInfo.InvariantCulture);

        Assert.Same(MauiBinding.DoNothing, result);
    }

    [Fact]
    public void ParameterAttribute_can_be_applied_to_properties()
    {
        var attribute = Assert.Single(typeof(ParameterTarget).GetProperty(nameof(ParameterTarget.Value))!
            .GetCustomAttributes(typeof(RouteableParameterAttribute), inherit: true));

        Assert.IsType<RouteableParameterAttribute>(attribute);
    }

    private sealed class ParameterTarget
    {
        [RouteableParameter]
        public string? Value { get; set; }
    }
}
