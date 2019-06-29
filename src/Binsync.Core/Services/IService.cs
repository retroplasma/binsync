using System;

namespace Binsync.Core.Services
{
	public interface IService
	{
		bool Connected { get; }

		bool Connect();

		byte[] GetSubject(string id);
		byte[] GetBody(string id);

		bool Upload(Chunk chunk);
	}
}