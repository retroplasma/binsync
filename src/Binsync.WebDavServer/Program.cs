using System;
using System.Threading.Tasks;
using System.Linq;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.HttpSys;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Binsync.WebDavServer
{
	public class Program
	{
		public static bool IsKestrel { get; private set; }

		public static bool IsWindows => Environment.OSVersion.Platform == PlatformID.Win32NT;

		public static Binsync.Core.Engine BinsyncEngine;

		public static void Main(string[] args)
		{
			var config = Binsync.Util.CommandLine.ConfigForMain(args);
			if (config == null) return;
			BinsyncEngine = config.ToEngine();
			BinsyncEngine.Load().Wait();
			//BinsyncEngine.ForceFlushParity().Wait();

			BuildWebHost(args).Run();
		}

		private static IWebHost BuildWebHost(string[] args)
		{
			var tempHost = BuildWebHost(args, whb => whb);
			var config = tempHost.Services.GetRequiredService<IConfiguration>();

			return BuildWebHost(
				args,
				builder => ConfigureHosting(builder, config));
		}

		private static IWebHost BuildWebHost(string[] args, Func<IWebHostBuilder, IWebHostBuilder> customConfig) =>
			customConfig(WebHost.CreateDefaultBuilder(args))
				.UseStartup<Startup>()
				.Build();

		private static IWebHostBuilder ConfigureHosting(IWebHostBuilder builder, IConfiguration configuration)
		{
			var forceKestrelUse = configuration.GetValue<bool>("use-kestrel");
			if (!forceKestrelUse && IsWindows)
			{
				builder = builder
					.UseHttpSys(
						opt =>
						{
							opt.Authentication.Schemes = AuthenticationSchemes.NTLM;
							opt.Authentication.AllowAnonymous = true;
						});
			}
			else
			{
				IsKestrel = true;
				builder = builder
					.UseKestrel(
						opt => { opt.Limits.MaxRequestBodySize = null; });
			}

			return builder;
		}
	}
}
