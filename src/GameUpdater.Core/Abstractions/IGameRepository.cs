using GameUpdater.Shared.Models;

namespace GameUpdater.Core.Abstractions;

public interface IGameRepository
{
    Task<IReadOnlyList<GameRecord>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<GameRecord?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<int> InsertAsync(GameRecord game, CancellationToken cancellationToken = default);

    Task UpdateAsync(GameRecord game, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

