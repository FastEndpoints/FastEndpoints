using System.Runtime.CompilerServices;
using Xunit;

namespace ProjectTestRunner.Helpers
{
    public sealed class PrettyTheoryAttribute : TheoryAttribute
    {
        public PrettyTheoryAttribute([CallerMemberName] string memberName = null) => DisplayName = memberName;
    }
}
