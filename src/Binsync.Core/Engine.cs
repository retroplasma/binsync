using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using Binsync.Core.Caches;
using Binsync.Core.Services;
using Binsync.Core.Helpers;
using Binsync.Core.Formats;

namespace Binsync.Core
{
	public class Engine
	{
		Identifier identifier;
		Encryption encryption;
		Generator generator;
		IServiceFactory svcFactory;
		int totalConnections; // download + upload
		int uploadConnections;
		DB db;
		DedupContext<byte[]> dedupCtxD = new DedupContext<byte[]>();
		DedupContext dedupCtxU = new DedupContext();

		public static Engine CreateDummy(string storageCode, string password, int totalConnections, int uploadConnections)
		{
			var path = "/tmp/binsync/";
			var cachePath = Path.Combine(path, "cache");
			var fac = new Services.TestFactory(Path.Combine(path, "dummystore"));
			return new Engine(storageCode, password, cachePath, fac, totalConnections, uploadConnections);
		}

		Engine(string storageCore, string password, string cachePath, IServiceFactory fac, int totalConnections, int uploadConnections)
		{
			if (!(totalConnections >= 1 && uploadConnections >= 1 && totalConnections >= uploadConnections))
			{
				throw new ArgumentException("connection counts must be >= 1 and total must be >= upload connection");
			}
			var key = Generator.DeriveKey(storageCode: storageCore, password: password);
			svcFactory = fac;
			identifier = new Identifier(key);
			encryption = new Encryption(identifier);
			generator = new Generator(identifier);
			this.totalConnections = totalConnections;
			this.uploadConnections = totalConnections;
			conSemT = new SemaphoreSlim(totalConnections, totalConnections);
			conSemU = new SemaphoreSlim(uploadConnections, uploadConnections);

			db = new DB(Path.Combine(cachePath, identifier.PublicHash));
		}

		public void Load()
		{
			//assuranceContainer.RefillNewAssuranceNOTTHREADSAFE();
			//assuranceContainer.RefillMainAssuranceNOTTHREADSAFE();
		}

		// TODO: assurance flush

		public async Task UploadFile(string localPath, string remotePath)
		{
			// TODO: check path format validity
			var metaSegments = new List<MetaSegment.Command.FileOrigin>();
			long fileSize = 0;
			var tasks = new HashSet<Task>();
			int maxConcurrentTasks = (32 * 1024 * 1024) / Constants.SegmentSize; // based on total 32 MiB chunk cache
			foreach (var chunk in General.EnumerateFileChunks(localPath, Constants.SegmentSize))
			{
				var hash = chunk.Bytes.SHA256();
				metaSegments.Add(new MetaSegment.Command.FileOrigin { Hash = hash, Start = chunk.Position, Size = (uint)chunk.Bytes.Length });
				fileSize += chunk.Bytes.Length;
				var task = uploadChunk(chunk.Bytes, hash, generator.GenerateRawOrParityID(hash));
				tasks.Add(task);
				if (tasks.Count == maxConcurrentTasks)
				{
					var t = await Task.WhenAny(tasks);
					tasks.Remove(t);
					if (t.IsFaulted) throw t.Exception;  // let remaining ones finish gracefully?
				}
			}

			await Task.WhenAll(tasks);
			try
			{
				await pushFileToMeta(metaSegments, fileSize, remotePath);
			}
			catch (MetaEntryOverwriteException ex)
			{
				Constants.Logger.Log("can't push to meta. reason: " + ex.Message);
				//throw ex;
			}
		}

		async Task uploadChunk(byte[] bytes, byte[] hash, byte[] indexId, Action _inAssuranceAdditionTransaction = null)
		{
			await dedupCtxU.Deduplicate(indexId, async () =>
			{
				await flushParity(force: false);
				await _uploadChunk(bytes, hash, indexId, false, _inAssuranceAdditionTransaction);
			});
		}

		SemaphoreSlim flushParitySem = new SemaphoreSlim(1, 1);

		public async Task ForceFlushParity()
		{
			await flushParity(force: true);
		}

