using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using GZCTF.Controllers;
using GZCTF.Models;
using GZCTF.Models.Request.Admin;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GZCTF.Test.UnitTests.Services;

public class AppearanceTests
{
    [Fact]
    public async Task DraftIsPrivateAndPublishAndResetPersist()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using (var db = new AppDbContext(options))
        {
            var controller = new AppearanceController(db);
            await controller.SaveDraft(new AppearanceModel { Title = "Draft brand", HomeMarkdown = "Welcome!" }, default);
            Assert.Empty((await controller.Published(default)).Title);
        }
        await using (var db = new AppDbContext(options))
        {
            Assert.Contains("Draft brand", (await db.Configs.FindAsync("Appearance:Draft"))!.Value);
            await new AppearanceController(db).Publish(new AppearanceModel { Title = "Published brand", PrimaryColor = "#123456" }, default);
        }
        await using (var db = new AppDbContext(options))
        {
            var controller = new AppearanceController(db);
            Assert.Equal("Published brand", (await controller.Published(CancellationToken.None)).Title);
            await controller.Reset(default);
            Assert.Empty((await controller.Published(default)).Title);
        }
        await using (var db = new AppDbContext(options))
            Assert.Empty((await new AppearanceController(db).Published(default)).PrimaryColor);
    }

    [Theory]
    [InlineData("https://example.com/logo.png", true)]
    [InlineData("/assets/logo.png", true)]
    [InlineData("", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("//example.com/logo.png", false)]
    [InlineData("/\\example.com/logo.png", false)]
    [InlineData("http://example.com/logo.png", false)]
    public void ValidatesBrandingUrls(string url, bool expected)
    {
        var model = new AppearanceModel { LogoUrl = url };
        Assert.Equal(expected, Validator.TryValidateObject(model, new ValidationContext(model), new List<ValidationResult>(), true));
    }

    [Fact]
    public void RejectsInvalidColorsAndOversizeStyles()
    {
        var model = new AppearanceModel { PrimaryColor = "red; display:none", CustomCss = new string('x', 20001) };
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), errors, true));
        Assert.Equal(2, errors.Count);
    }
}
