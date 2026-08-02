using AnimeBackend.Application.Abstractions;
using AnimeBackend.Domain;
using AnimeBackend.Domain.Media;
using Microsoft.AspNetCore.Mvc;

namespace AnimeBackend.Api.Controllers;

public sealed record SyncCommitRequest(IReadOnlyList<object>? Changes);

// The sync model is acknowledged as poor in the contract and will be
// redesigned together with tag triggers. These endpoints keep the settings
// page functional: the connection check is real, the queue is honestly empty.
[ApiController]
[Route("api/v1")]
public sealed class SyncController(IEnumerable<ISourceClient> clients) : ControllerBase
{
    [HttpPost("integrations/{service}/check")]
    public async Task<IActionResult> CheckIntegration(string service, CancellationToken ct)
    {
        var source = MediaSourceExtensions.FromWire(service);
        if (source is null)
            throw new NotFoundException("Неизвестный сервис");

        var client = clients.First(c => c.Source == source);
        try
        {
            // A cheap catalogue query doubles as a connectivity probe (and
            // warms the search cache).
            await client.SearchAsync("", MediaType.Anime, ct);
            return Ok(new { Connected = true });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return Ok(new { Connected = false });
        }
    }

    [HttpGet("sync/pending")]
    public IActionResult Pending()
        => Ok(new { Items = Array.Empty<object>(), Total = 0 });

    [HttpPost("sync/commit")]
    public IActionResult Commit([FromBody] SyncCommitRequest request)
    {
        if (request.Changes is null)
            throw new DomainException("Не переданы изменения для синхронизации");
        return Ok(new { Committed = request.Changes.Count });
    }
}