		async Task flushParity(bool force)
		{
			await flushParitySem.WaitAsync();
			try
			{
				if (force)
				{
					db.ForceParityProcessingState();
				}

				var d = db.GetProcessingParityRelations();
				foreach (var key in d.Keys)
				{
					var k = (long)key;
					var v = d[k] as List<DB.SQLMap.ParityRelation>;

					// create parities
					var sw = System.Diagnostics.Stopwatch.StartNew();
					Constants.Logger.Log("creating parity");
					var input = v.Select(x => x.TmpDataCompressed).ToArray();
					var parities = Integrity.Parity.CreateParity(input, Constants.ParityCount);
					Constants.Logger.Log("parity created in {0}s", sw.ElapsedMilliseconds / 1000.0);

					// MAYBE: add concurrency cap
					var tasks = new List<Task>();
					// upload parities
					byte[][] parityHashes = new byte[parities.Length][];
					for (int i = 0; i < parities.Length; i++)
					{
						var bytes = parities[i];
						var hash = bytes.SHA256();
						parityHashes[i] = hash;
						var indexId = this.generator.GenerateRawOrParityID(hash);

						var task = dedupCtxU.Deduplicate(indexId, async () =>
						{
							await _uploadChunk(bytes, hash, indexId, isParity: true);
						});
						tasks.Add(task);
					}
					await Task.WhenAll(tasks);

					// clear
					db.CloseParityRelations(k, input.Length, parityHashes);

				}
			}
			finally
			{
				flushParitySem.Release();
			}
		}


		async Task _uploadChunk(byte[] bytes, byte[] hash, byte[] indexId, bool isParity = false, Action _inAssuranceAdditionTransaction = null)
		{
			if (null != db.FindMatchingSegmentInAssurancesByIndexId(indexId))
				return;
			for (var r = 0; r < Constants.ReplicationAttemptCount; r++)
			{
				var locator = this.generator.DeriveLocator(indexId, (uint)r);
				var compressed = bytes.GetCompressed();
				var lengthForAssurance = isParity ? bytes.Length : compressed.Length; // MAYBE: prettier?
				var encrypted = encryption.Encrypt(compressed, locator);
				var ok = await withServiceFromPool(serviceUsage.Up, async svc =>
				{
					var randomSubject = Cryptography.GetRandomBytes(32).ToHexString();
					var res = svc.Upload(new Chunk(locator, randomSubject, encrypted));
					return await Task.FromResult(res);
				});

				if (!ok)
				{
					// MAYBE: custom exception instead of having bool result?
					Constants.Logger.Log($"Upload not ok. Possibly the article exists. Retrying with r = {r + 1}");
					continue;
				}
				else
				{
					Constants.Logger.Log("Upload ok for " + (isParity ? "par" : "dat") + $" with {indexId.ToHexString()} with r = {r}"
					+ $"\n  -> {locator}");
					if (isParity)
					{
						db.AddNewAssurance(indexId, (uint)r, hash, (uint)lengthForAssurance, _inAssuranceAdditionTransaction);
					}
					else
					{
						db.AddNewAssuranceAndTmpData(indexId, (uint)r, hash, (uint)lengthForAssurance, compressed, _inAssuranceAdditionTransaction);
					}
					return;
				}
			}
			throw new Exception("Could not upload any replications");
		}
		SemaphoreSlim conSemT;
		SemaphoreSlim conSemU;
		ConcurrentBag<IService> services = new ConcurrentBag<IService>();
		enum serviceUsage { Up, Down };
		async Task<T> withServiceFromPool<T>(serviceUsage usage, Func<IService, Task<T>> fn)
		{
			if (usage == serviceUsage.Up)
			{
				await conSemU.WaitAsync();
			}
			await conSemT.WaitAsync();
			try
			{
				return await Task.Run(async () =>
				{
					IService service;
					var ok = services.TryTake(out service);
					if (!ok)
					{
						service = svcFactory.Give();
					}
					if (!service.Connected)
					{
						if (!service.Connect())
						{
							throw new Exception("Could not connect to service");
						}
					}
					try
					{
						return await fn(service);
					}
					finally
					{
						services.Add(service);
					}
				});
			}
			finally
			{
				conSemT.Release();
				if (usage == serviceUsage.Up)
				{
					conSemU.Release();
				}
			}
		}

