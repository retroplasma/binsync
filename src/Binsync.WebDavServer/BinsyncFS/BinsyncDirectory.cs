using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FubarDev.WebDavServer;
using FubarDev.WebDavServer.Model;
using FubarDev.WebDavServer.Model.Headers;
using FubarDev.WebDavServer.FileSystem;

namespace Binsync.WebDavServer
{
	/// <summary>
	/// An in-memory implementation of a WebDAV collection
	/// </summary>
	public class BinsyncDirectory : BinsyncEntry, ICollection, IRecusiveChildrenCollector
	{
		private readonly Dictionary<string, BinsyncEntry> _children = new Dictionary<string, BinsyncEntry>(StringComparer.OrdinalIgnoreCase);

		private readonly bool _isRoot;

		/// <summary>
		/// Initializes a new instance of the <see cref="BinsyncDirectory"/> class.
		/// </summary>
		/// <param name="fileSystem">The file system this collection belongs to</param>
		/// <param name="parent">The parent collection</param>
		/// <param name="path">The root-relative path of this collection</param>
		/// <param name="name">The name of the collection</param>
		/// <param name="isRoot">Is this the file systems root directory?</param>
		public BinsyncDirectory(
			 BinsyncFileSystem fileSystem,
			 ICollection parent,
			 Uri path,
			 string name,
			bool isRoot = false)
			: base(fileSystem, parent, path, name)
		{
			_isRoot = isRoot;
		}

		/// <inheritdoc />
		public override Task<DeleteResult> DeleteAsync(CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
			/*
			if (BinsyncFS.IsReadOnly)
				throw new UnauthorizedAccessException("Failed to modify a read-only file system");

			if (_isRoot)
				throw new UnauthorizedAccessException("Cannot remove the file systems root collection");

			if (BinsyncParent == null)
				throw new InvalidOperationException("The collection must belong to a collection");

			if (BinsyncParent.Remove(Name))
			{
				var propStore = FileSystem.PropertyStore;
				if (propStore != null)
				{
					await propStore.RemoveAsync(this, cancellationToken).ConfigureAwait(false);
				}

				return new DeleteResult(WebDavStatusCode.OK, null);
			}

			return new DeleteResult(WebDavStatusCode.NotFound, this);
			*/
		}

		string properEscape(string str)
		{
			Func<string, string> properEscape = x =>
			{
				var s = System.Uri.EscapeDataString($"a{x}a");
				return s.Substring(0, s.Length - 1).Substring(1);
			};

			var split = str.Split('/').Select(properEscape).ToArray();
			return split.Aggregate("", (a, b) => a + "/" + b).Substring(1);
		}

		string properUnescape(string str)
		{
			Func<string, string> properUnescape = x =>
			{
				var s = System.Uri.UnescapeDataString($"a{x}a");
				return s.Substring(0, s.Length - 1).Substring(1);
			};

			var split = str.Split('/').Select(properUnescape).ToArray();
			return split.Aggregate("", (a, b) => a + "/" + b).Substring(1);
		}

		/// <inheritdoc />
		public async Task<IEntry> GetChildAsync(string name, CancellationToken ct)
		{
			if (name.StartsWith("._") || name == ".DS_Store") return null;

			var engine = Program.BinsyncEngine;
			var next = _isRoot ? "" : System.Net.WebUtility.UrlDecode(this.Path.ToString());
			if (next.Length > 0 && next.Substring(next.Length - 1, 1) == "/") next = next.Substring(0, next.Length - 1);
			//next = (_isRoot ? "" : (this.Path.ToString())).Trim('/');
			//Console.WriteLine("next: " + next + ", name; " + name);
			var m = await engine.DownloadMetaForPath(next);
			if (m == null) return null;
			foreach (var c in m.Commands)
			{
				if (c.FOLDER_ORIGIN == null) throw new InvalidDataException("folder origin must be set");
				var n = c.FOLDER_ORIGIN?.Name;
				if (name != n)
					continue;
				var isFile = c.TYPE == Binsync.Core.Formats.MetaSegment.Command.TYPEV.FILE;
				if (isFile)
				{
					var fileSize = c.FOLDER_ORIGIN.FileSize;
					var newItem = new BinsyncFile(BinsyncFS, this, Path.Append(name, false), name, fileSize);
					return newItem;
				}
				else
				{
					var coll = new BinsyncDirectory(BinsyncFS, this, Path.AppendDirectory(name), name) as ICollection;
					return await coll.GetMountTargetAsync(BinsyncFS).ConfigureAwait(false);
				}
			}
			return null;
		}

