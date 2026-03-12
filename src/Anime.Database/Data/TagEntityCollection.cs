using Anime.Database.Entities;
using Anime.UUID.Tool;

namespace Anime.Database.Data;

public static class TagEntityCollection
{
	private static readonly Guid Tag1Guid = GenerateUuid.GetGuidFromString("tag1");
	public static readonly TagEntity Tag1 = new()
	{
		Id = Tag1Guid, Name = "tag1",

		CreatedAt = new DateTime(2026, 3, 12, 0, 0, 0, DateTimeKind.Utc),
		CreatorId = ProfileEntityCollection.SystemGuid,
		ProfileId = ProfileEntityCollection.KotonaiGuid,
		AnimeId = AnimeEntityCollection.Anime1Guid,
	};

	private static readonly Guid Tag2Guid = GenerateUuid.GetGuidFromString("tag2");
	public static readonly TagEntity Tag2 = Tag1 with
		{ Id = Tag2Guid, Name = "tag2", };

	private static readonly Guid Tag3Guid = GenerateUuid.GetGuidFromString("tag3");
	public static readonly TagEntity Tag3 = Tag1 with
		{ Id = Tag3Guid, Name = "tag3", AnimeId = AnimeEntityCollection.Anime2Guid };
}
