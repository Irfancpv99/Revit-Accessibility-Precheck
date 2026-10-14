using RevitAccessibility.Core.Config;
using Xunit;

namespace RevitAccessibility.Core.Tests;

public class AddinConfigTests
{
    [Fact]
    public void Names_are_read_from_json()
    {
        var c = AddinConfig.Parse("{\"ClearWidthParameter\":\"Width\",\"AccessDoorParameter\":\"Access door\"}");
        Assert.Equal("Width", c.ClearWidthParameter);
        Assert.Equal("Access door", c.AccessDoorParameter);
    }

    [Fact]
    public void Missing_keys_keep_the_defaults()
    {
        var c = AddinConfig.Parse("{\"ClearWidthParameter\":\"Width\"}");
        Assert.Equal("Width", c.ClearWidthParameter);
        Assert.Equal(AddinConfig.DefaultAccessDoorParameter, c.AccessDoorParameter);
    }

    [Fact]
    public void Keys_are_case_insensitive_and_values_trimmed()
    {
        var c = AddinConfig.Parse("{\"clearwidthparameter\":\"  Width  \"}");
        Assert.Equal("Width", c.ClearWidthParameter);
    }

    [Fact]
    public void Comments_trailing_commas_and_bom_are_tolerated()
    {
        var c = AddinConfig.Parse("// note\n{\"ClearWidthParameter\":\"Width\",}".TrimStart('﻿'));
        Assert.Equal("Width", c.ClearWidthParameter);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"ClearWidthParameter\":\"\"}")]
    [InlineData("{\"AccessDoorParameter\":\"   \"}")]
    [InlineData("{\"ClearWidthParameter\":5}")]
    public void Malformed_config_is_an_error_not_a_silent_default(string json)
    {
        Assert.Throws<InvalidDataException>(() => AddinConfig.Parse(json));
    }
}
