using Microsoft.Extensions.DependencyInjection;
using Shoko.Server.Settings;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shoko.Tests
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {

            var settingsMock = new Moq.Mock<ServerSettings>();
            settingsMock.Setup(s => s.SaveSettings()).Callback(() => { });
            settingsMock.SetupAllProperties();

            services.AddSingleton(settingsMock.Object);

            ServerSettings.ConfigureServices(services);
        }
    }
}
