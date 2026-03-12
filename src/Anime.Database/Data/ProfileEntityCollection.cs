using Anime.Database.Entities;
using Anime.UUID.Tool;

namespace Anime.Database.Data;

public static class ProfileEntityCollection
{
	public static readonly Guid SystemGuid = GenerateUuid.GetGuidFromString("system");
	public static readonly ProfileEntity SystemProfile = new()
	{
		Id = SystemGuid, NickName = "system",

		CreatedAt = new DateTime(2026, 3, 12, 0, 0, 0, DateTimeKind.Utc),
		CreatorId = SystemGuid
	};

	public static readonly Guid KotonaiGuid = GenerateUuid.GetGuidFromString("kotonai");
	public static readonly ProfileEntity KotonaiProfile = SystemProfile with
		{ Id = KotonaiGuid, NickName = "kotonai" };
}
