using LunarLabs.WebServer.Core;
using LunarLabs.WebServer.HTTP;
using System;

namespace Phantom.Wallet
{
    public static class HostBuilder
    {
        public static HTTPServer CreateServer(string[] args)
        {
            var log = new ConsoleLogger();

            // either parse the settings from the program args or initialize them manually
            var settings = ServerSettings.Parse(args);
            settings.Compression = false;

            //var sessionStorage = new FileSessionStorage("session") { CookieExpiration = TimeSpan.FromHours(6) };

            // instantiate a new site, the second argument is the relative file path where the public site contents will be found
            //return new HTTPServer(settings, log, sessionStorage);
            return new HTTPServer(settings, log);
        }
    }
}
