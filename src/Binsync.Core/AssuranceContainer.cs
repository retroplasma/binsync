using System;
using System.Collections.Generic;
using System.Linq;

using Binsync.Core.Helpers;
using Binsync.Core.Formats;
using Binsync.Core.Caches;

namespace Binsync.Core
{
	/// <summary>
	/// Store and search for assurances etc.
	/// </summary>
	public class AssuranceContainer
	{
		public readonly object AssuranceLock = new object();

		readonly DiskCache diskCache;

		public AssuranceContainer(DiskCache diskCache)
		{
			this.diskCache = diskCache;
		}

		public AssuranceSegment MainAssurance = new AssuranceSegment();
		public AssuranceSegment NewAssurance = new AssuranceSegment();
		public uint NextAssuranceIndex = 0;

		Dictionary<string, AssuranceSegment.Segment> DictIndexIdToSegment = new Dictionary<string, AssuranceSegment.Segment>();


		public AssuranceSegment.Segment FindMatchingSegmentInAssurancesByPlainHashNOTTHREADSAFE(byte[] plainHash)
		{
			Func<IEnumerable<AssuranceSegment.Segment>, AssuranceSegment.Segment> segWithHash =
				segments =>
				segments.FirstOrDefault(segment => segment.PlainHash.SequenceEqual(plainHash));

			return segWithHash(MainAssurance.Segments) ?? segWithHash(NewAssurance.Segments);
		}

		public AssuranceSegment.Segment FindMatchingSegmentInAssurancesByIndexIdNOTTHREADSAFE(byte[] indexId)
		{
			var key = indexId.ToHexString();
			AssuranceSegment.Segment val;
			return DictIndexIdToSegment.TryGetValue(key, out val) ? val : null;
		}

		public AssuranceSegment.ParityRelation RelSearchNOTTHREADSAFE(byte[] plainHash)
		{
			Func<AssuranceSegment, AssuranceSegment.ParityRelation> relationWithHash = assuranceSegment => assuranceSegment.ParityRelations
				.FirstOrDefault(relation =>
					relation.DataPlainHashes.Any(h => h.SequenceEqual(plainHash)) ||
					relation.ParityPlainHashes.Any(h => h.SequenceEqual(plainHash))
				);

			return relationWithHash(MainAssurance) ?? relationWithHash(NewAssurance);
		}

		public bool CheckParityInAsssurancesNOTTHREADSAFE(byte[] hash)
		{
			Predicate<IEnumerable<AssuranceSegment.ParityRelation>> predicate =
				relation =>
				relation.Any(
					r => r.DataPlainHashes.Any( // also parityplainhashes?
						ph => ph.SequenceEqual(hash)
					)
				);
			return predicate(MainAssurance.ParityRelations) || predicate(NewAssurance.ParityRelations);
		}

		public bool LookForIndexIdAndLocatorNOTTHREADSAFE(byte[] indexId, uint replication)
		{
			Predicate<IEnumerable<AssuranceSegment.Segment>> predicate =
				segments =>
				segments.Any(
					segment => segment.Replication == replication && segment.IndexID.SequenceEqual(indexId) // only indexid?
				);

			return predicate(MainAssurance.Segments) || predicate(NewAssurance.Segments);
		}

		public void AppendAssurancesNOTTHREADSAFE(AssuranceSegment assurance)
		{
			semiAppendAssurancesNOTTHREADSAFE(assurance);
			diskCache.AddNewMainAssurance(assurance);
		}

		void semiAppendAssurancesNOTTHREADSAFE(AssuranceSegment assurance)
		{
			MainAssurance.Segments.AddRange(assurance.Segments);

			foreach (var seg in assurance.Segments)
			{
				addSegmentIndizesToDict(seg);
			}

			MainAssurance.ParityRelations.AddRange(assurance.ParityRelations);
		}

		void addSegmentIndizesToDict(AssuranceSegment.Segment seg)
		{
			var key = seg.IndexID.ToHexString();
			if (!DictIndexIdToSegment.ContainsKey(key))
				DictIndexIdToSegment.Add(key, seg);
		}

		public void AddRelationsNOTTHREADSAFE(List<byte[]> dataRel, List<byte[]> parityRel)
		{
			var rel = new AssuranceSegment.ParityRelation
			{
				DataPlainHashes = dataRel.ToArray(),
				ParityPlainHashes = parityRel.ToArray()
			};

			{
				NewAssurance.ParityRelations.Add(rel);
				diskCache.AddNewParityRelation(rel);
			}
		}

		public void AddNewAssuranceNOTTHREADSAFE(byte[] indexId, uint rep, byte[] plainHash, uint compressedSize)
		{
			var seg = new AssuranceSegment.Segment
			{
				IndexID = indexId,
				Replication = rep,
				PlainHash = plainHash,
				CompressedLength = compressedSize
			};

			{
				NewAssurance.Segments.Add(seg);
				diskCache.AddNewAssuranceSegment(seg);
			}
			addSegmentIndizesToDict(seg);
		}

		public void AssurancesNewToMainNOTTHREADSAFE(List<byte[]> assurancesSerialized)
		{
			MainAssurance.Segments.AddRange(NewAssurance.Segments);
			MainAssurance.ParityRelations.AddRange(NewAssurance.ParityRelations);

			// good idea? or rather wait until there?
			foreach (var assSer in assurancesSerialized)
			{
				diskCache.AddNewMainAssuranceSerialized(assSer);
			}

			// increment next assurance index for polling ?

			{
				NewAssurance.Segments.Clear();
				NewAssurance.ParityRelations.Clear();
				diskCache.ClearNewAssurances();
			}
		}

		public void RefillNewAssuranceNOTTHREADSAFE()
		{
			foreach (var seg in diskCache.EnumerateNewAssuranceSegments())
			{
				NewAssurance.Segments.Add(seg);
				addSegmentIndizesToDict(seg);
			}

			foreach (var rel in diskCache.EnumerateNewParityRelations())
			{
				NewAssurance.ParityRelations.Add(rel);
			}
		}

		public void RefillMainAssuranceNOTTHREADSAFE()
		{
			foreach (var ass in diskCache.EnumerateMainAssurances())
			{
				semiAppendAssurancesNOTTHREADSAFE(ass);
				NextAssuranceIndex++;
			}
		}
	}
}

