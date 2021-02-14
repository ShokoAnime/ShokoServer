using Shoko.Server.Settings;
using Shoko.Server.Settings.DI;
using System;
using Xunit;

namespace Shoko.Tests
{
    public interface IDependency
    {
        
    }

    internal class DependencyClass : IDependency
    {
        
    }

    public class MyAwesomeTests
    {
        private readonly IConfiguration<AniDbSettings> _d;

        public MyAwesomeTests(IConfiguration<AniDbSettings> d) => _d = d;

        [Fact]
        public void AssertThatWeDoStuff()
        {
            Assert.Equal(1, _d.Instance.ClientPort);
        }
    }
}
