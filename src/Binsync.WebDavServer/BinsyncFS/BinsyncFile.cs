using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using FubarDev.WebDavServer;
using FubarDev.WebDavServer.Model;
using FubarDev.WebDavServer.Model.Headers;
using FubarDev.WebDavServer.FileSystem;

namespace Binsync.WebDavServer
{
	/// <summary>
	/// An in-memory implementation of a WebDAV document
	/// </summary>
	public class BinsyncFile : BinsyncEntry, IDocument
	{
		string pathString;

		/// <summary>
		/// Initializes a new instance of the <see cref="BinsyncFile"/> class.
		/// </summary>
		/// <param name="fileSystem">The file system this document belongs to</param>
		/// <param name="parent">The parent collection</param>
		/// <param name="path">The root-relative path of this document</param>
		/// <param name="name">The name of this document</param>
		public BinsyncFile(BinsyncFileSystem fileSystem, ICollection parent, Uri path, string name, long? fileSize = null)
			: /* this */base(fileSystem, parent, path, name/*, new byte[0]*/)
		{
			Length = fileSize ?? -1;

			var p = System.Net.WebUtility.UrlDecode(this.Path.ToString());
			if (p.Length > 0 && p.Substring(p.Length - 1, 1) == "/") p = p.Substring(0, p.Length - 1);
			pathString = p;
		}

		/// <inheritdoc />
		public long Length { get; set; }// => Data.Length;

		/// <summary>
		/// Gets or sets the underlying data
		/// </summary>
		public MemoryStream Data { get; set; }

		/// <inheritdoc />
		public override Task<DeleteResult> DeleteAsync(CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
			/*
			if (BinsyncFS.IsReadOnly)
				throw new UnauthorizedAccessException("Failed to modify a read-only file system");

			if (BinsyncParent == null)
				throw new InvalidOperationException("The document must belong to a collection");

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

		/// <inheritdoc />
		public Task<Stream> OpenReadAsync(CancellationToken cancellationToken)
		{
			return Task.FromResult<Stream>(new Binsync.Util.BinsyncStream(Program.BinsyncEngine, pathString, Length, cancellationToken));
		}

		/// <inheritdoc />
		public Task<Stream> CreateAsync(CancellationToken cancellationToken)
		{
			if (BinsyncFS.IsReadOnly)
				throw new UnauthorizedAccessException("Failed to modify a read-only file system");

			return Task.FromResult<Stream>(/* Data = */new Binsync.Util.BinsyncStream2(Program.BinsyncEngine, pathString, cancellationToken));
		}

		/// <inheritdoc />
		public Task<IDocument> CopyToAsync(ICollection collection, string name, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
			/*
			var coll = (BinsyncDirectory)collection;
			coll.Remove(name);

			var doc = (BinsyncFile)await coll.CreateDocumentAsync(name, cancellationToken).ConfigureAwait(false);
			doc.Data = new MemoryStream(Data.ToArray());
			doc.CreationTimeUtc = CreationTimeUtc;
			doc.LastWriteTimeUtc = LastWriteTimeUtc;
			doc.ETag = ETag;

			var sourcePropStore = FileSystem.PropertyStore;
			var destPropStore = collection.FileSystem.PropertyStore;
			if (sourcePropStore != null && destPropStore != null)
			{
				var sourceProps = await sourcePropStore.GetAsync(this, cancellationToken).ConfigureAwait(false);
				await destPropStore.RemoveAsync(doc, cancellationToken).ConfigureAwait(false);
				await destPropStore.SetAsync(doc, sourceProps, cancellationToken).ConfigureAwait(false);
			}
			else if (destPropStore != null)
			{
				await destPropStore.RemoveAsync(doc, cancellationToken).ConfigureAwait(false);
			}

			return doc;
			*/
		}

		/// <inheritdoc />
		public Task<IDocument> MoveToAsync(ICollection collection, string name, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
			/*
			if (BinsyncFS.IsReadOnly)
				throw new UnauthorizedAccessException("Failed to modify a read-only file system");

			var sourcePropStore = FileSystem.PropertyStore;
			var destPropStore = collection.FileSystem.PropertyStore;

			IReadOnlyCollection<XElement> sourceProps;
			if (sourcePropStore != null && destPropStore != null)
			{
				sourceProps = await sourcePropStore.GetAsync(this, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				sourceProps = null;
			}

			var coll = (BinsyncDirectory)collection;
			var doc = (BinsyncFile)await coll.CreateDocumentAsync(name, cancellationToken).ConfigureAwait(false);
			doc.Data = new MemoryStream(Data.ToArray());
			doc.CreationTimeUtc = CreationTimeUtc;
			doc.LastWriteTimeUtc = LastWriteTimeUtc;
			doc.ETag = ETag;
			Debug.Assert(BinsyncParent != null, "BinsyncParent != null");
			if (BinsyncParent == null)
				throw new InvalidOperationException("The document must belong to a collection");
			if (!BinsyncParent.Remove(Name))
				throw new InvalidOperationException("Failed to remove the document from the source collection.");

			if (destPropStore != null)
			{
				await destPropStore.RemoveAsync(doc, cancellationToken).ConfigureAwait(false);

				if (sourceProps != null)
				{
					await destPropStore.SetAsync(doc, sourceProps, cancellationToken).ConfigureAwait(false);
				}
			}

			return doc;
			*/
		}
	}
}
