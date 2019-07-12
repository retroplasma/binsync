//#define USE_ALTERNATIE_RAW_CACHE

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using SQLite;
using Binsync.Core.Helpers;

namespace Binsync.Core.Caches
{
	public class DB
	{
		SQLiteConnection con;
		string cachePath;

		public DB(string cachePath)
		{
			Directory.CreateDirectory(cachePath);
			con = new SQLiteConnection(Path.Combine(cachePath, "db.sqlite"));
			con.CreateTable<SQLMap.Segment>();
			con.CreateTable<SQLMap.ParityRelation>();
			con.CreateTable<SQLMap.Command>();

			this.cachePath = cachePath;
		}

		public class SQLMap
		{
			public class Segment
			{
				[PrimaryKey, AutoIncrement]
				public int Id { get; set; }
				public bool IsNew { get; set; }

				/* proto */
				[Indexed]
				public byte[] IndexID { get; set; }
				public UInt32 Replication { get; set; }
				[Indexed]
				public byte[] PlainHash { get; set; }
				public uint CompressedLength { get; set; }
			}

			public class ParityRelation
			{
				[PrimaryKey, AutoIncrement]
				public int Id { get; set; }
				public bool IsNew { get; set; }
				public ParityRelationState State { get; set; }
				//[Ignore]
				public byte[] TmpDataCompressed { get; set; }

				/* proto? */
				[Indexed]
				public long CollectionID { get; set; }
				public long ElementID { get; set; }

				/* proto */
				[Indexed]
				public byte[] PlainHash { get; set; }
				public bool IsParityElement { get; set; }
			}

			public enum ParityRelationState
			{
				FillingUp, Processing, Done
			}

			public enum CommandMetaType
			{
				File, Folder
			}

			public class Command
			{
				[PrimaryKey, AutoIncrement]
				public int Id { get; set; }
				public bool IsNew { get; set; }
				[Indexed]
				public string Path { get; set; }
				public int Index { get; set; }
				public CommandMetaType MetaType { get; set; }


				/* proto */

				public enum CMDV { ADD = 2 }
				public enum TYPEV { FOLDER = 0, FILE = 1, BLOCK = 2 }

				public CMDV CMD { get; set; }
				public TYPEV TYPE { get; set; }

				public string FolderOrigin_Name { get; set; } // file or folder name
				public long FolderOrigin_FileSize { get; set; } // file size if file in folder

				public byte[] FileOrigin_Hash { get; set; } // hash of block
				public long FileOrigin_Start { get; set; } // position of block in file
				public uint FileOrigin_Size { get; set; } // size of block
			}
		}

		public void AddNewAssurance(byte[] indexId, uint rep, byte[] plainHash, uint compressedSize, Action _additionalActionForTransaction = null)
		{
			var segment = new SQLMap.Segment
			{
				IsNew = true,
				IndexID = indexId,
				Replication = rep,
				PlainHash = plainHash,
				CompressedLength = compressedSize,
			};
			lock (con)
			{
				if (_additionalActionForTransaction == null)
				{
					con.Insert(segment);
				}
				else
				{
					con.RunInTransaction(() =>
					{
						con.Insert(segment);
						_additionalActionForTransaction();
					});
				}
			}
		}

		public System.Collections.Specialized.OrderedDictionary GetProcessingParityRelations()
		{
			var dir = Path.Combine(cachePath, "tmpDataCompressed");

			var d = new System.Collections.Specialized.OrderedDictionary();
			List<SQLMap.ParityRelation> x;
			lock (con)
			{
				x = con.Query<SQLMap.ParityRelation>("select * from parityrelation where state = ? and (not IsParityElement) order by collectionId, elementId", SQLMap.ParityRelationState.Processing);
			}
			foreach (var e in x)
			{
				var v = d[e.CollectionID] as List<SQLMap.ParityRelation>;
				if (v == null)
				{
					v = new List<SQLMap.ParityRelation>();
					d.Add(e.CollectionID, v);
				}
#if USE_ALTERNATIE_RAW_CACHE
				e.TmpDataCompressed = File.ReadAllBytes(Path.Combine(dir, e.CollectionID.ToString(), e.ElementID.ToString()));
#endif
				v.Add(e);
			}
			return d;
		}