		/// <inheritdoc />
		public async Task<IReadOnlyCollection<IEntry>> GetChildrenAsync(CancellationToken ct)
		{
			var result = new List<IEntry>();

			var engine = Program.BinsyncEngine;
			var next = _isRoot ? "" : System.Net.WebUtility.UrlDecode(this.Path.ToString());
			if (next.Length > 0 && next.Substring(next.Length - 1, 1) == "/") next = next.Substring(0, next.Length - 1);
			//next = (_isRoot ? "" : (this.Path.ToString())).Trim('/');
			//Console.WriteLine("next: " + next);
			var m = await engine.DownloadMetaForPath(next);
			if (m == null) return result;
			foreach (var c in m.Commands)
			{
				if (c.FOLDER_ORIGIN == null) throw new InvalidDataException("folder origin must be set");
				var name = c.FOLDER_ORIGIN?.Name;
				var isFile = c.TYPE == Binsync.Core.Formats.MetaSegment.Command.TYPEV.FILE;
				if (isFile)
				{
					var fileSize = c.FOLDER_ORIGIN.FileSize;
					var newItem = new BinsyncFile(BinsyncFS, this, Path.Append(name, false), name, fileSize);
					result.Add(newItem);
				}
				else
				{
					var coll = new BinsyncDirectory(BinsyncFS, this, Path.AppendDirectory(name), name) as ICollection;
					result.Add(await coll.GetMountTargetAsync(BinsyncFS).ConfigureAwait(false));
				}
			}

			return result;
		}

		/// <inheritdoc />
		public Task<IDocument> CreateDocumentAsync(string name, CancellationToken ct)
		{
			return Task.FromResult<IDocument>(CreateDocument(name));
		}

		/// <inheritdoc />
		public Task<ICollection> CreateCollectionAsync(string name, CancellationToken ct)
		{
			var newItem = CreateCollection(name);
			return Task.FromResult<ICollection>(newItem);
		}

		/// <inheritdoc />
		public IAsyncEnumerable<IEntry> GetEntries(int maxDepth)
		{
			throw new NotImplementedException();
			/*
			return this.EnumerateEntries(maxDepth);
			*/
		}

		// there is a problem with escaping %, ?, & etc. at the moment so we use this for now
		static string regexStr = @"^[a-zA-Z0-9_\- \.]+$";
		static System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(regexStr);
		public static void checkName(string name)
		{
			if (name != name.Trim() || !regex.IsMatch(name))
				throw new ArgumentException("Name must match " + regexStr + " and not start/end with spaces");
		}

		/// <summary>
		/// Creates a document
		/// </summary>
		/// <param name="name">The name of the document to create</param>
		/// <returns>The created document</returns>
		/// <exception cref="UnauthorizedAccessException">The file system is read-only</exception>
		/// <exception cref="IOException">Document or collection with the same name already exists</exception>
		public BinsyncFile CreateDocument(string name)
		{
			if (BinsyncFS.IsReadOnly)
				throw new UnauthorizedAccessException("Failed to modify a read-only file system");

			checkName(name);

			ETag = new EntityTag(false);
			return new BinsyncFile(BinsyncFS, this, Path.Append(name, false), name);



			//throw new NotImplementedException();
			/*if (_children.ContainsKey(name))
                throw new IOException("Document or collection with the same name already exists");
            var newItem = new BinsyncFile(BinsyncFS, this, Path.Append(name, false), name);
            _children.Add(newItem.Name, newItem);
            ETag = new EntityTag(false);
            return newItem;*/
		}

		/// <summary>
		/// Creates a new collection
		/// </summary>
		/// <param name="name">The name of the collection to create</param>
		/// <returns>The created collection</returns>
		/// <exception cref="UnauthorizedAccessException">The file system is read-only</exception>
		/// <exception cref="IOException">Document or collection with the same name already exists</exception>
		public BinsyncDirectory CreateCollection(string name)
		{
			if (BinsyncFS.IsReadOnly)
				throw new UnauthorizedAccessException("Failed to modify a read-only file system");

			checkName(name);

			ETag = new EntityTag(false);

			var engine = Program.BinsyncEngine;
			var next = _isRoot ? "" : System.Net.WebUtility.UrlDecode(this.Path.ToString());
			if (next.Length > 0 && next.Substring(next.Length - 1, 1) == "/") next = next.Substring(0, next.Length - 1);
			next = next + "/" + name;
			if (next.Length > 0 && next.Substring(next.Length - 1, 1) == "/") next = next.Substring(0, next.Length - 1);
			next = "/" + next;

			engine.NewDirectory(next).Wait();
			return new BinsyncDirectory(BinsyncFS, this, Path.AppendDirectory(name), name);

			//throw new NotImplementedException();
			/*
			if (BinsyncFS.IsReadOnly)
				throw new UnauthorizedAccessException("Failed to modify a read-only file system");
			if (_children.ContainsKey(name))
				throw new IOException("Document or collection with the same name already exists");
			var newItem = new BinsyncDirectory(BinsyncFS, this, Path.AppendDirectory(name), name);
			_children.Add(newItem.Name, newItem);
			ETag = new EntityTag(false);
			return newItem;
			*/
		}

		internal bool Remove(string name)
		{
			throw new NotImplementedException();
			/*
			if (BinsyncFS.IsReadOnly)
				throw new UnauthorizedAccessException("Failed to modify a read-only file system");
			return _children.Remove(name);
			*/
		}
	}
}