		public async Task<byte[]> DownloadChunk(byte[] indexId, bool parityAware = true)
		{
			return await dedupCtxD.Deduplicate(indexId, async () =>
			{
				return await _downloadChunk(indexId, parityAware);
			});
		}

		async Task<byte[]> _downloadChunk(byte[] indexId, bool parityAware = true)
		{
			var seg = db.FindMatchingSegmentInAssurancesByIndexId(indexId);
			if (seg == null) throw new KeyNotFoundException($"segment at index '{indexId.ToHexString()}' not found");
			var loc = generator.DeriveLocator(indexId, seg.Replication);
			var data = await withServiceFromPool(serviceUsage.Down, async svc =>
			{
				var res = svc.GetBody(loc);
				return await Task.FromResult(res);
			});
			byte[] decrypted = null;
			try
			{
				if (data == null) throw new FileNotFoundException(@"data for segment with index '{indexId.ToHexString()}' not found");
				try
				{
					decrypted = encryption.Decrypt(data, loc);
				}
				catch (Exception ex)
				{
					throw new InvalidDataException(@"data for segment with index '{indexId.ToHexString()}' is invalid", ex);
				}
			}
			catch (Exception)
			{
				if (!parityAware) return null;
				var hash = seg.PlainHash;
				var rels = db.GetParityRelationsForHash(hash);
				var ours = rels.Select((r, i) => new { r, i }).Where(ri => ri.r.PlainHash.SequenceEqual(hash)).First();
				if (ours.r.TmpDataCompressed != null) return ours.r.TmpDataCompressed.GetDecompressed();
				var segs = rels.Select(r => db.FindMatchingSegmentInAssurancesByPlainHash(r.PlainHash)).ToArray();
				var tasks = rels.Select(async (r, i) =>
				{
					return r.TmpDataCompressed?.GetDecompressed() ?? await DownloadChunk(segs[i].IndexID, parityAware: false);
				});
				var dl = await Task.WhenAll(tasks);
				var parityInfo1 = dl.Select((d, i) => new { d, i }).Where(r => !rels[r.i].IsParityElement)
					.Select(r => new Integrity.Parity.ParityInfo
					{
						Data = r.d == null ? null : r.d.GetCompressed(),
						Broken = r.d == null,
						RealLength = segs[r.i].CompressedLength
					}).ToArray();
				var parityInfo2 = dl.Select((d, i) => new { d, i }).Where(r => rels[r.i].IsParityElement)
					.Select(r => new Integrity.Parity.ParityInfo
					{
						Data = r.d == null ? null : r.d,
						Broken = r.d == null,
						RealLength = segs[r.i].CompressedLength
					}).ToArray();
				try
				{
					Constants.Logger.Log($"repairing {indexId.ToHexString()}");
					Integrity.Parity.RepairWithParity(ref parityInfo1, ref parityInfo2);
					var recovered = parityInfo1.Concat(parityInfo2).Select((p, i) => new { p, i })
						.Where(pi => pi.i == ours.i).Select(pi => pi.p).First().Data;
					if (!ours.r.IsParityElement && recovered != null)
					{
						recovered = recovered.GetDecompressed();
					}
					var valid = recovered != null && recovered.SHA256().SequenceEqual(hash);
					if (!valid) throw new InvalidDataException(@"not enough parity for segment with index '{indexId.ToHexString()}'");
					return recovered;
				}
				catch (Exception ex)
				{
					throw new NotEnoughParityException(@"not enough parity for segment with index '{indexId.ToHexString()}'", ex);
				}
			}
			return decrypted.GetDecompressed();
		}

		SemaphoreSlim metaSem = new SemaphoreSlim(1, 1);

		public class Meta : Formats.MetaSegment
		{
			public string Path;
			public bool IsFile;
		}

