namespace Binsync.Core
{
	public class Chunk
	{
		//subject has no purpose for now

		public string ID { get; private set; }
		public string Subject { get; private set; }
		public byte[] Data { get; private set; }

		public Chunk(string id, string subject, byte[] data)
		{
			ID = id;
			Subject = subject;
			Data = data;
		}
	}
}