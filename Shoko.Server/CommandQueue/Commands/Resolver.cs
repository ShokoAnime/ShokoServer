using System;
using System.Collections.Generic;
using System.Linq;

namespace Shoko.Server.CommandQueue.Commands
{
    public class Resolver
    {
        private static readonly Lazy<Resolver> _instance = new Lazy<Resolver>(() => new Resolver());
        public static Resolver Instance => _instance.Value;

        public IReadOnlyDictionary<string, Type> StringToCommand { get; }
        public IReadOnlyDictionary<Type, string> CommandToString { get; }
        public IReadOnlyDictionary<string, Type> StringToGenericPrecondition { get; }
        public IReadOnlyDictionary<Type, string> GenericPreconditionToString { get; }
        public IReadOnlyDictionary<string, Type> StringToPrecondition { get; }
        public IReadOnlyDictionary<Type, string> PreconditionToString { get; }

        private Resolver()
        {
            Type cmdType = typeof(ICommand);
            List<Type> cmds = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(t => cmdType.IsAssignableFrom(t) && !t.IsInterface).ToList();
            StringToCommand = cmds.ToDictionary(a => a.Name, a => a);
            CommandToString = cmds.ToDictionary(a => a, a => a.Name);
            Type preType = typeof(IPrecondition);
            cmds = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(t => preType.IsAssignableFrom(t) && !t.IsInterface).ToList();
            StringToPrecondition = cmds.ToDictionary(a => a.Name, a => a);
            PreconditionToString = cmds.ToDictionary(a => a, a => a.Name);
            StringToGenericPrecondition = cmds.Where(t => !cmdType.IsAssignableFrom(t)).ToDictionary(a => a.Name, a => a);
            GenericPreconditionToString = cmds.Where(t => !cmdType.IsAssignableFrom(t)).ToDictionary(a => a, a => a.Name);
        }

    }
}
