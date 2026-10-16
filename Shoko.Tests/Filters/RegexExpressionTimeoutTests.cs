using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shoko.Abstractions.Filtering.Expressions.Logic.Strings;
using Shoko.Abstractions.Filtering.Expressions.Logic.StringSets;
using Shoko.Abstractions.Filtering.Expressions.Selectors.StringSelectors;
using Shoko.Abstractions.Filtering.Expressions.Selectors.StringSetSelectors;
using Shoko.Abstractions.Utilities;
using Xunit;

namespace Shoko.Tests.Filters;

/// <summary>
/// A user's pattern that backtracks without end counts as no match once it
/// runs out of time, and is not run again after that.
/// </summary>
public class RegexExpressionTimeoutTests
{
    private const string Pattern = "^(a+)+$";

    private static readonly string s_endless = new string('a', 64) + "!";

    [Fact(Timeout = 30_000)]
    public async Task APatternThatRunsOutOfTimeIsNotRunAgain()
    {
        var tripped = new ConcurrentQueue<string>();
        void OnTripped(string pattern, string input)
        {
            if (pattern is Pattern)
                tripped.Enqueue(input);
        }

        var expression = new AnyRegexMatchesExpression(new NamesSelector(), Pattern);
        BoundedRegex.Tripped += OnTripped;
        try
        {
            var endless = new TestFilterable { Names = new HashSet<string> { s_endless } };
            var matching = new TestFilterable { Names = new HashSet<string> { "aaa" } };

            var first = await Task.Run(() => expression.Evaluate(endless, null, null), TestContext.Current.CancellationToken);
            var second = expression.Evaluate(matching, null, null);

            Assert.False(first);
            Assert.False(second);
            Assert.Equal([s_endless], tripped);
        }
        finally
        {
            BoundedRegex.Tripped -= OnTripped;
        }
    }

    [Fact]
    public void APatternThatFinishesStillMatches()
    {
        var filterable = new TestFilterable { Name = "aaa", Names = new HashSet<string> { "aaa" } };

        Assert.True(new StringRegexMatchesExpression(new NameSelector(), Pattern).Evaluate(filterable, null, null));
        Assert.True(new AnyRegexMatchesExpression(new NamesSelector(), Pattern).Evaluate(filterable, null, null));
        Assert.True(new AllRegexMatchesExpression(new NamesSelector(), Pattern).Evaluate(filterable, null, null));
    }
}
