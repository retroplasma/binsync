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
			await pushFileToMeta(metaSegments, fileSize, remotePath);
		}

		async Task uploadChunk(byte[] bytes, byte[] hash, byte[] indexId)
		{
			await deduplicate(indexId, async () =>
			{
				// flush parity if needed
				await flushParity(force: false);

				// upload
				await _uploadChunk(bytes, hash, indexId);

				// cache data for parity creation
				var hexHash = hash.ToHexString();
			});
		}

		SemaphoreSlim flushParitySem = new SemaphoreSlim(1, 1);

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
					Console.WriteLine($"{k}: {v.Count}");

					// create parities
					var sw = System.Diagnostics.Stopwatch.StartNew();
					Constants.Logger.Log("creating parity");
					var input = v.Select(x => x.TmpDataCompressed).ToArray();
					var parities = Integrity.Parity.CreateParity(input, Constants.ParityCount);
					Constants.Logger.Log("parity created in {0}s", sw.ElapsedMilliseconds / 1000.0);

					// upload parities
					byte[][] parityHashes = new byte[parities.Length][];
					for (int i = 0; i < parities.Length; i++)
					{
						var bytes = parities[i];
						var hash = bytes.SHA256();
						parityHashes[i] = hash;
						var indexId = this.generator.GenerateRawOrParityID(hash);

						await deduplicate(indexId, async () =>
						{
							await _uploadChunk(bytes, hash, indexId, isParity: true);
						});
					}

					// clear
					db.CloseParityRelations(k, input.Length, parityHashes);

				}
			}
			finally
			{
				flushParitySem.Release();
			}
		}


		async Task _uploadChunk(byte[] bytes, byte[] hash, byte[] indexId, bool isParity = false)
		{
			for (var r = 0; r < Constants.ReplicationCount; r++)
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
					Constants.Logger.Log($"Upload ok with r = {r}");
					if (isParity)
					{
						db.AddNewAssurance(indexId, (uint)r, hash, (uint)lengthForAssurance);
					}
					else
					{
						db.AddNewAssuranceAndTmpData(indexId, (uint)r, hash, (uint)lengthForAssurance, compressed);
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

		class dedupContainer { public Exception Exception = null; public List<SemaphoreSlim> Semaphores = new List<SemaphoreSlim>(); }
		SemaphoreSlim dedupSem = new SemaphoreSlim(1, 1);
		Dictionary<string, dedupContainer> dedupLive = new Dictionary<string, dedupContainer>();
		async Task deduplicate(byte[] indexId, Func<Task> fn)
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
					if (null != db.FindMatchingSegmentInAssurancesByIndexId(indexId))
					{
						return;
					}
					dedupLive.Add(indexIdStr, new dedupContainer());
				}
			}
			finally { dedupSem.Release(); }

			if (s != null)
			{
				await s.WaitAsync();
				if (d.Exception != null) throw d.Exception;
				return;
			}

			Exception ex = null;
			try
			{
				await fn();
			}
			catch (Exception _ex)
			{
				ex = _ex;
			}
			finally
			{
				await dedupSem.WaitAsync();
				try
				{
					dedupLive[indexIdStr].Exception = ex;
					foreach (var sem in dedupLive[indexIdStr].Semaphores)
					{
						sem.Release();
					}
					dedupLive.Remove(indexIdStr);
				}
				finally { dedupSem.Release(); }
				if (ex != null)
				{
					throw ex;
				}
			}
		}

		async Task pushFileToMeta(List<MetaSegment.Command.FileOrigin> metaSegments, long fileSize, string remotePath)
		{
			// TODO: actual
			Constants.Logger.Log("pushFileToMeta not implemented");
			await Task.Delay(1000);
		}
	}
}
