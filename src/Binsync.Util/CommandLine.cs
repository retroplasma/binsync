using System;
using Binsync.Core;
using System.IO;

namespace Binsync.Util
{
	public class CommandLine
	{
		public static Config ConfigForMain(string[] args)
		{
			if (args.Length != 1)
			{
				Console.Error.WriteLine("Please specify path to config JSON file as argument.");
				Console.Error.WriteLine("Example config.json:");
				Console.Error.WriteLine(Binsync.Util.Config.ExampleJSON(storageCodeText: "GENERATE USING COMMAND LINE ARGUMENT 'gen'"));
				return null;
			}

			if (args[0].ToLowerInvariant() == "gen")
			{
				Console.WriteLine("Storage Code: " + Engine.Credentials.GenerateStorageCode());
				return null;
			}

			return new Binsync.Util.Config(jsonFilePath: args[0]);
		}
	}
}