		public async Task<Meta> DownloadMetaForPath(string path)
		{
			int maxConcurrentTasks = 10;
			uint metaIndex = 0;
			var tasks = new List<Task<byte[]>>();

			var isFile = null != db.FindMatchingSegmentInAssurancesByIndexId(generator.GenerateMetaFileID(0, path));
			if (!isFile)
			{
				var isFolder = null != db.FindMatchingSegmentInAssurancesByIndexId(generator.GenerateMetaFolderID(0, path));
				if (!isFolder)
					return null;
			}

			for (; ; metaIndex++)
			{
				var indexId = isFile
					? generator.GenerateMetaFileID(metaIndex, path)
					: generator.GenerateMetaFolderID(metaIndex, path);
				var seg = db.FindMatchingSegmentInAssurancesByIndexId(indexId);
				if (seg == null)
					break;

				var task = DownloadChunk(indexId);
				tasks.Add(task);

				if (tasks.Count == maxConcurrentTasks)
				{
					var t = await Task.WhenAny(tasks);
					if (t.IsFaulted) throw t.Exception; // catch earlier by capping maxConcurrentTasks to connection limit?
				}
			}

			var combinedMeta = new Meta { Path = path, IsFile = isFile };

			var values = await Task.WhenAll(tasks);
			foreach (var cmds1 in values.Select(v => MetaSegment.FromByteArray(v).Commands))
			{
				combinedMeta.Commands.AddRange(cmds1);
			}

			var cmds2 = db.CommandsInTransientCache(path).OrderBy(c => c.Index).Select(x => x.ToProtoObject());
			combinedMeta.Commands.AddRange(cmds2);

			return combinedMeta;
		}

