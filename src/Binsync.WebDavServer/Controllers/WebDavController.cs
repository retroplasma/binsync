using FubarDev.WebDavServer;
using FubarDev.WebDavServer.AspNetCore;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace Binsync.WebDavServer
{
	[Route("_dav/{*path}")]
	[Authorize]
	public class WebDavController : WebDavControllerBase
	{
		public WebDavController(IWebDavContext context, IWebDavDispatcher dispatcher, ILogger<WebDavIndirectResult> responseLogger = null)
		   : base(context, dispatcher, responseLogger)
		{
		}
	}
}