		public void CloseParityRelations(long collectionId, int inputLength, byte[][] parityHashes)
		{
			lock (con)// MAYBE: see AddNewAssuranceAndTmpData
			{
				con.RunInTransaction(() =>
				{
					foreach (var ph in parityHashes)
					{
						con.Insert(new SQLMap.ParityRelation
						{
							IsNew = true,
							State = SQLMap.ParityRelationState.Processing,
							CollectionID = collectionId,
							ElementID = ++inputLength,
							PlainHash = ph,
							IsParityElement = true,
						});
					}

					con.Query<SQLMap.ParityRelation>(@"
						update parityrelation set state = ?, tmpDataCompressed = null where collectionId = ?
					", SQLMap.ParityRelationState.Done, collectionId);
				});

#if USE_ALTERNATIE_RAW_CACHE
				var dir = Path.Combine(cachePath, "tmpDataCompressed", collectionId.ToString());
				safeDeleteDir(dir);
#endif
			}
		}

		public void ForceParityProcessingState()
		{
			lock (con)
			{
				con.Execute(@"
					update parityrelation
					set state = ?
					where state = ?
				", SQLMap.ParityRelationState.Processing, SQLMap.ParityRelationState.FillingUp);
			}
		}

		public void AddNewAssuranceAndTmpData(byte[] indexId, uint rep, byte[] plainHash, uint compressedSize, byte[] tmpBytesCompressed, Action _additionalActionForTransaction = null)
		{
			lock (con) // MAYBE: something better than lock? we get a savePoint error otherwise atm.
			{
				con.RunInTransaction(() =>
				{
					var segment = new SQLMap.Segment
					{
						IsNew = true,
						IndexID = indexId,
						Replication = rep,
						PlainHash = plainHash,
						CompressedLength = compressedSize,
					};
					con.Insert(segment);

					// get next collectionId and elementId
					var pr = con.Query<SQLMap.ParityRelation>(@"
						select collectionId, elementId + 1 elementId from (
							select collectionId, elementId from parityrelation where state = ?
							order by collectionId desc, elementId desc
							limit 1
						) x
						union
						select coalesce(max(collectionId) + 1, 1) collectionId, 1 elementId from parityrelation
						limit 1
					", SQLMap.ParityRelationState.FillingUp).First();

#if USE_ALTERNATIE_RAW_CACHE
					var dir = Path.Combine(cachePath, "tmpDataCompressed", pr.CollectionID.ToString());
					Directory.CreateDirectory(dir);
					safeSave(Path.Combine(dir, pr.ElementID.ToString()), tmpBytesCompressed);
#endif

					var parityRelation = new SQLMap.ParityRelation
					{
						IsNew = true,
						State = SQLMap.ParityRelationState.FillingUp,
						CollectionID = pr.CollectionID,
						ElementID = pr.ElementID,
						PlainHash = plainHash,
						IsParityElement = false,
					};
#if !USE_ALTERNATIE_RAW_CACHE
					parityRelation.TmpDataCompressed = tmpBytesCompressed;
#endif
					con.Insert(parityRelation);


					// set state to processing if needed
					con.Query<SQLMap.ParityRelation>(@"
						update parityrelation
						set state = ?
						where state = ? and (
							select count(*) from parityrelation pr2 where pr2.collectionId = parityrelation.collectionId and (not pr2.isParityElement)
						) >= ?
					", SQLMap.ParityRelationState.Processing, SQLMap.ParityRelationState.FillingUp, Constants.DataBeforeParity);

					if (_additionalActionForTransaction != null)
						_additionalActionForTransaction();
				});
			}
		}

		public SQLMap.Segment FindMatchingSegmentInAssurancesByIndexId(byte[] indexId)
		{
			lock (con)
			{
				return con.Query<SQLMap.Segment>("select * from segment where indexId = ? limit 1", indexId).FirstOrDefault();
			}
		}

		public SQLMap.Segment FindMatchingSegmentInAssurancesByPlainHash(byte[] plainHash)
		{
			lock (con)
			{
				return con.Query<SQLMap.Segment>("select * from segment where plainHash = ? limit 1", plainHash).FirstOrDefault();
			}
		}

		public List<SQLMap.ParityRelation> GetParityRelationsForHash(byte[] hash)
		{
			var dir = Path.Combine(cachePath, "tmpDataCompressed");
			lock (con)
			{
				// includes incomplete ones
				var rels = con.Query<SQLMap.ParityRelation>("select * from parityrelation where collectionId = (select collectionId from parityrelation where plainHash = ?)", hash);
				foreach (var e in rels)
				{
#if USE_ALTERNATIE_RAW_CACHE
					e.TmpDataCompressed = File.ReadAllBytes(Path.Combine(dir, e.CollectionID.ToString(), e.ElementID.ToString()));
#endif
				}
				return rels;
			}
		}

		public SQLMap.CommandMetaType? MetaTypeAtPathInTransientCache(string path)
		{
			lock (con)
			{
				var res = con.Query<SQLMap.Command>("select * from command where path = ? limit 1", path).FirstOrDefault();
				if (res == null) return null;
				return res.MetaType;
			}
		}

		public List<SQLMap.Command> CommandsInTransientCache(string path = null)
		{
			lock (con)
			{
				var commands = path == null
					? con.Query<SQLMap.Command>("select * from command")
					: con.Query<SQLMap.Command>("select * from command where path = ?", path);
				commands = commands.Select(c =>
				{
					c.Index--;
					return c;
				}).ToList();
				return commands;
			}
		}

		public void CommandsFlushedForPath(string path, int indexSmallerThan, bool _isAlreadyInTransaction = false)
		{
			indexSmallerThan++;
			Action action = () => con.Execute("delete from command where path = ? and `index` < ?", path, indexSmallerThan);

			if (_isAlreadyInTransaction)
			{
				action();
			}
			else
			{
				lock (con) action();
			}
		}

		public void AddCommandsToTransientCache(List<SQLMap.Command> commands)
		{
			commands = commands.Select(c =>
			{
				c.Index++;
				return c;
			}).ToList();
			lock (con)
			{
				con.InsertAll(commands, runInTransaction: true);
			}
		}

		static void safeSave(string path, byte[] data)
		{
			var final = path;
			var tmp = path + ".tmp";
			File.WriteAllBytes(tmp, data);
			File.Move(tmp, final);
		}

		static void safeDeleteDir(string path)
		{
			var tmp = path + ".tmp";
			Directory.Move(path, tmp);
			Directory.Delete(tmp, true);
		}
	}

	public static class Extensions
	{
		public static DB.SQLMap.Command ToDBObject(this Formats.MetaSegment.Command c)
		{
			return new DB.SQLMap.Command
			{
				CMD = (DB.SQLMap.Command.CMDV)c.CMD,
				TYPE = (DB.SQLMap.Command.TYPEV)c.TYPE,
				MetaType = c.FOLDER_ORIGIN != null ? DB.SQLMap.CommandMetaType.Folder : DB.SQLMap.CommandMetaType.File,
				FolderOrigin_Name = c.FOLDER_ORIGIN?.Name,
				FolderOrigin_FileSize = c.FOLDER_ORIGIN?.FileSize ?? 0,
				FileOrigin_Hash = c.FILE_ORIGIN?.Hash,
				FileOrigin_Start = c.FILE_ORIGIN?.Start ?? 0,
				FileOrigin_Size = c.FILE_ORIGIN?.Size ?? 0,
			};
		}

		public static Formats.MetaSegment.Command ToProtoObject(this DB.SQLMap.Command c)
		{
			return new Formats.MetaSegment.Command
			{
				CMD = (Formats.MetaSegment.Command.CMDV)c.CMD,
				TYPE = (Formats.MetaSegment.Command.TYPEV)c.TYPE,
				FOLDER_ORIGIN = c.MetaType != DB.SQLMap.CommandMetaType.Folder ? null : new Formats.MetaSegment.Command.FolderOrigin
				{
					Name = c.FolderOrigin_Name,
					FileSize = c.FolderOrigin_FileSize,
				},
				FILE_ORIGIN = c.MetaType != DB.SQLMap.CommandMetaType.File ? null : new Formats.MetaSegment.Command.FileOrigin
				{
					Hash = c.FileOrigin_Hash,
					Size = c.FileOrigin_Size,
					Start = c.FileOrigin_Start,
				},
			};
		}
	}
}

