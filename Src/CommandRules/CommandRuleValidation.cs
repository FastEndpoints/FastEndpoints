namespace FastEndpoints;

static class CommandRuleValidation
{
    public static void ValidateRuleMatch(Type ruleType, CommandRuleMatch? match)
    {
        if (match is null)
            throw new CommandRuleException($"Command rule [{ruleType.FullName}] returned a null match.");

        if (!match.Matched)
            return;

        var ruleName = ruleType.FullName;

        ValidateCommands(
            match.Commands,
            $"Command rule [{ruleName}] returned a null command list.",
            $"Command rule [{ruleName}] returned a null planned command.",
            $"Command rule [{ruleName}] returned a planned command with a null command.");
    }

    public static void ValidateRulePlan(CommandRulePlan? plan)
    {
        if (plan is null)
            throw new CommandRuleException("Command engine returned a null rule plan.");

        ValidateCommands(
            plan.Commands,
            "Command engine returned a null command list.",
            "Command engine returned a null planned command.",
            "Command engine returned a planned command with a null command.");

        switch (plan.MatchedRuleCount)
        {
            case < 0:
                throw new CommandRuleException("Command engine returned a negative matched rule count.");
            case 0 when plan.Commands.Count > 0:
                throw new CommandRuleException("Command engine returned commands without any matched rules.");
        }
    }

    static void ValidateCommands(IReadOnlyList<PlannedCommand>? commands, string nullListMessage, string nullPlannedMessage, string nullCommandMessage)
    {
        if (commands is null)
            throw new CommandRuleException(nullListMessage);

        foreach (var planned in commands)
        {
            if (planned is null)
                throw new CommandRuleException(nullPlannedMessage);

            if (planned.Command is null)
                throw new CommandRuleException(nullCommandMessage);
        }
    }
}