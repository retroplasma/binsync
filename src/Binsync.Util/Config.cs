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

		public class Misc
		{
			public int TotalConnections;
			public int UploadConnections;
			public string CachePath;
		}

		public Misc GetMisc()
		{
			var config = new ConfigurationBuilder()
							 .AddJsonFile(jsonFilePath)
							 .Build();
			var m = config.GetSection("misc");
			var totalConnections = ((Func<string, ushort>)(str =>
			{
				var c = ushort.Parse(str, System.Globalization.NumberStyles.Integer);
				if (c == 0) throw new ArgumentException("Invalid total connections: must be > 0");
				return c;
			}))(m["totalConnections"]);
			var uploadConnections = ((Func<string, ushort>)(str =>
			{
				var c = ushort.Parse(str, System.Globalization.NumberStyles.Integer);
				if (c == 0) throw new ArgumentException("Invalid upload connections: must be > 0");
				if (c > totalConnections) throw new ArgumentException("Invalid upload connections: must be <= total connections");
				return c;
			}))(m["uploadConnections"]);
			var cachePath = ((Func<string, string>)(str =>
			{
				if (str == "" || str == null) throw new ArgumentException("No cache path specified");
				return str;
			}))(m["cachePath"]);
			return new Misc { TotalConnections = totalConnections, UploadConnections = uploadConnections, CachePath = cachePath };
		}

		public Engine ToEngine()
		{
			var config = this;
			var fac = config.GetServiceFactory();
			var creds = config.GetCredentials();
			var misc = config.GetMisc();
			return new Engine(creds, misc.CachePath, fac, misc.TotalConnections, misc.UploadConnections);
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

		public IServiceFactory GetServiceFactory()
		{
			var config = new ConfigurationBuilder()
				.AddJsonFile(jsonFilePath)
				.Build();


			var svc = config["service"];
			switch (svc)
			{
				case "usenet":
					var u = config.GetSection("usenet");

					var username = u["username"];
					var password = u["password"];
					var serverAddress = u["serverAddress"];
					var useSSL = ((Func<string, bool>)(str =>
					{
						if (str == "True") return true;
						if (str == "False") return false;
						throw new ArgumentException("Invalid bool");
					}))(u["useSSL"]);
					var port = ((Func<string, ushort>)(str =>
						{
							var p = ushort.Parse(str, System.Globalization.NumberStyles.Integer);
							if (p == 0) throw new ArgumentException("Invalid port: 0");
							return p;
						}))(u["port"]);
					var newsgroup = u["newsgroup"];
					var postFromUser = u["postFromUser"];

					return new DelegateFactory<Usenet>(() => new Usenet(
						username, password, serverAddress, useSSL, port, newsgroup, postFromUser
					));
				case "testStorage":
					var t = config.GetSection("testStorage");
					var path = t["storagePath"];
					return new DelegateFactory<TestDummy>(() => new TestDummy(
						path
					));
				default:
					throw new ArgumentException($"Invalid service '{svc}'");
			}
		}

		public static string ExampleJSON(string storageCodeText)
		{
			return @"{
	""credentials"": {
		""storageCode"": """ + storageCodeText + @""",
		""password"": ""hunter2""
	},
	""service"": ""usenet"",
	""usenet"": {
		""username"": ""foo"",
		""password"": ""bar"",
		""serverAddress"": ""usenet.example.com"",
		""useSSL"": true,
		""port"": 443,
		""newsgroup"": ""alt.binaries.boneless"",
		""postFromUser"": ""max.power@example.com (Max Power)""
	},	
	""misc"": {
		""totalConnections"": 10,
		""uploadConnections"": 3,
		""cachePath"": """ + (System.IO.Path.PathSeparator == '\\' ? "C:\\some\\absolute\\non\\volatile\\path" : "/some/absolute/non/volatile/path") + @"""
	}
}";
		}

	}
}
