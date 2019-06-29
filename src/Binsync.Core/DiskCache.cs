using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

using Binsync.Core.Helpers;
using Binsync.Core.Formats;

namespace Binsync.Core.Caches
{
	public class RawCacheDiskDictionary : Dictionary<string, byte[]>
	{
		readonly DiskCache diskState;

		public RawCacheDiskDictionary(DiskCache diskState)
		{
			this.diskState = diskState;

			foreach (var x in diskState.EnumerateDiskRawCache())
				base.Add(x.Key, x.Value);
		}

		public new void Clear()
		{
			diskState.ClearDiskRawCache();
			base.Clear();
		}

		public new void Add(string hexHash, byte[] bytes)
		{
			base.Add(hexHash, bytes);
			diskState.AddToDiskRawCache(hexHash, bytes);
		}
	}

	public class DiskCache
	{
		readonly object diskCacheLock = new object();
		readonly object metaAggregateLock = new object();

		readonly string basePath;
		readonly string hash;

		string CacheDir1
		{
			get
			{
				return createDir(Path.Combine(basePath, "cache"));
			}
		}

		string CacheDir2
		{
			get
			{
				return createDir(Path.Combine(CacheDir1, hash));
			}
		}

		string RawCacheDir
		{
			get
			{
				return createDir(Path.Combine(CacheDir2, "raw"));
			}
		}

		string DiskCacheDir
		{
			get
			{
				return createDir(Path.Combine(CacheDir2, "diskcache"));
			}
		}

		string MetaAggregateDir
		{
			get
			{
				return createDir(Path.Combine(CacheDir2, "metaaggregate"));
			}
		}


		string NewAssuranceDir
		{
			get
			{
				return createDir(Path.Combine(CacheDir2, "newassurances"));
			}
		}

		string NewAssuranceSegmentsDir
		{
			get
			{
				return createDir(Path.Combine(NewAssuranceDir, "segments"));
			}
		}

		string NewAssuranceParityRelationsDir
		{
			get
			{
				return createDir(Path.Combine(NewAssuranceDir, "parityrelations"));
			}
		}

		string MainAssuranceDir
		{
			get
			{
				return createDir(Path.Combine(CacheDir2, "mainassurances"));
			}
		}

		public DiskCache(string basePath, string hash)
		{
			this.basePath = basePath;
			this.hash = hash;
		}

		static IEnumerable<T> refillSub<T>(string path, Func<byte[], T> deserializer) where T : ProtoBufSerializable
		{
			var count = finalFilesInPath(path).Count();

			for (int i = 0; i < count; i++)
			{
				var subPath = Path.Combine(path, i + ".final");
				var data = File.ReadAllBytes(subPath);
				var deserialized = deserializer(data);
				yield return deserialized;
			}
		}

		public IEnumerable<AssuranceSegment> EnumerateMainAssurances()
		{
			return refillSub(MainAssuranceDir, General.Deserialize<AssuranceSegment>);
		}

		public void AddNewMainAssurance(AssuranceSegment ass)
		{
			saveAssuranceSub(MainAssuranceDir, ass);
		}

		public void AddNewMainAssuranceSerialized(byte[] ass)
		{
			subSaveInPath(MainAssuranceDir, ass);
		}


		public IEnumerable<AssuranceSegment.ParityRelation> EnumerateNewParityRelations()
		{
			return refillSub(NewAssuranceParityRelationsDir, General.Deserialize<AssuranceSegment.ParityRelation>);
		}

		public IEnumerable<AssuranceSegment.Segment> EnumerateNewAssuranceSegments()
		{
			return refillSub(NewAssuranceSegmentsDir, General.Deserialize<AssuranceSegment.Segment>);
		}

		public void AddNewParityRelation(AssuranceSegment.ParityRelation rel)
		{
			saveAssuranceSub(NewAssuranceParityRelationsDir, rel);
		}

		public void AddNewAssuranceSegment(AssuranceSegment.Segment seg)
		{
			saveAssuranceSub(NewAssuranceSegmentsDir, seg);
		}

		public void ClearNewAssurances()
		{
			safeDelete(NewAssuranceDir);
		}

		static void saveAssuranceSub<T>(string path, T seg) where T : ProtoBufSerializable
		{
			var serialized = seg.SerializeContract();
			subSaveInPath(path, serialized);
		}

		static void subSaveInPath(string path, byte[] data)
		{
			var count = finalFilesInPath(path).Count();
			var nextName = count.ToString();
			safeSave(Path.Combine(path, nextName), data);
		}

		public IEnumerable<KeyValuePair<string, byte[]>> EnumerateDiskRawCache()
		{
			foreach (var x in finalFilesInPath(RawCacheDir))
			{
				var data = File.ReadAllBytes(x);
				var name = Path.GetFileNameWithoutExtension(x);

				yield return new KeyValuePair<string, byte[]>(name, data);
			}
		}

		public void ClearDiskRawCache()
		{
			safeDelete(RawCacheDir);
		}

		public void AddToDiskRawCache(string hexHash, byte[] bytes)
		{
			safeSave(Path.Combine(RawCacheDir, hexHash), bytes);
		}

