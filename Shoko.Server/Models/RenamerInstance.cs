using System;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Plugin.Abstractions;
using Shoko.Server.Renamer;
using Shoko.Server.Utilities;

namespace Shoko.Server.Models;

public class RenamerInstance : IRenamerInstance
{
    public int ID { get; set; }
    public string Name { get; set; }
    public Type Type { get; set; }
    public object Settings { get; set; }

    private IBaseRenamer _renamer;
    public IBaseRenamer Renamer
    {
        get
        {
            if (_renamer == null)
            {
                var renamerService = Utils.ServiceContainer.GetRequiredService<RenameFileService>();
                if (!renamerService.RenamersByType.TryGetValue(Type, out var renamer)) throw new Exception("Renamer not found");
                _renamer = renamer;
            }
            return _renamer;
        }
    }
}
