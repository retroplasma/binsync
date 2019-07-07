using NUnit.Framework;
using System;
using Binsync.Core;
using Binsync.Core.Helpers;
using Binsync.Core.Formats;
using Binsync.Core.Caches;

namespace Tests
{
	public class FormatsTests
	{
		[Test]
		public static void TestMetaSegmentConverison()
		{
			var ms = new Binsync.Core.Formats.MetaSegment();

			var c1 = new Binsync.Core.Formats.MetaSegment.Command
			{
				CMD = Binsync.Core.Formats.MetaSegment.Command.CMDV.ADD,
				TYPE = Binsync.Core.Formats.MetaSegment.Command.TYPEV.FOLDER,
				FOLDER_ORIGIN = new Binsync.Core.Formats.MetaSegment.Command.FolderOrigin
				{
					Name = "a",
					FileSize = 1
				},
				FILE_ORIGIN = null,
			};
			ms.Commands.Add(c1);

			var c2 = new Binsync.Core.Formats.MetaSegment.Command
			{
				CMD = Binsync.Core.Formats.MetaSegment.Command.CMDV.ADD,
				TYPE = Binsync.Core.Formats.MetaSegment.Command.TYPEV.BLOCK,
				FILE_ORIGIN = new Binsync.Core.Formats.MetaSegment.Command.FileOrigin
				{
					Hash = new byte[] { 1 },
					Size = 2,
					Start = 3,
				},
			};
			ms.Commands.Add(c2);

			var msRe = Binsync.Core.Formats.MetaSegment.FromByteArray(ms.ToByteArray());
			var c1re = msRe.Commands[0].ToDBObject().ToProtoObject();
			var c2re = msRe.Commands[1].ToDBObject().ToProtoObject();

			Assert.AreEqual(Binsync.Core.Formats.MetaSegment.Command.CMDV.ADD, c1re.CMD);
			Assert.AreEqual(Binsync.Core.Formats.MetaSegment.Command.TYPEV.FOLDER, c1re.TYPE);
			Assert.AreEqual("a", c1re.FOLDER_ORIGIN.Name);
			Assert.AreEqual(1, c1re.FOLDER_ORIGIN.FileSize);
			Assert.AreEqual(null, c1re.FILE_ORIGIN);

			Assert.AreEqual(Binsync.Core.Formats.MetaSegment.Command.CMDV.ADD, c2re.CMD);
			Assert.AreEqual(Binsync.Core.Formats.MetaSegment.Command.TYPEV.BLOCK, c2re.TYPE);
			Assert.AreEqual(null, c2re.FOLDER_ORIGIN);
			Assert.AreEqual(1, c2re.FILE_ORIGIN.Hash.Length);
			Assert.AreEqual(1, c2re.FILE_ORIGIN.Hash[0]);
			Assert.AreEqual(2, c2re.FILE_ORIGIN.Size);
			Assert.AreEqual(3, c2re.FILE_ORIGIN.Start);
		}

	}
}