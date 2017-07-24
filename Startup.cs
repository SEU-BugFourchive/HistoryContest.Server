using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.SpaServices.Webpack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Swagger;

namespace HistoryContest.Server
{
	public class Startup
	{
		public Startup(IHostingEnvironment env)
		{
			var builder = new ConfigurationBuilder()
				.SetBasePath(env.ContentRootPath)
				.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
				.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
				.AddEnvironmentVariables();
			Configuration = builder.Build();
		}

		public IConfigurationRoot Configuration { get; }

		// This method gets called by the runtime. Use this method to add services to the container.
		public void ConfigureServices(IServiceCollection services)
		{
			// Add mvc framework services.
#if DEBUG
			services.AddMvc().AddRazorOptions(options =>
			{
				options.ViewLocationExpanders.Clear();
				options.ViewLocationExpanders.Add(new MyViewEngine());
			});
#else
			services.AddMvc();
#endif

			// Add logging services to application
			services.AddLogging();

			// Register the Swagger generator, defining one or more Swagger documents
			services.AddSwaggerGen(c =>
			{
				c.SwaggerDoc("seu-history-contest", new Info
				{
					Title = "SEU History Contest API",
					Version = "v1",
					Description = "Backend Web API for History Contest System front end.",
					TermsOfService = "None",
					Contact = new Contact { Name = "Vigilans", Email = "vigilans@foxmail.com", Url = "http://vigi.me/" }
				});

				//Set the comments path for the swagger json and ui.
				var basePath = PlatformServices.Default.Application.ApplicationBasePath;
				var xmlPath = Path.Combine(basePath, "HistoryContest.Server.xml");
				c.IncludeXmlComments(xmlPath);
			});
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
		{
			loggerFactory.AddConsole(Configuration.GetSection("Logging"));
			loggerFactory.AddDebug();


			/* ------------static file routes------------*/
			// use wwwroot static files
			app.UseStaticFiles();

			// enable default url rewrite for wiki
			app.UseDefaultFiles(new DefaultFilesOptions()
			{
				FileProvider = new PhysicalFileProvider(Path.Combine(env.ContentRootPath, @"HistoryContest.Wiki")),
				RequestPath = new PathString("/wiki")
			});

			// use wiki static files
			app.UseStaticFiles(new StaticFileOptions()
			{
				FileProvider = new PhysicalFileProvider(Path.Combine(env.ContentRootPath, @"HistoryContest.Wiki")),
				RequestPath = new PathString("/wiki")
			});
			/* ------------static file routes------------*/


			/* ------------api document routes------------*/
			// Enable middleware to serve generated Swagger as a JSON endpoint.
			app.UseSwagger();

			// Enable middleware to serve swagger-ui (HTML, JS, CSS etc.), specifying the Swagger JSON endpoint.
			app.UseSwaggerUI(c =>
			{
				c.SwaggerEndpoint("/swagger/seu-history-contest/swagger.json", "SEU History Contest API v1");
			});
			/* ------------api document routes------------*/


			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
				app.UseWebpackDevMiddleware(new WebpackDevMiddlewareOptions {
					HotModuleReplacement = true,
					ProjectPath = env.ContentRootPath + "/HistoryContest.Client"
				});
			}
			else
			{
				app.UseExceptionHandler("/Home/Error");
			}


			/* ------------javascript spa routes------------*/
			app.UseMvc(routes =>
			{
				routes.MapRoute(
					name: "default",
					template: "{controller=Home}/{action=Index}/{id?}");

				routes.MapSpaFallbackRoute(
					name: "spa-fallback",
					defaults: new { controller = "Home", action = "Index" });
			});
			/* ------------javascript spa routes------------*/
		}
	}

#if DEBUG
	public class MyViewEngine : IViewLocationExpander
	{
		public void PopulateValues(ViewLocationExpanderContext context)
		{
			context.Values["customviewlocation"] = nameof(MyViewEngine);
		}

		public IEnumerable<string> ExpandViewLocations(
			 ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
		{
			return new[]
			{
			 "~/HistoryContest.Server/Views/{1}/{0}.cshtml",
			 "~/HistoryContest.Server/Views/Shared/{0}.cshtml"
			};
		}
	}
#endif
}
