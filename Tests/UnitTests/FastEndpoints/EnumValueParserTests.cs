using FastEndpoints;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace EnumValueParser;

public class EnumValueParserTests
{
    [Fact]
    public void SingleDefinedNameBinds()
    {
        var res = typeof(Category).ValueParser()("One");

        res.IsSuccess.ShouldBeTrue();
        res.Value.ShouldBe(Category.One);
    }

    [Fact]
    public void UndefinedNumericDoesNotBind()
        => typeof(Category).ValueParser()("99").IsSuccess.ShouldBeFalse();

    [Fact]
    public void CommaSeparatedNamesDoNotBindForNonFlags()
    {
        typeof(Category).ValueParser()("One,Two").IsSuccess.ShouldBeFalse();
        typeof(Category).ValueParser()("One, Two").IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void RepeatedValuesDoNotBindForNonFlags()
        => typeof(Category).ValueParser()(new StringValues(["One", "Two"])).IsSuccess.ShouldBeFalse();

    [Fact]
    public void NullableNonFlagsUsesTheSameRule()
        => typeof(Category?).ValueParser()("One,Two").IsSuccess.ShouldBeFalse();

    [Fact]
    public void FlagsCommaListBindsWhenCombinationIsDefined()
    {
        var res = typeof(Perms).ValueParser()("Read,Write");

        res.IsSuccess.ShouldBeTrue();
        res.Value.ShouldBe(Perms.ReadWrite);
    }

    [Fact]
    public void FlagsRepeatedValuesBindWhenCombinationIsDefined()
    {
        var res = typeof(Perms).ValueParser()(new StringValues(["Read", "Write"]));

        res.IsSuccess.ShouldBeTrue();
        res.Value.ShouldBe(Perms.ReadWrite);
    }

    [Fact]
    public void FlagsCommaListDoesNotBindWhenCombinationIsUndefined()
        => typeof(Perms).ValueParser()("Read, Execute").IsSuccess.ShouldBeFalse();

    [Fact]
    public void AllowUndefinedEnumValuesRestoresTryParse()
    {
        var previous = Config.BndOpts.AllowUndefinedEnumValues;
        Config.BndOpts.AllowUndefinedEnumValues = true;

        try
        {
            var combined = typeof(Category).ValueParser()("One,Two");
            combined.IsSuccess.ShouldBeTrue();
            combined.Value.ShouldBe(Category.Three);
            typeof(Category).ValueParser()("99").IsSuccess.ShouldBeTrue();
        }
        finally
        {
            Config.BndOpts.AllowUndefinedEnumValues = previous;
        }
    }

    enum Category
    {
        One = 1,
        Two = 2,
        Three = 3
    }

    [Flags]
    enum Perms
    {
        Read = 1,
        Write = 2,
        Execute = 4,
        ReadWrite = Read | Write
    }
}
