//#define LAG
//#define DONT_REALLY_SAVE
//#define THROTTLE_DOWN_SPEED

using System;
using System.Collections.Generic;
using ProtoBuf;
using System.IO;
using Binsync.Core.Helpers;

namespace Binsync.Core.Services
{
	public class TestDummy : IService
	{
		class StorageDummy
		{
			public StorageDummy(string storagePath)
			{
				this.storagePath = storagePath;
			}

			readonly string storagePath;

			static int lag()
			{
				return 20 + (General.GetWeakPseudoRandomBytes(1)[0] / 10);
			}

			//static void throttleDownSpeed(long size)
			//{
			//	// ((8.5 * 1024* 1024 * 1024 )/ 106/60) / ((11 * 1024 * 1024 )/ 8)
			//
			//	// speed is shit simulation:
			//
			//	float mbit = 40/Constants.MaxConnections;
			//	float speed = (mbit * 1024 * 1024) / 8;
			//	float seconds = size / speed;
			//	float milliseconds = seconds * 1000;
			//	System.Threading.Thread.Sleep ((int)milliseconds);
			//}

			public void Store(string id, byte[] data)
			{
#if LAG
				System.Threading.Thread.Sleep (lag());
#endif
				string filePath = Path.Combine(storagePath, id);

				lock (filePath)
				{
					if (File.Exists(filePath))
						throw new Exception("Article exists already");
#if !DONT_REALLY_SAVE

					File.WriteAllBytes(filePath, data);
#endif
				}
			}

			public byte[] Retrieve(string id)
			{
#if LAG
				System.Threading.Thread.Sleep (lag());
#endif
				string filePath = Path.Combine(storagePath, id);

#if THROTTLE_DOWN_SPEED
				throttleDownSpeed(new FileInfo(filePath).Length);
#endif

				return File.ReadAllBytes(filePath);
			}

			[ProtoContract]
			public class Format
			{
				[ProtoMember(1)]
				public byte[] Subject;

				[ProtoMember(2)]
				public byte[] Data;
			}
		}

		readonly StorageDummy storageDummy;

		public TestDummy(string storagePath)
		{
			storageDummy = new StorageDummy(storagePath);
		}

		public bool Connected
		{
			get
			{
				return true;
			}
		}

		public bool Connect()
		{
			return true;
		}

		public byte[] GetSubject(string id)
		{
			var data = storageDummy.Retrieve(id);
			StorageDummy.Format retrievedFormat;

			using (MemoryStream ms = new MemoryStream(data))
			{
				retrievedFormat = Serializer.Deserialize<StorageDummy.Format>(ms);
			}

			return retrievedFormat.Subject;
		}

		public byte[] GetBody(string id)
		{
			var data = storageDummy.Retrieve(id);
			StorageDummy.Format retrievedFormat;

			using (MemoryStream ms = new MemoryStream(data))
			{
				retrievedFormat = Serializer.Deserialize<StorageDummy.Format>(ms);
			}

			return retrievedFormat.Data;
		}


		public bool Upload(Chunk chunk)
		{
			var id = chunk.ID;
			var subject = chunk.Subject.GetBytesUTF8();
			var data = chunk.Data;

			byte[] finalData;

			var finalFormat = new StorageDummy.Format
			{
				Subject = subject,
				Data = data
			};

			using (var ms = new MemoryStream(subject.Length + data.Length))
			{
				Serializer.Serialize(ms, finalFormat);
				finalData = ms.ToArray();
			}

			storageDummy.Store(id, finalData);

			return true;
		}
	}
}