		public void DoDiskCache(byte[] indexId, byte[] data)
		{
			var path = Path.Combine(DiskCacheDir, indexId.ToHexString());
			lock (diskCacheLock)
			{
				if (!File.Exists(path + ".final"))
				{
					safeSave(path, data);

					if (finalFilesInPath(DiskCacheDir).Count() >= Constants.DiskCacheSize)
					{
						var oldest = finalFilesInPath(DiskCacheDir).Select(f => new { File = f, Time = File.GetCreationTimeUtc(f).Ticks })
							.Aggregate(new { File = "", Time = long.MaxValue }, (acc, val) => acc.Time < val.Time ? acc : val).File;

						safeDeleteFile(oldest);
					}
				}
			}
		}

		// flush disk cache?

		public byte[] GetFromDiskCache(byte[] indexId)
		{
			var path = Path.Combine(DiskCacheDir, indexId.ToHexString() + ".final");
			lock (diskCacheLock)
			{
				if (File.Exists(path))
				{
					return File.ReadAllBytes(path);
				}
				else
				{
					return null;
				}
			}
		}

		public void CacheMetaPart(MetaSegment seg, string path, uint index)
		{
			var mthash = new MetaExtraIndexFormat { MetaType = MetaExtraIndexFormat.TYPE.FOLDER, LocalPath = path }.ToByteArray().SHA1().ToHexString();
			var mtdir = Path.Combine(MetaAggregateDir, mthash + ".mt");
			var indexfile = Path.Combine(mtdir, index.ToString());

			lock (metaAggregateLock)
			{
				createDir(mtdir);

				var pathInfo = Path.Combine(mtdir, "__path");
				if (!File.Exists(pathInfo))
				{
					File.WriteAllText(pathInfo + ".tmp", path);
					File.Move(pathInfo + ".tmp", pathInfo);
				}

				safeSave(indexfile, seg.ToByteArray());
			}
		}

		public void ClearMetaCacheParts(string mthash)
		{
			var mtdir = Path.Combine(MetaAggregateDir, mthash + ".mt");

			lock (metaAggregateLock)
				safeDelete(mtdir);
		}

		public string GetMtHashFromPath(string path)
		{
			return new MetaExtraIndexFormat { MetaType = MetaExtraIndexFormat.TYPE.FOLDER, LocalPath = path }.ToByteArray().SHA1().ToHexString();
		}

		public string GetPathFromMtHash(string mthash)
		{
			var mtdir = Path.Combine(MetaAggregateDir, mthash + ".mt");

			lock (metaAggregateLock)
			{
				var pathInfo = Path.Combine(mtdir, "__path");
				if (File.Exists(pathInfo))
				{
					return File.ReadAllText(pathInfo);
				}
				else
				{
					return null;
				}
			}
		}

		public bool MtHashExists(string mthash)
		{
			var mtdir = Path.Combine(MetaAggregateDir, mthash + ".mt");
			lock (metaAggregateLock)
			{
				return Directory.Exists(mtdir);
			}
		}

		public IEnumerable<string> GetEnumerableMTHashes()
		{
			lock (metaAggregateLock) // useless?
			{
				return Directory.EnumerateDirectories(MetaAggregateDir).Where(s => s.EndsWith(".mt")).Select(x => x.Substring(0, x.Length - 3));
			}
		}

		public int GetMetaAggregateCount(string mthash)
		{
			var mtdir = Path.Combine(MetaAggregateDir, mthash + ".mt");

			lock (metaAggregateLock)
			{
				if (Directory.Exists(mtdir))
				{
					createDir(mtdir);

					return finalFilesInPath(mtdir).Count(f => !f.StartsWith("__"));
				}
				else
				{
					return 0;
				}
			}
			//TODO: clear, flush etc
		}

		public MetaSegment AggregateMeta(string mthash)
		{
			var mtdir = Path.Combine(MetaAggregateDir, mthash + ".mt");

			lock (metaAggregateLock)
			{
				if (Directory.Exists(mtdir))
				{
					createDir(mtdir);

					var aggregation = new MetaSegment();

					var sorted = new SortedList<uint, MetaSegment>();

					foreach (var f in finalFilesInPath(mtdir).Where(f => !f.StartsWith("__")))
					{
						var data = File.ReadAllBytes(f);
						var meta = MetaSegment.FromByteArray(data);
						sorted.Add(uint.Parse(Path.GetFileNameWithoutExtension(f)), meta);
					}
					foreach (var m in sorted)
					{
						aggregation.Commands.AddRange(m.Value.Commands);
					}

					return aggregation;
				}
				else
				{
					return null;
				}
			}
			//TODO: clear, flush etc
		}

		static IEnumerable<string> finalFilesInPath(string path)
		{
			return Directory.EnumerateFiles(path).Where(s => s.EndsWith(".final"));
		}

		// TODO: avoid IO on getter
		static string createDir(string dir)
		{
			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir);
			return dir;
		}

		static void safeDelete(string path)
		{
			var tmp = path + ".tmp";
			Directory.Move(path, tmp);
			Directory.Delete(tmp, true);
		}

		static void safeDeleteFile(string path)
		{
			var tmp = path + ".tmp";
			File.Move(path, tmp);
			File.Delete(tmp);
		}

		static void safeSave(string path, byte[] data)
		{
			var final = path + ".final";
			var tmp = path + ".tmp";
			File.WriteAllBytes(tmp, data);
			File.Move(tmp, final);
		}
	}
}

