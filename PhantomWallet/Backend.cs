using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Phantasma.RpcClient;
using Phantasma.RpcClient.Interfaces;
using Phantom.Wallet.Helpers;
using Serilog;
using Serilog.Core;
using System.Globalization;
using System.Reflection;

namespace Phantom.Wallet
{
    class Backend
    {
        public static IServiceProvider AppServices => _app.Services;

        private static IServiceCollection serviceCollection; //= new ServiceCollection();
        private static Application _app; //= new Application(serviceCollection);
        private static Logger Logger = new LoggerConfiguration().MinimumLevel.Debug()
                                    .WriteTo.File(Utils.LogPath).CreateLogger();
  	    public static string Port { get; set; }
  	    public static string Path { get; set; }

        static void Main(string[] args)
        {
            Init();
	          ParseArgs(args);
            SetDefaultCulture(new CultureInfo("en-US"));
            var server = HostBuilder.CreateServer(args);
            var viewsRenderer = new ViewsRenderer(server, "views");
            Console.WriteLine("UTILS LOGPATH: " + Utils.LogPath);

            viewsRenderer.SetupHandlers();
            viewsRenderer.SetupControllers();

	        OpenBrowser("http://localhost:"+Port);
            server.Run();
        }

        public static void Init(bool reInit = false)
        {
            if (_app == null || reInit)
            {
                serviceCollection = new ServiceCollection();
                _app = new Application(serviceCollection);
            }
        }

	    public static void ParseArgs(String[] args) {
	        // code from LunarServer
	        foreach (var arg in args)
            {
                if (!arg.StartsWith("--"))
                {
                    continue;
                }

                var temp = arg.Substring(2).Split(new char[] { '=' }, 2);
                var key = temp[0].ToLower();
                var val = temp.Length > 1 ? temp[1] : "";

                switch (key)
                {
                    case "path": Path = val; break;
                    case "port": Port = val; break;
                }
            }
  	    }

	    public static void OpenBrowser(string url)
	    {
	        try
	        {
	            Process.Start(url);
	        }
	        catch
	        {
	            // hack because of this: https://github.com/dotnet/corefx/issues/10361
	            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
	            {
	                url = url.Replace("&", "^&");
	                Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
	            }
	            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
	            {
	                Process.Start("xdg-open", url);
	            }
	            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
	            {
	                Process.Start("open", url);
	            }
	            else
	            {
	                throw;
	            }
	        }
	    }

      static void SetDefaultCulture(CultureInfo culture)
      {
          Type type = typeof(CultureInfo);

          try
          {
              type.InvokeMember("s_userDefaultCulture",
                                  BindingFlags.SetField | BindingFlags.NonPublic | BindingFlags.Static,
                                  null,
                                  culture,
                                  new object[] { culture });

              type.InvokeMember("s_userDefaultUICulture",
                                  BindingFlags.SetField | BindingFlags.NonPublic | BindingFlags.Static,
                                  null,
                                  culture,
                                  new object[] { culture });
          }
          catch { }

          try
          {
              type.InvokeMember("m_userDefaultCulture",
                                  BindingFlags.SetField | BindingFlags.NonPublic | BindingFlags.Static,
                                  null,
                                  culture,
                                  new object[] { culture });

              type.InvokeMember("m_userDefaultUICulture",
                                  BindingFlags.SetField | BindingFlags.NonPublic | BindingFlags.Static,
                                  null,
                                  culture,
                                  new object[] { culture });
          }
          catch { }
      }
    }

    public class Application
    {
        public IServiceProvider Services { get; set; }
        private static Logger Logger = new LoggerConfiguration().MinimumLevel.Debug()
                                    .WriteTo.File(Utils.LogPath).CreateLogger();

        public Application(IServiceCollection serviceCollection)
        {
            ConfigureServices(serviceCollection);
            Services = serviceCollection.BuildServiceProvider();
        }

        private void ConfigureServices(IServiceCollection serviceCollection)
        {
            Logger.Information("RpcServerUrl " + Settings.RpcServerUrl);
            serviceCollection.AddScoped<IPhantasmaRpcService>(provider => new PhantasmaRpcService(
                new Phantasma.RpcClient.Client.RpcClient(new Uri(Settings.RpcServerUrl), httpClientHandler: new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            })));
        }
    }
}
