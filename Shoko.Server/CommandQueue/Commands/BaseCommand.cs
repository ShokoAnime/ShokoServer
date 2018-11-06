using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NLog;

namespace Shoko.Server.CommandQueue.Commands
{
    public class BaseCommand<T> where T: ICommand
    {
        protected static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public BaseCommand()
        {
        }

        protected BaseCommand(string str)
        {
            JsonConvert.PopulateObject(str, this, JsonSettings);
        }
        [JsonIgnore]
        public virtual CommandProgress<T> Progress { get; } = new CommandProgress<T>();
        public static JsonSerializerSettings JsonSettings { get; } = new JsonSerializerSettings {Formatting = Formatting.None, NullValueHandling = NullValueHandling.Ignore, ContractResolver = new InterfaceContractResolver()};

        public virtual string Serialize()
        {
            return JsonConvert.SerializeObject(this, JsonSettings);
        }

        public virtual async Task<CommandResult> RunAsync(IProgress<ICommandProgress> progress = null, CancellationToken token = default(CancellationToken))
        {
            return await Task.FromResult(Run(progress));
        }

        public virtual CommandResult Run(IProgress<ICommandProgress> progress = null)
        {
            try
            {
                return RunAsync(progress).GetAwaiter().GetResult();
            }
            catch (AggregateException e)
            {
                throw e.Flatten().InnerExceptions.First();
            }
        }

        public void InitProgress(IProgress<ICommandProgress> prg, bool report = true)
        {
           
            Progress.Progress = 0;
            Progress.Command = (T)(object)this;
            if (report)
                prg?.Report(Progress);
        }

        
        public void UpdateAndReportProgress(IProgress<ICommandProgress> prg, double value)
        {
            Progress.Progress = value;
            prg?.Report(Progress);
        }

        public CommandResult ReportErrorAndGetResult(IProgress<ICommandProgress> prg, CommandResultStatus status, string error, Exception e = null)
        {
            Progress.Status = status;
            Progress.Error = error;
            prg?.Report(Progress);
            if (e != null)
                logger.Error(e, error);
            else
                logger.Error(error);
            return new CommandResult(status, error);
        }

        public CommandResult ReportFinishAndGetResult(IProgress<ICommandProgress> prg)
        {
            Progress.Progress = 100;
            prg?.Report(Progress);
            return new CommandResult();
        }
        public int Retries { get; set; }
        public string Batch { get; set; }
        private class InterfaceContractResolver : DefaultContractResolver
        {
            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                JsonProperty property = base.CreateProperty(member, memberSerialization);
                Type[] interfaces = member.DeclaringType?.GetInterfaces() ?? new Type[0];
                foreach (Type iface in interfaces)
                {
                    foreach (PropertyInfo interfaceProperty in iface.GetProperties())
                    {
                        // This is weak: among other things, an implementation 
                        // may be deliberately hiding an interface member
                        if (interfaceProperty.Name == member.Name && interfaceProperty.MemberType == member.MemberType)
                        {
                            if (interfaceProperty.GetCustomAttributes(typeof(JsonIgnoreAttribute), true).Any())
                            {
                                property.Ignored = true;
                                return property;
                            }
                        }
                    }
                }

                Type[] subclasses = member.DeclaringType?.GetNestedTypes() ?? new Type[0];
                foreach (Type cls in subclasses)
                {
                    foreach (PropertyInfo prop in cls.GetProperties())
                    {
                        // This is weak: among other things, an implementation 
                        // may be deliberately hiding an interface member
                        if (prop.Name == member.Name && prop.MemberType == member.MemberType)
                        {
                            if (prop.GetCustomAttributes(typeof(JsonIgnoreAttribute), true).Any())
                            {
                                property.Ignored = true;
                                return property;
                            }
                        }
                    }
                }

                return property;
            }
        }
    }
}