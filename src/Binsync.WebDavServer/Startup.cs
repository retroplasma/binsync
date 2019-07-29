using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

using FubarDev.WebDavServer.AspNetCore;
using FubarDev.WebDavServer.AspNetCore.Logging;
using FubarDev.WebDavServer.FileSystem;
using FubarDev.WebDavServer.Locking;
using FubarDev.WebDavServer.Locking.InMemory;
using FubarDev.WebDavServer.Props.Store;
using FubarDev.WebDavServer.Props.Store.InMemory;
using Binsync.WebDavServer.Middlewares;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;


namespace Binsync.WebDavServer
{
	public class Startup
	{
		public Startup(IConfiguration configuration)
		{
			Configuration = configuration;
		}

		public IConfiguration Configuration { get; }

		// This method gets called by the runtime. Use this method to add services to the container.
		public void ConfigureServices(IServiceCollection services)
		{
			services.Configure<WebDavHostOptions>(cfg => Configuration.Bind("Host", cfg));
			services.AddMvcCore()
					.AddWebDav();
			services.AddScoped<IFileSystemFactory, BinsyncFileSystemFactory>();
			services.AddScoped<IPropertyStoreFactory, InMemoryPropertyStoreFactory>();
			services.AddSingleton<ILockManager, InMemoryLockManager>();
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IHostingEnvironment env, IApplicationLifetime lifetime)
		{
			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}

			app.UseMiddleware<RequestLogMiddleware>();

			if (!Program.IsKestrel)
			{
				app.UseMiddleware<ImpersonationMiddleware>();
			}

			app.UseForwardedHeaders(new ForwardedHeadersOptions()
			{
				ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
			});

			app.UseMvc();

			lifetime.ApplicationStarted.Register(Shell.Launch);
		}
	}
}