		async Task pushFileToMeta(List<MetaSegment.Command.FileOrigin> metaSegments, long fileSize, string remotePath)
		{
			await metaSem.WaitAsync();
			try
			{
				// validate and resolve remotePath
				// MAYBE: move remotePath conversion out of here and replace here with validation after conversion
				if (Path.DirectorySeparatorChar != '/' && Path.AltDirectorySeparatorChar != '/')
					throw new NotImplementedException("remotePath separator must be '/'");
				if (remotePath.Substring(0, 1) != "/") throw new ArgumentException("Invalid path. Must be absolute file path.");
				remotePath = Path.GetFullPath(remotePath);
				if (remotePath.Substring(0, 1) != "/" || !Path.IsPathRooted(remotePath) || Path.GetFileName(remotePath) == "")
					throw new ArgumentException("Invalid path. Must be absolute file path.");

				// convert to actual remote path
				// split path into partial paths: root (empty), directories (without leading or trailing slash) and file name
				var root = "";
				var candidateDirs = ((Func<string, string[]>)(path =>
				 {
					 var pp = Path.GetDirectoryName(path).Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
					 if (pp.Length == 0) return pp;
					 pp[0] = $"{pp[0]}";
					 for (var i = 1; i < pp.Length; i++)
						 pp[i] = $"{pp[i - 1]}/{pp[i]}";
					 return pp;
				 }))(remotePath);
				var file = remotePath.Substring(1);
				var allDirs = new string[] { root }.Concat(candidateDirs).ToArray();
				var all = allDirs.Concat(new[] { file }).ToArray();

				// check if we can push to meta at that remotePath.
				// if we want deletion or modification later, we need to iterate over the index here and aggregate meta
				foreach (var dir in candidateDirs)
				{
					if (DB.SQLMap.CommandMetaType.File == db.MetaTypeAtPathInTransientCache(dir) ||
						null != db.FindMatchingSegmentInAssurancesByIndexId(generator.GenerateMetaFileID(0, dir)))
					{
						throw new MetaEntryOverwriteException($"Directory '{dir}' would overwrite file at the same path.");
					}
				}
				var metaTypeFile = db.MetaTypeAtPathInTransientCache(file);
				if (DB.SQLMap.CommandMetaType.Folder == metaTypeFile ||
					null != db.FindMatchingSegmentInAssurancesByIndexId(generator.GenerateMetaFolderID(0, file)))
				{
					throw new MetaEntryOverwriteException($"File '{file}' would overwrite folder at the same path.");
				}
				if (DB.SQLMap.CommandMetaType.File == metaTypeFile ||
					null != db.FindMatchingSegmentInAssurancesByIndexId(generator.GenerateMetaFileID(0, file)))
				{
					throw new MetaEntryOverwriteException($"File '{file}' would overwrite file at the same path.");
				}

				Constants.Logger.Log($"Pushing to meta: {remotePath}");

				var allDirsEx = allDirs.Select((d, i) => new { last = Path.GetFileName(d), full = d, i }).ToArray();

				var pushList = new List<DB.SQLMap.Command>();
				foreach (var dir in allDirsEx)
				{
					var commands = new List<DB.SQLMap.Command>();
					var i = 0;
					for (; ; i++)
					{
						var indexId = generator.GenerateMetaFolderID((uint)i, dir.full);
						var seg = db.FindMatchingSegmentInAssurancesByIndexId(indexId);
						if (seg == null)
							break;
						var chunk = await DownloadChunk(indexId); // TODO cache
						commands.AddRange(MetaSegment.FromByteArray(chunk).Commands
							.Select(c => c.ToDBObject())
							.Select(c =>
							{
								c.IsNew = false;
								c.Path = dir.full;
								c.MetaType = DB.SQLMap.CommandMetaType.Folder;
								return c;
							})
						);
					}
					var commands2 = db.CommandsInTransientCache(dir.full);
					if (commands2.Where(c => c.Index < i).FirstOrDefault() != null)
						throw new InvalidDataException($"too small index in meta db cache for dir '{dir}'");

					i += commands2.Count;

					var allCommands = commands.Concat(commands2).ToArray();

					if (dir.i != allDirsEx.Length - 1)
					{
						var next = allDirsEx[dir.i + 1];
						var hasFolder = null != allCommands.Where(c => c.MetaType == DB.SQLMap.CommandMetaType.Folder && c.FolderOrigin_Name == next.last).FirstOrDefault();
						if (hasFolder)
							continue;

						// add folder to folder
						pushList.Add(new DB.SQLMap.Command
						{
							IsNew = true,
							Path = dir.full,
							Index = i,
							MetaType = DB.SQLMap.CommandMetaType.Folder,
							CMD = DB.SQLMap.Command.CMDV.ADD,
							TYPE = DB.SQLMap.Command.TYPEV.FOLDER,
							FolderOrigin_Name = next.last,
						});
					}
					else
					{
						var fileName = Path.GetFileName(file);
						var hasFile = null != allCommands.Where(c => c.MetaType == DB.SQLMap.CommandMetaType.Folder && c.FolderOrigin_Name == fileName).FirstOrDefault();
						if (hasFile)
							throw new MetaEntryOverwriteException($"File '{file}' would overwrite file in parent folder.");

						// add file to folder
						pushList.Add(new DB.SQLMap.Command
						{
							IsNew = true,
							Path = dir.full,
							Index = i,
							MetaType = DB.SQLMap.CommandMetaType.Folder,
							CMD = DB.SQLMap.Command.CMDV.ADD,
							TYPE = DB.SQLMap.Command.TYPEV.FILE,
							FolderOrigin_Name = fileName,
							FolderOrigin_FileSize = fileSize,
						});
					}
				}

				// add blocks to file
				pushList.AddRange(metaSegments.Select((ms, i) => new DB.SQLMap.Command
				{
					IsNew = true,
					Path = file,
					Index = i,
					MetaType = DB.SQLMap.CommandMetaType.File,
					CMD = DB.SQLMap.Command.CMDV.ADD,
					TYPE = DB.SQLMap.Command.TYPEV.BLOCK,
					FileOrigin_Hash = ms.Hash,
					FileOrigin_Size = ms.Size,
					FileOrigin_Start = ms.Start,
				}));

				db.AddCommandsToTransientCache(pushList);
			}
			finally
			{
				metaSem.Release();
			}
		}

