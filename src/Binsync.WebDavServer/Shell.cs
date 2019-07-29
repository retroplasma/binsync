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
	public static class Shell
	{
		public static void Launch()
		{
			Task.Run(async () =>
			{
				await Task.Delay(100);
				var engine = Program.BinsyncEngine;
				var pubIdPrefix = $"[{engine.PublicHash.Substring(0, 8)}] $ ";

				//Console.TreatControlCAsInput = true;
				//Console.CancelKeyPress += delegate { System.Environment.Exit(0); };

				while (true)
				{
					try
					{
						Console.Write(pubIdPrefix);
						var cmd = Console.ReadLine();
						if (new string[] { "q", "exit", "quit", ":q", ":q!" }.Contains(cmd.ToLowerInvariant()))
						{
							System.Environment.Exit(0);
						}

						try
						{
							var cmd2 = cmd.Trim().ToLowerInvariant();
							if (new string[] { "help", "h" }.Contains(cmd2))
							{
								help();
							}
							/*else if (new string[] { "fetch" }.Contains(cmd2))
							{
								await fetch();
							}*/
							else if ("info" == cmd2)
							{
								Console.Error.WriteLine(" info not implemented.");
							}
							else if ("reset" == cmd2)
							{
								Console.Error.WriteLine(" reset not implemented yet. Please close the program and " +
								"delete the cache folder\n manually. All unflushed state will be lost.");
							}
							else if (new string[] { "write", "write 1", "write 0" }.Contains(cmd2))
							{
								await write(cmd2 == "write 1" ? (bool?)true : cmd2 == "write 0" ? (bool?)false : null);
							}
							else if (new string[] { "flush" }.Contains(cmd2))
							{
								await flush();
							}
							else if (cmd2 == "")
							{
								continue;
							}
							else if (new string[] { "clear", "cls" }.Contains(cmd2))
							{
								Console.Clear();
							}
							else
							{
								Console.Error.WriteLine($" unknown command: {cmd}");
							}
						}
						catch (Exception ex)
						{
							Console.Error.WriteLine(ex.ToString());
						}
					}
					catch
					{
						System.Environment.Exit(0);
					}
				}
			});
		}

		static void help()
		{
			var width = Console.WindowWidth;
			Console.WriteLine($@"
 write   Enables or disables writing. Only enable writing on one instance at the
         same time and remember to 'flush' before using another instance or
         deleting cached data. Some servers take time to propagate writes so
         it can happen that new data isn't available instantaneously.

          write     Shows if writing is enabled or not. (Disabled by default.)
          write 1   Enables writing.
          write 0   Disables writing.
         
         The reason this setting exists is because writing is ""append-only"",
         (no deletes or overwrites are supported at the moment) so uploads only
         work properly with some applications such as ""Cyberduck"", but not so
         well with ""Finder"" for example.

 flush   Flushes meta, parity and assurances. Makes all new uploaded data
         retrievable by storage code and password. You shouldn't flush after
         every write because it will make future retrieval slower. Only use it
         on one instance at the same time. If you want to switch to another
         instance you have to do it in this order or the data will be
         inconsistent:

          1) instance A: ..., write something, flush
          2) instance B: reset cache, write something, ...

         Make sure that all flushes from the other instance have been fetched
         before performing new writes. If they aren't: wait and retry later.		 

 info    Shows information about our state. (TODO)
 reset   Resets all cached state. (TODO)
 help    Prints this help message.
 clear   Clears the console screen.
 quit    Quits the application.
");
		}

		/* 
		// MAYBE: implement fetch
		static Task fetch()
		{
			//	fetch   Tries to find new data that has been uploaded. Make sure that this
			//			instance doesn't have new data flushed (or written) if another one
			//			did, as this will make the overall state inconsistent.
			throw new NotImplementedException("replace with reset (for now?)");
		}
		*/

		static bool writingEnabled = false;
		public static bool WritingEnabled { get { return writingEnabled; } }

		static Task write(bool? enable = null)
		{
			switch (enable)
			{
				case null:
					Console.WriteLine(" Writing is " + (writingEnabled ? "enabled" : "disabled") + ".");
					break;
				case true:
					if (writingEnabled)
					{
						Console.WriteLine(" Writing is already enabled.");
					}
					else
					{
						writingEnabled = true;
						Console.WriteLine(" Writing has been enabled. Good luck.");
					}
					break;
				case false:
					if (!writingEnabled)
					{
						Console.WriteLine(" Writing is already disabled.");
					}
					else
					{
						writingEnabled = false;
						Console.WriteLine(" Writing has been disabled.");
					}
					break;
			}
			return Task.CompletedTask;
		}

		static async Task flush()
		{
			await Program.BinsyncEngine.FlushMeta();
			Console.WriteLine(" Flushed meta.");
			await Program.BinsyncEngine.ForceFlushParity();
			Console.WriteLine(" Flushed parity.");
			await Program.BinsyncEngine.FlushAssurances();
			Console.WriteLine(" Flushed assurances.");
			//throw new NotImplementedException("assurance flush not implemented");
		}
	}
}