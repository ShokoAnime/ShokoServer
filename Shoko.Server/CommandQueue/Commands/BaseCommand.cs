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
 
    public abstract class BaseCommand 
    {
        protected static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public BaseCommand()
        {
        }
        public double Progress { get; set; }
        public CommandStatus Status { get; set; } = CommandStatus.Canceled;
        public string Error { get; set; }
        public int MaxRetries { get; set; } = 3;

        protected BaseCommand(string str)
        {
            JsonConvert.PopulateObject(str, this, JsonSettings);
        }
        public static JsonSerializerSettings JsonSettings { get; } = new JsonSerializerSettings {Formatting = Formatting.None, NullValueHandling = NullValueHandling.Ignore, ContractResolver = new InterfaceContractResolver()};

        public virtual string Serialize()
        {
            return JsonConvert.SerializeObject(this, JsonSettings);
        }

        public virtual async Task RunAsync(IProgress<ICommand> progress = null, CancellationToken token = default(CancellationToken))
        {
            await Task.Run(() => Run(progress), token);
        }

        public virtual void Run(IProgress<ICommand> progress = null)
        {
            try
            {
                RunAsync(progress).GetAwaiter().GetResult();
            }
            catch (AggregateException e)
            {
                throw e.Flatten().InnerExceptions.First();
            }
        }

        public void InitProgress(IProgress<ICommand> prg, bool report = true)
        {
           
            Progress = 0;
            Status = CommandStatus.Working;
            if (report)
                prg?.Report((ICommand)this);
        }

        
        public void UpdateAndReportProgress(IProgress<ICommand> prg, double value)
        {
            Progress = value;
            prg?.Report((ICommand)this);
        }

        public void ReportErrorAndGetResult(IProgress<ICommand> prg, CommandStatus status, string error, Exception e = null)
        {
            Status = status;
            Error = error;
            prg?.Report((ICommand)this);
            if (e != null)
                logger.Error(e, error);
            else
                logger.Error(error);
        }
        public void ReportErrorAndGetResult(IProgress<ICommand> prg, string error, Exception e = null)
        {
            Status = CommandStatus.Error;
            Error = error;
            prg?.Report((ICommand)this);
            if (e != null)
                logger.Error(e, error);
            else
                logger.Error(error);
        }
        public void ReportFinishAndGetResult(IProgress<ICommand> prg)
        {
            Progress = 100;
            Status = CommandStatus.Finished;
            prg?.Report((ICommand)this);
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