		public async Task FlushMeta()
		{
			await metaSem.WaitAsync();
			try
			{
				var cmds = db.CommandsInTransientCache();
				var groupedCmds = cmds.OrderBy(c => c.Index).GroupBy(c => c.Path);
				foreach (var group in groupedCmds)
				{
					var g = group.ToList();
					var path = group.Key;
					var seg = new Formats.MetaSegment { Commands = g.Select(e => e.ToProtoObject()).ToList() };
					var protoSegs = seg.ToListOfByteArrays();

					var isFile = seg.Commands[0].ToDBObject().MetaType == DB.SQLMap.CommandMetaType.File;
					var sum = g[0].Index;

					var nextIndex = 0;
					for (; ; nextIndex++)
					{
						var indexId = isFile
							? generator.GenerateMetaFileID((uint)nextIndex, path)
							: generator.GenerateMetaFolderID((uint)nextIndex, path);
						var exists = null != db.FindMatchingSegmentInAssurancesByIndexId(indexId);
						if (!exists) break;
					}

					foreach (var psi in protoSegs.Select((ps, i) => new { ps, i }))
					{
						sum += Formats.MetaSegment.FromByteArray(psi.ps).Commands.Count;

						var idx = nextIndex + psi.i;
						var indexId = isFile
							? generator.GenerateMetaFileID((uint)idx, path)
							: generator.GenerateMetaFolderID((uint)idx, path);

						await uploadChunk(psi.ps, psi.ps.SHA256(), indexId, _inAssuranceAdditionTransaction: () =>
						{
							db.CommandsFlushedForPath(path, indexSmallerThan: sum, _isAlreadyInTransaction: true);
						});

						// TODO: cache uploaded chunk					
					}
				}
			}
			finally
			{
				metaSem.Release();
			}
		}

		class DedupContext : DedupContext<int>
		{
			public async Task Deduplicate(byte[] indexId, Func<Task> fn)
			{
				await base.Deduplicate(indexId, async () =>
				{
					await fn();
					return 1;
				});
			}

			new public Task<int> Deduplicate(byte[] indexId, Func<Task<int>> fn)
			{
				throw new UnauthorizedAccessException("Use DedupContext<T> for generic access");
			}
		}

		class DedupContext<T>
		{
			class dedupContainer { public T Result; public Exception Exception = null; public List<SemaphoreSlim> Semaphores = new List<SemaphoreSlim>(); }
			SemaphoreSlim dedupSem = new SemaphoreSlim(1, 1);
			Dictionary<string, dedupContainer> dedupLive = new Dictionary<string, dedupContainer>();
			public async Task<T> Deduplicate(byte[] indexId, Func<Task<T>> fn)
			{
				var indexIdStr = indexId.ToHexString();
				SemaphoreSlim s = null;
				dedupContainer d = null;
				await dedupSem.WaitAsync();
				try
				{
					if (dedupLive.ContainsKey(indexIdStr))
					{
						// live dedup
						(d = dedupLive[indexIdStr]).Semaphores.Add(s = new SemaphoreSlim(0, 1));
					}
					else
					{
						dedupLive.Add(indexIdStr, new dedupContainer());
					}
				}
				finally { dedupSem.Release(); }

				if (s != null)
				{
					await s.WaitAsync();
					if (d.Exception != null) throw d.Exception;
					return d.Result;
				}

				T res = default(T);
				Exception ex = null;
				try
				{
					res = await fn();
					return res;
				}
				catch (Exception _ex)
				{
					ex = _ex;
					throw ex;
				}
				finally
				{
					await dedupSem.WaitAsync();
					try
					{
						dedupLive[indexIdStr].Result = res;
						dedupLive[indexIdStr].Exception = ex;
						foreach (var sem in dedupLive[indexIdStr].Semaphores)
						{
							sem.Release();
						}
						dedupLive.Remove(indexIdStr);
					}
					finally { dedupSem.Release(); }
				}
			}
		}
	}

	public class NotEnoughParityException : Exception
	{
		public NotEnoughParityException() { }
		public NotEnoughParityException(string message) : base(message) { }
		public NotEnoughParityException(string message, Exception inner) : base(message, inner) { }
	}

	public class MetaEntryOverwriteException : Exception
	{
		public MetaEntryOverwriteException() { }
		public MetaEntryOverwriteException(string message) : base(message) { }
		public MetaEntryOverwriteException(string message, Exception inner) : base(message, inner) { }
	}
}
