﻿using IntegrationTests.Shared.Fixtures;
using TestCases.CommandBusTest;
using Xunit;
using Xunit.Abstractions;

namespace FastEndpoints.IntegrationTests.CommandBusTests;

public class CommandBusTests : EndToEndTestBase
{
    public CommandBusTests(EndToEndTestFixture endToEndTestFixture, ITestOutputHelper outputHelper) : base(endToEndTestFixture, outputHelper)
    {
    }

    [Fact]
    public async Task CommandThatReturnsAResult()
    {
        var res1 = await new TestCommand
        {
            FirstName = "johnny",
            LastName = "lawrence"
        }
        .ExecuteAsync();

        var res2 = await new TestCommand
        {
            FirstName = "jo",
            LastName = "law"
        }
        .ExecuteAsync();

        res1.Should().Be("johnny lawrence");
        res2.Should().Be("jo law");
    }

    [Fact]
    public async Task CommandThatReturnsVoid()
    {
        var cmd = new TestVoidCommand
        {
            FirstName = "johnny",
            LastName = "lawrence"
        };

        await cmd.ExecuteAsync();

        cmd.FirstName.Should().Be("pass");
        cmd.LastName.Should().Be("pass");
    }
}