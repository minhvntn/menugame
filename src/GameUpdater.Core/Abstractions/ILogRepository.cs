using GameUpdater.Shared.Models;

namespace GameUpdater.Core.Abstractions;

public interface ILogRepository
{
    Task AddAsync(UpdateLogEntry entry, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UpdateLogEntry>> GetRecentAsync(int limit = 200, CancellationToken cancellationToken = default);

    Task ClearAllAsync(CancellationToken cancellationToken = default);
}

