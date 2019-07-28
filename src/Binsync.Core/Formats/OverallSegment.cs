using System;
using ProtoBuf;
using System.Collections.Generic;
using System.Linq;

namespace Binsync.Core.Formats
{
	[ProtoContract]
	public class OverallSegment
	{
		[ProtoMember(1)]
		public byte[] Data { get; set; }

		[ProtoMember(2)]
		public byte[] Padding { get; set; }

		public void AddPadding()
		{
			if (null == Data) throw new Exception("data not set");
			const int min = 16000;
			var extra = Binsync.Core.Helpers.Cryptography.GetRandomInt(0, 100);
			var padLength = Math.Max(0, (min + extra) - Data.Length);
			Padding = Binsync.Core.Helpers.General.GetWeakPseudoRandomBytes(padLength);
		}

		public byte[] ToByteArray()
		{
			using (var ms = new System.IO.MemoryStream())
			{
				Serializer.Serialize(ms, this);
				return ms.ToArray();
			}
		}

		public static OverallSegment FromByteArray(byte[] data)
		{
			using (var ms = new System.IO.MemoryStream(data))
			{
				return Serializer.Deserialize<OverallSegment>(ms);
			}
		}
	}
}

