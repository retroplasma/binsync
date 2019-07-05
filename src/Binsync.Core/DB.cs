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
				public byte[] IndexID { get; set; }
				public UInt32 Replication { get; set; }
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
				public byte[] PlainHash { get; set; }
				public bool IsParityElement { get; set; }
			}

			public enum ParityRelationState
			{
				FillingUp, Processing, Done
			}
		}

		public void AddNewAssurance(byte[] indexId, uint rep, byte[] plainHash, uint compressedSize)
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
				con.Insert(segment);
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

		public void AddNewAssuranceAndTmpData(byte[] indexId, uint rep, byte[] plainHash, uint compressedSize, byte[] tmpBytesCompressed)
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
					Console.WriteLine($"pr {pr.CollectionID} {pr.ElementID}");

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
}

