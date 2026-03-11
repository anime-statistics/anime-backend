
# База данных

## Миграции

```text
dotnet ef migrations add <NAME> --project src/Anime.Database --startup-project src/Anime.Server
dotnet ef database update --project src/Anime.Database --startup-project src/Anime.Server
```
