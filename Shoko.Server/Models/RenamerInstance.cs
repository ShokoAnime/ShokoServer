using System;
using Shoko.Plugin.Abstractions;

namespace Shoko.Server.Models;

public class RenamerInstance : IRenamerInstance
{
    public int ID { get; set; }
    public string Name { get; set; }
    public Type Type { get; set; }
    public object Settings { get; set; }
}
