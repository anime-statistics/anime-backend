using System.Security.Cryptography;
using System.Text;

namespace Anime.UUID.Tool;

public static class GenerateUuid
{
	public static Guid GetGuidFromString(string buffer)
	{
		var bytes = Encoding.UTF8.GetBytes(buffer);
		var hash = MD5.HashData(bytes);

		var guid = new Guid(hash);
		return guid;
	}
}
