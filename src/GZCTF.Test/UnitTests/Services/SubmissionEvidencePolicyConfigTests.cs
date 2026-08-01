using System;
using System.Collections.Generic;
using GZCTF.Models.Internal;
using GZCTF.Services.Config;
using Xunit;

namespace GZCTF.Test.UnitTests.Services;

public class SubmissionEvidencePolicyConfigTests
{
    private sealed class UnsupportedCollectionConfig
    {
        public List<string> Values { get; set; } = ["one"];
    }

    [Fact]
    public void ConfigStore_PersistsAllowedHostsThroughScalarSurrogate()
    {
        var policy = new SubmissionEvidencePolicy
        {
            AllowedLinkHosts = ["chatgpt.com", "claude.ai"],
            AllowedLinkHostsCsv = "chatgpt.com,claude.ai"
        };

        var configs = ConfigService.GetConfigs(policy);

        var hosts = Assert.Single(configs, config =>
            config.ConfigKey == "SubmissionEvidencePolicy:AllowedLinkHostsCsv");
        Assert.Equal("chatgpt.com,claude.ai", hosts.Value);
        Assert.DoesNotContain(configs, config =>
            config.ConfigKey.StartsWith("SubmissionEvidencePolicy:AllowedLinkHosts:"));
    }

    [Fact]
    public void DatabaseOverride_ReplacesDeclarativeHostDefaults()
    {
        var policy = new SubmissionEvidencePolicy
        {
            AllowedLinkHosts = ["default.example"],
            AllowedLinkHostsCsv = "chatgpt.com, claude.ai\nperplexity.ai"
        };

        policy.ApplyAllowedLinkHostsOverride();

        Assert.Equal(["chatgpt.com", "claude.ai", "perplexity.ai"], policy.AllowedLinkHosts);
    }

    [Fact]
    public void ConfigStore_RejectsConcreteCollectionsBeforeReflectingIndexers()
    {
        var error = Assert.Throws<NotSupportedException>(() =>
            ConfigService.GetConfigs(new UnsupportedCollectionConfig()));

        Assert.DoesNotContain("Parameter count mismatch", error.Message);
    }
}
