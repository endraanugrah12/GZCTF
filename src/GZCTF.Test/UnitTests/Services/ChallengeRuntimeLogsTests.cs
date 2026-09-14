using System;
using System.Linq;
using System.Threading.Tasks;
using GZCTF.Controllers;
using GZCTF.Models;
using GZCTF.Models.Data;
using GZCTF.Services.Container;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using RuntimeContainer = GZCTF.Models.Data.Container;

namespace GZCTF.Test.UnitTests.Services;

public class ChallengeRuntimeLogsTests
{
    [Theory]
    [InlineData("38.147.122.175", "38.147.122.175")]
    [InlineData("[2001:db8::1]", "2001:db8::1")]
    public async Task DirectAddressUsesLiteralIp(string host, string expected)
        => Assert.Equal(expected, await PublicInstanceAddress.ResolveAsync(host, default));

    [Fact]
    public void EndpointBracketsIpv6AndLogCaptureIsBounded()
    {
        Assert.Equal("[2001:db8::1]:32000", new RuntimeContainer { PublicIP = "2001:db8::1", PublicPort = 32000 }.Entry);
        var log = new ChallengeRuntimeLogsController.BoundedLog();
        log.Report(new string('x', 70000));
        log.Report("more");
        Assert.True(log.Truncated);
        Assert.Equal(65536, log.ToString().Length);
    }

    [Fact]
    public async Task LogsCannotSelectAnotherGameOrChallengeContainer()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var first = new RuntimeContainer { ContainerId = "first" };
        var other = new RuntimeContainer { ContainerId = "other" };
        db.AddRange(new GameChallenge { Id = 1, GameId = 10, Title = "First", TestContainer = first },
            new GameChallenge { Id = 2, GameId = 20, Title = "Other", TestContainer = other });
        await db.SaveChangesAsync();
        var controller = new ChallengeRuntimeLogsController(db, null!, NullLogger<ChallengeRuntimeLogsController>.Instance);
        Assert.Equal(first.Id, (await controller.Containers(10, 1).SingleAsync()).Id);
        Assert.Empty(await controller.Containers(20, 1).ToArrayAsync());
        Assert.DoesNotContain(other.Id, await controller.Containers(10, 1).Select(c => c.Id).ToArrayAsync());
    }
}
