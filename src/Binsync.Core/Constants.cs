using System;
using System.Security.Cryptography;

namespace Binsync.Core
{
	public static partial class Constants
	{
		public static readonly RandomNumberGenerator RNG = RNGCryptoServiceProvider.Create();

		public static readonly string MetaVersion = "1.0";

		public const int SegmentSize = 524288; //segment size: raw chunk size, so maybe -1 because of compression for better last encrypted block if CBC?
		public const int DataBeforeParity = 100;
		public const int ParityCount = 20;
		//public const int ArticlesBeforeMeta = 20;
		//public static int MaxConnections = 16;
		//public const int ReplicationSearchBounds = 10;
		public const int AssuranceReplicationCount = 20;
		public const int ReplicationAttemptCount = 20;

		public static readonly Logger Logger = new Logger(Console.WriteLine);

		public const int CacheSize = 100000;
		public const int DiskCacheSize = 100000;
	}
}
