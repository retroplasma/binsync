using System;
using Microsoft.Extensions.Configuration;
using Binsync.Core;
using Binsync.Core.Services;
using Binsync.Core.Helpers;

namespace Binsync.Util
{
	public class Config
	{
		string jsonFilePath;

		public Config(string jsonFilePath)
		{
			this.jsonFilePath = jsonFilePath;
		}

		public Engine.Credentials GetCredentials()
		{
			var config = new ConfigurationBuilder()
							 .AddJsonFile(jsonFilePath)
							 .Build();
			var c = config.GetSection("credentials");

			var storageCode = ((Func<string, string>)(str =>
			{
				try
				{
					if (str == "" || str == null || str.Length != 64 || str.FromHexToBytes().ToHexString() != str)
						throw new Exception();
				}
				catch
				{
					throw new ArgumentException("Invalid storageCode");
				}
				return str;
			}))(c["storageCode"]);
			var password = ((Func<string, string>)(str =>
			{
				if (str == "" || str == null) throw new ArgumentException("No password specified");
				return str;
			}))(c["password"]);

			return new Engine.Credentials { StorageCode = storageCode, Password = password };
		}
	}
}
