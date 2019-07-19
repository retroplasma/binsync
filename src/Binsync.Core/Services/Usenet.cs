using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using NntpClientLib;
using yEnc;
using Binsync.Core.Helpers;

namespace Binsync.Core.Services
{
	public class Usenet : IService
	{
		Rfc977NntpClientWithExtensions client;

		string username;
		string password;
		string serverAddress;
		bool useSSL;
		UInt16 port;
		string newsgroup;
		string postFromUser;

		public Usenet(string username, string password, string serverAddress, bool useSSL, UInt16 port, string newsgroup, string postFromUser)
		{
			this.username = username;
			this.password = password;
			this.serverAddress = serverAddress;
			this.useSSL = useSSL;
			this.port = port;
			this.newsgroup = newsgroup;
			this.postFromUser = postFromUser;
		}

		public bool Connected
		{
			get
			{
				if (client == null)
					return false;

				// polling?

				return client.Connected;
			}
		}

		public bool Connect()
		{
			try
			{
				client = ConnectUsenet();
				return true;
			}
			catch (Exception ex)
			{
				throw ex;
			}
		}

		public byte[] GetSubject(string id)
		{
			var nntpClient = client;
			try
			{
				var str = nntpClient.RetrieveArticleHeader(IdToMessageId(id))["Subject"][0];
				return str.GetBytesUTF8();
			}
			catch (Exception ex)
			{
				if (nntpClient.LastNntpResponse.StartsWith("430 No Such Article"))
				{
					return null;
				}
				throw ex;
			}
		}

		public byte[] GetBody(string id)
		{
			var nntpClient = client;

			try
			{
				using (MemoryStream ms = new MemoryStream())
				{
					var crc = new byte[4];
					Download(nntpClient.RetrieveArticleBody(IdToMessageId(id)), ref crc, ms);
					var rawData = ms.ToArray();

					return rawData;
				}
			}
			catch (Exception ex)
			{
				if (nntpClient.LastNntpResponse.StartsWith("430 No Such Article"))
				{
					return null;
				}
				throw ex;
			}
		}

		public bool Upload(Chunk chunk)
		{
			ArticleHeadersDictionary headers = CreateHeader(IdToMessageId(chunk.ID), chunk.Subject);

			Rfc977NntpClientWithExtensions nntpClient = client;

			var crcv = new CRCValue();

			var rawData = chunk.Data;

			try
			{
				using (MemoryStream ms = new MemoryStream(rawData))
				{
					nntpClient.PostArticle(new ArticleHeadersDictionaryEnumerator(headers), Upload(rawData.Length, crcv, ms));
				}
			}
			catch (Exception ex)
			{
				if (nntpClient.LastNntpResponse.StartsWith("441 Posting Failed"))
				{
					return false;
				}
				throw ex;
			}

			try
			{
				if (!nntpClient.LastNntpResponse.StartsWith("240 Article Posted") && nntpClient.LastNntpResponse.Split('<')[1].Split('>')[0] == null)
				{
					throw new Exception(nntpClient.LastNntpResponse);
				}
			}
			catch
			{
				throw new Exception(nntpClient.LastNntpResponse);
			}

			return true;
		}

		public void Download(IEnumerable<string> body, ref byte[] crchash, Stream outputStream)
		{
			byte[] buffer = new byte[4096];
			using (MemoryStream tmpStream = new MemoryStream())
			{
				tmpStream.Position = 0;
				StreamWriter sw = new StreamWriter(tmpStream, System.Text.Encoding.GetEncoding("ISO-8859-1"));

				foreach (string line in body)
				{
					sw.WriteLine(line);
				}

				sw.Flush();
				tmpStream.Position = 0;

				YEncDecoder yencDecoder = new YEncDecoder();

				using (CryptoStream yencStream = new CryptoStream(tmpStream, yencDecoder, CryptoStreamMode.Read))
				{
					int read = 0;
					while ((read = yencStream.Read(buffer, 0, buffer.Length)) > 0)
					{
						outputStream.Write(buffer, 0, read);
					}
				}
				crchash = yencDecoder.CRCHash;
			}
		}

		public class CRCValue
		{
			public byte[] Crc32;
		}

		public IEnumerable<string> Upload(int size, CRCValue crcValue, Stream inputStream)
		{
			var yencEncoder = new YEncEncoder();
			var buffer = new byte[4096];

			using (MemoryStream tmpStream = new MemoryStream((int)(inputStream.Length)))
			{
				using (CryptoStream yencStream = new CryptoStream(tmpStream, yencEncoder, CryptoStreamMode.Write))
				{
					int toRead = size;

					inputStream.Position = 0;

					for (int i = 0; i < toRead / buffer.Length; i++)
					{
						inputStream.Read(buffer, 0, buffer.Length);
						yencStream.Write(buffer, 0, buffer.Length);
					}
					if (toRead % buffer.Length != 0)
					{
						inputStream.Read(buffer, 0, toRead % buffer.Length);
						yencStream.Write(buffer, 0, toRead % buffer.Length);
					}

					tmpStream.Position = 0;
					StreamReader sr = new StreamReader(tmpStream, System.Text.Encoding.GetEncoding("ISO-8859-1"));

					while (sr.Peek() >= 0)
					{
						yield return sr.ReadLine();
					}

				}
				crcValue.Crc32 = yencEncoder.CRCHash;
			}
		}

		public Rfc977NntpClientWithExtensions ConnectUsenet()
		{
			var client1 = new Rfc977NntpClientWithExtensions();
			client1.Connect(this.serverAddress, this.port, this.useSSL);
			if (this.username != null && this.password != null)
			{
				client1.AuthenticateUser(this.username, this.password);
			}

			var newsgroup = this.newsgroup;
			client1.SelectNewsgroup(newsgroup);

			return client1;
		}

		public string IdToMessageId(string id)
		{
			var res = $"<{id.Substring(0, id.Length - 8)}@{id.Substring(id.Length - 8) }.local>";
			return res;
		}

		public ArticleHeadersDictionary CreateHeader(string messageId, string subject)
		{
			var headers = new ArticleHeadersDictionary();

			headers.AddHeader("From", postFromUser);
			headers.AddHeader("Subject", subject);
			headers.AddHeader("Newsgroups", this.newsgroup);
			//headers.AddHeader("Date", new NntpDateTime(DateTime.Now).ToString());
			headers.AddHeader("Message-ID", messageId);

			return headers;
		}
	}
}
