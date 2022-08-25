using System;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Server.Commands.Interfaces;

namespace Shoko.Server.Commands
{
    public class CommandRequestFactory : ICommandRequestFactory
    {
        private readonly IServiceProvider _provider;

        public CommandRequestFactory(IServiceProvider provider)
        {
            _provider = provider;
        }

        public T Create<T>(Action<T> ctor = null) where T : ICommandRequest
        {
            var obj = ActivatorUtilities.CreateInstance<T>(_provider);
            ctor?.Invoke(obj);
            obj.GenerateCommandID();
            return obj;
        }
    }
}
