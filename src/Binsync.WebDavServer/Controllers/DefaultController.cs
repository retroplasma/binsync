using FubarDev.WebDavServer;
using FubarDev.WebDavServer.AspNetCore;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Linq;
using System;
using System.Collections.Generic;
using Binsync.Core;
using Binsync.Core.Formats;

namespace Binsync.WebDavServer
{
	[Route("/")]
	public class RedirectController : Controller
	{
		public IActionResult Index() => Redirect(DefaultController.Prefix);
	}

	[Route(DefaultController.Prefix + "/{*path}")]
	[Authorize]
	public class DefaultController : Controller
	{
		public const string Prefix = "data";

		public async Task<IActionResult> Index()
		{
			var engine = Program.BinsyncEngine;

			var route = getNormalizedRoute();
			var path = getBinsyncPathFromNormalizedRoute(route);

			var m = await engine.DownloadMetaForPath(path);
			if (m == null)
			{
				// handle empty dir & file
				var prev = path.Split('/').SkipLast(1).Aggregate("", (a, b) => a + "/" + b).Trim('/');
				var cur = path.Split('/').Last().Trim('/');
				if (cur == "" && prev == "")
				{
					m = new Engine.Meta { IsFile = false, Path = "", Commands = new List<MetaSegment.Command>() };
				}
				else
				{
					var p = await engine.DownloadMetaForPath(prev);
					if (p == null)
						return NotFound();
					MetaSegment.Command cmd;
					if ((cmd = p.Commands?.Where(c => c.FOLDER_ORIGIN?.Name == cur).Take(1).FirstOrDefault()) != null)
						m = new Engine.Meta { IsFile = cmd.TYPE == MetaSegment.Command.TYPEV.FILE, Path = path, Commands = new List<MetaSegment.Command>() };
					else
						return NotFound();
				}
			}
			if (m.IsFile) return Redirect(escape($"/_dav/{m.Path}"));

			var entries = new List<entry>();

			if (path != "")
			{
				entries.Add(new entry
				{
					href = escape(route.Split('/').SkipLast(1).Aggregate("", (a, b) => a + "/" + b).Substring(1)),
					label = "..",
					@class = "back",
					title = "Back",
				});
			}

			entries.AddRange(m.Commands.Select(c => new { c, f = c.TYPE == MetaSegment.Command.TYPEV.FILE }).Select(x => new entry
			{
				href = escape(x.f
					   ? $"/_dav/{m.Path}/{x.c.FOLDER_ORIGIN?.Name}".Replace("//", "/")
					   : $"{route}/{x.c.FOLDER_ORIGIN?.Name}"),
				label = x.c.FOLDER_ORIGIN?.Name,
				@class = x.f ? "file" : "folder",
				title = x.f ? $"File: {x.c.FOLDER_ORIGIN?.FileSize} B" : "Folder",
			}));


			var pubHash = engine.PublicHash.Substring(0, 8);
			var text = $"<font color=\"gray\">[{HttpUtility.HtmlEncode(pubHash)}]</font> /" + HttpUtility.HtmlEncode(m.Path);

			var elems = entries
				.Select(e => $"<a href=\"{e.href}\"><li class=\"{e.@class}\" title=\"{e.@title}\">{HttpUtility.HtmlEncode(e.label)}</li></a>")
				.ToList();

			if (path == "" && entries.Count == 0 || elems.Count == 1)
			{
				elems.Add("<li class=\"empty\">This folder is empty.</li>");
			}

			var webDavUrl = $"{Request.Scheme}://{Request.Host}" + escape($"/_dav/{m.Path}".TrimEnd('/'));
			var title = HttpUtility.HtmlEncode($"binsync [{pubHash}]");

			Response.ContentType = "text/html; charset=UTF-8";
			return Content($@"
<!DOCTYPE html>
<html>
	<head>
		<meta charset=""UTF-8"">
		<title>{title}</title>
		<style>
			a:link {{
				text-decoration: none;
			}}
			a:hover {{
				text-decoration: underline;
			}}
			li {{
				list-style: none;
			}}
			.empty {{
				color: gray;
			}}
			li::before {{
				display:inline-block;
			}}
			.empty::before {{ content: ""ℹ️ ""; }}
			.back::before {{ content: ""⬅️ ""; }}
			.folder::before {{ content: ""📁 ""; }}
			.file::before {{ content: ""📄 ""; }}
		</style>
	</head>
	<body>
		<a title=""Home"" href=""/"">
			<img height=""32px"" alt=""binsync logo"" src=""/images/header_logo.png"">
		</a>
		<hr>
		<h3>{text}</h3>
		<ul>
			{elems.Aggregate("", (a, b) => $"{a}\n\t\t\t{b}").Trim('\n').TrimStart('\t')}
		</ul>
		<hr>
		WebDAV: {webDavUrl}
	</body>
</html>
			");

			// todo: learn some asp net
		}

		public class entry
		{
			public string href;
			public string label;
			public string @class;
			public string title;
		}

		string escape(string str)
		{
			Func<string, string> escape = x =>
			{
				var s = System.Uri.EscapeDataString($"a{x}a");
				return s.Substring(0, s.Length - 1).Substring(1);
			};

			var split = str.Split('/').Select(escape).ToArray();
			return split.Aggregate("", (a, b) => a + "/" + b).Substring(1);
		}

		string getNormalizedRoute()
		{
			var route = Request.Path.Value;
			if (route.Length > 0 && route.Substring(route.Length - 1, 1) == "/")
				route = route.Substring(0, route.Length - 1);
			return route;
		}

		string getBinsyncPathFromNormalizedRoute(string normalizedRoute)
		{
			return normalizedRoute.Substring(Math.Min(normalizedRoute.Length, Prefix.Length + 2));
		}
	}
}
