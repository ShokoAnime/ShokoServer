using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Shoko.Models.Server;
using Shoko.Server.Native.Hashing;


namespace Shoko.Server.CommandQueue.Commands
{
    public static class CommandExtensions
    {
        public static ICommand ToCommand(this CommandRequest cmd)
        {
            if (!Resolver.Instance.StringToCommand.TryGetValue(cmd.Class, out Type t))
                return null;
            ConstructorInfo ctor = t.GetConstructor(new[] { typeof(string) });
            if (ctor == null)
                return null;
            ICommand worker = (ICommand)ctor.Invoke(new object[] { cmd.Data });
            worker.ParallelMax = cmd.ParallelMax;
            worker.ParallelTag = cmd.ParallelTag;
            worker.Priority = cmd.Priority;
            worker.Retries = cmd.Retries;
            worker.Batch = cmd.Batch;
            worker.RetryFutureInSeconds = cmd.RetryFutureSeconds;
            DeserializePrecondition(worker, cmd.PreconditionClass1);
            DeserializePrecondition(worker, cmd.PreconditionClass2);
            DeserializePrecondition(worker, cmd.PreconditionClass3);
            DeserializePrecondition(worker, cmd.PreconditionClass4);
            DeserializePrecondition(worker, cmd.PreconditionClass5);
            DeserializePrecondition(worker, cmd.PreconditionClass6);
            return worker;
        }
        public static void DeserializePrecondition(ICommand cmd, string precog)
        {
            if (cmd.GenericPreconditions==null)
                cmd.GenericPreconditions=new List<Type>();
            if (string.IsNullOrEmpty(precog))
                return;
            if (Resolver.Instance.StringToGenericPrecondition.TryGetValue(precog, out Type t))
                cmd.GenericPreconditions.Add(t);
        }
        public static string SerializePrecondition(ICommand command, int index)
        {
            if (command.GenericPreconditions.Count < index)
                return null;
            if (Resolver.Instance.GenericPreconditionToString.TryGetValue(command.GenericPreconditions[index], out string t))
                return t;
            return null;
        }
        public static CommandRequest ToCommandRequest(this ICommand b)
        {
            if (b.GenericPreconditions.Count>6)
                throw new Exception("Unable to serialize CommandRequest, the maximum number of preconditions supported are 6");
            CommandRequest c = new CommandRequest();
            c.Id = b.Id;
            c.Class = b.GetType().ToString();
            c.Type = b.WorkType;
            c.ParallelMax = b.ParallelMax;
            c.ParallelTag = b.ParallelTag;
            c.Priority = b.Priority;
            c.Retries = b.Retries;
            c.Batch = b.Batch;
            c.Data = b.Serialize();
            c.RetryFutureSeconds = b.RetryFutureInSeconds;
            c.PreconditionClass1 = SerializePrecondition(b, 0);
            c.PreconditionClass2 = SerializePrecondition(b, 1);
            c.PreconditionClass3 = SerializePrecondition(b, 2);
            c.PreconditionClass4 = SerializePrecondition(b, 3);
            c.PreconditionClass5 = SerializePrecondition(b, 4);
            c.PreconditionClass6 = SerializePrecondition(b, 5);
            return c;
        }

        public static string GetHash(this Dictionary<HashTypes, byte[]> dic, HashTypes t)
        {
            if (!dic.ContainsKey(t))
                return null;
            byte[] data = dic.First(a => a.Key == t).Value;
            return BitConverter.ToString(data).Replace("-", string.Empty);
        }

    }
}