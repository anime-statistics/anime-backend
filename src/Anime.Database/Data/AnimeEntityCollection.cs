using Anime.Database.Entities;
using Anime.UUID.Tool;

namespace Anime.Database.Data;

public static class AnimeEntityCollection
{
	public static readonly Guid Anime1Guid = GenerateUuid.GetGuidFromString("anime1");
	public static readonly AnimeEntity Anime1 = new()
	{
		Id = Anime1Guid, Title = "anime1",

		CreatedAt = new DateTime(2026, 3, 12, 0, 0, 0, DateTimeKind.Utc),
		CreatorId = ProfileEntityCollection.SystemGuid,
		ProfileId = ProfileEntityCollection.KotonaiGuid,
	};

	public static readonly Guid Anime2Guid = GenerateUuid.GetGuidFromString("anime2");
	public static readonly AnimeEntity Anime2 = Anime1 with
		{ Id = Anime2Guid, Title = "anime2" };
}
