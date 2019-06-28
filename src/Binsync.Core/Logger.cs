using System;

namespace Binsync.Core
{
	public class Logger
	{
		readonly Action<string, object[]> logger;

		public Logger(Action<string, object[]> logger)
		{
			this.logger = logger;
		}

		public void Log(string str, params object[] objs)
		{
			logger(str, objs);
		}
	}
}

