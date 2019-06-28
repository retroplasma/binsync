using System;
using ProtoBuf;
using System.Collections.Generic;
using System.Linq;

namespace Binsync.Core.Formats
{
	public abstract class ProtoBufSerializable
	{

	}

	/// <summary>
	/// Assurance Segment, contains index IDs of uploaded segments and new parity relations. Should not get too big. (Test against Constants.SegmentSize)
	/// </summary>
	[ProtoContract]
	public class AssuranceSegment : ProtoBufSerializable
	{
		// segment length, meta verification

		// parity on compressed

		[ProtoMember(1)]
		public List<Segment> Segments = new List<Segment>(); // raw, parity, meta

		[ProtoMember(2)]
		public List<ParityRelation> ParityRelations = new List<ParityRelation>();


		[ProtoContract]
		public class Segment : ProtoBufSerializable
		{
			[ProtoMember(1)]
			public byte[] IndexID { get; set; }

			[ProtoMember(2)]
			public UInt32 Replication { get; set; } // Derive final locator with method in Generators2

			[ProtoMember(3)]
			public byte[] PlainHash { get; set; }

			[ProtoMember(4)]
			public uint CompressedLength { get; set; } //needed for parity relation, which is also stored in AssuranceSegment

			/*[ProtoMember(5)]
			public ENCRYPTION_TYPE EncryptionType { get; set;}

			public enum ENCRYPTION_TYPE
			{
				NOT_SPECIFIED = 0,
				AES_CBC = 1,
				AES_GCM = 2
			}*/
		}

		[ProtoContract]
		public class ParityRelation : ProtoBufSerializable
		{
			[ProtoMember(1)]
			public byte[][] DataPlainHashes { get; set; } //meta, raw

			[ProtoMember(2)]
			public byte[][] ParityPlainHashes { get; set; }
		}


		public byte[] ToByteArray()
		{
			//compress here?

			using (var ms = new System.IO.MemoryStream())
			{
				Serializer.Serialize(ms, this);
				return ms.ToArray();
			}
		}

		public static AssuranceSegment FromByteArray(byte[] data)
		{
			using (var ms = new System.IO.MemoryStream(data))
			{
				return Serializer.Deserialize<AssuranceSegment>(ms);
			}
		}

		public List<byte[]> ToListOfByteArrays()
		{
			var list = new List<byte[]>();
			fillListWithAssurances(this, list);
			return list;
		}

		// optimize
		static void fillListWithAssurances(AssuranceSegment seg, List<byte[]> list)
		{
			byte[] saved = seg.ToByteArray();

			if (saved.Length > Constants.SegmentSize)
			{
				var segL = new AssuranceSegment();
				var segR = new AssuranceSegment();

				System.Threading.Tasks.Parallel.Invoke(
					delegate
					{
						foreach (var element in seg.Segments.Select((obj, inc) => new { obj, inc }))
							(element.inc < seg.Segments.Count / 2 ? segL : segR).Segments.Add(element.obj);
					},
					delegate
					{
						foreach (var element in seg.ParityRelations.Select((obj, inc) => new { obj, inc }))
							(element.inc < seg.ParityRelations.Count / 2 ? segL : segR).ParityRelations.Add(element.obj);
					});

				fillListWithAssurances(segL, list);
				fillListWithAssurances(segR, list);
			}
			else
			{
				list.Add(saved);
			}
		}
	}



}

