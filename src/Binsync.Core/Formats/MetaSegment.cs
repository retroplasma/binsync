using System;
using ProtoBuf;
using System.Collections.Generic;
using System.Linq;

namespace Binsync.Core.Formats
{
	//index = age, replication = replication, extraData = [type= "folder/file", path = "/"]

	[ProtoContract]
	public class MetaExtraIndexFormat
	{

		public enum TYPE
		{
			FOLDER = 0,
			FILE = 1
		}


		[ProtoMember(1)]
		public TYPE MetaType { get; set; }

		[ProtoMember(2)]
		public string LocalPath { get; set; }

		public byte[] ToByteArray()
		{
			using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
			{
				Serializer.Serialize(ms, this);
				return ms.ToArray();
			}
		}

		public static MetaExtraIndexFormat FromByteArray(byte[] data)
		{
			using (System.IO.MemoryStream ms = new System.IO.MemoryStream(data))
			{
				return Serializer.Deserialize<MetaExtraIndexFormat>(ms);
			}
		}
	}

	[ProtoContract]
	public class MetaSegment
	{

		[ProtoMember(1)]
		public List<Command> Commands = new List<Command>();

		[ProtoContract]
		public class Command
		{

			public enum CMDV
			{
				//KEEP = 0,  // LATER
				//FLUSH = 1, // LATER
				ADD = 2,
				//DELETE = 3 // LATER
				//, MODIFY = 4
			}

			public enum TYPEV
			{
				FOLDER = 0,
				FILE = 1,
				BLOCK = 2
			}

			[ProtoMember(1)]
			public CMDV CMD { get; set; }

			[ProtoMember(2)]
			public TYPEV TYPE { get; set; }

			[ProtoMember(3)]
			public FolderOrigin FOLDER_ORIGIN { get; set; }

			[ProtoMember(4)]
			public FileOrigin FILE_ORIGIN { get; set; }


			[ProtoContract]
			public class FolderOrigin
			{
				[ProtoMember(1)]
				public string Name { get; set; } // file or folder name

				[ProtoMember(2)]
				public long FileSize { get; set; } // file size if file in folder
			}

			[ProtoContract]
			public class FileOrigin
			{
				[ProtoMember(1)]
				public byte[] Hash { get; set; } // hash of block

				[ProtoMember(2)]
				public long Start { get; set; } // position of block in file

				[ProtoMember(3)]
				public uint Size { get; set; } // size of block
			}
		}


		public byte[] ToByteArray()
		{
			using (var ms = new System.IO.MemoryStream())
			{
				Serializer.Serialize(ms, this);
				return ms.ToArray();
			}
		}

		public static MetaSegment FromByteArray(byte[] data)
		{
			using (var ms = new System.IO.MemoryStream(data))
			{
				return Serializer.Deserialize<MetaSegment>(ms);
			}
		}

		public List<byte[]> ToListOfByteArrays()
		{
			return ToListOfByteArrays(Constants.SegmentSize);
		}

		public List<byte[]> ToListOfByteArrays(int customSize)
		{
			var list = new List<byte[]>();
			fillListWithMetas(this, list, customSize);
			return list;
		}

		// TODO: fill list with tuples (compressed array and plain hash). compress here


		// optimize
		static void fillListWithMetas(MetaSegment seg, List<byte[]> list, int customSize)
		{

			byte[] saved = seg.ToByteArray();

			if (saved.Length > customSize)
			{
				var segL = new MetaSegment();
				var segR = new MetaSegment();

				foreach (var element in seg.Commands.Select((obj, inc) => new { obj, inc }))
					(element.inc < seg.Commands.Count / 2 ? segL : segR).Commands.Add(element.obj);

				fillListWithMetas(segL, list, customSize);
				fillListWithMetas(segR, list, customSize);
			}
			else
			{
				list.Add(saved);
			}
		}

		//public IEnumerable<string> Print(){
		//	//yield return String.Format ("{0}", this.SuperCommand);
		//
		//	foreach (MetaSegment.Command command in this.Commands) {
		//
		//		if (command.CMD == Command.CMDV.KEEP || command.CMD == Command.CMDV.FLUSH) {
		//			yield return String.Format ("{0}", command.CMD);
		//		} else {
		//
		//			switch (command.TYPE) {
		//			case MetaSegment.Command.TYPEV.FILE:
		//			case MetaSegment.Command.TYPEV.FOLDER:
		//				yield return String.Format ("{0} {1} \"{2}\"", command.CMD, command.TYPE, command.FOLDER_ORIGIN.Name);
		//				break;
		//			case MetaSegment.Command.TYPEV.BLOCK:
		//				switch (command.CMD) {
		//				case MetaSegment.Command.CMDV.ADD:
		//					yield return String.Format ("{0} {1} {2} {3} {4}", command.CMD, command.TYPE, Helpers.Encoding.ByteArrayToHexString (command.FILE_ORIGIN.Hash), command.FILE_ORIGIN.Start, command.FILE_ORIGIN.Size);
		//					break;
		//				case MetaSegment.Command.CMDV.DELETE:
		//					yield return String.Format ("{0} {1} {2} {3}", command.CMD, command.TYPE, command.FILE_ORIGIN.Start, command.FILE_ORIGIN.Size);
		//					break;
		//
		//				}
		//				break;
		//			}
		//		}
		//	}
		//}
	}
}

