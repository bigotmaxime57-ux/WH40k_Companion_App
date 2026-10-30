using Microsoft.Extensions.Options;

namespace Wh40kSim.Web;

/// <summary>
/// File d'attente des calculs : au plus <see cref="SimulationOptions.MaxConcurrentSimulations"/> à la fois, les autres
/// attendent dans l'ordre d'arrivée, sans limite de longueur. Chaque demande en attente est prévenue de sa position
/// (1 = la prochaine) à chaque changement ; une demande annulée quitte la file.
/// </summary>
public sealed class SimulationQueue(IOptions<SimulationOptions> options)
{
    private readonly object _lock = new();
    private readonly LinkedList<Waiter> _waiting = new();
    private readonly int _capacity = Math.Max(1, options.Value.MaxConcurrentSimulations);
    private int _running;

    /// <summary>Attend une place de calcul ; la libérer en disposant l'objet rendu.</summary>
    /// <param name="onPosition">Appelé avec la position dans la file tant qu'on attend (jamais si une place est libre).</param>
    /// <exception cref="OperationCanceledException">Si la demande est annulée avant d'obtenir une place.</exception>
    public async Task<IDisposable> AcquireAsync(Action<int> onPosition, CancellationToken cancellationToken)
    {
        Waiter waiter;
        lock (_lock)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_running < _capacity && _waiting.Count == 0)
            {
                _running++;
                return new Slot(this);
            }
            waiter = new Waiter(onPosition);
            waiter.Node = _waiting.AddLast(waiter);
            onPosition(_waiting.Count);
        }

        await using (cancellationToken.Register(() => Leave(waiter)))
        {
            await waiter.Ready.Task;
        }
        return new Slot(this);
    }

    /// <summary>Demande annulée en attente : elle quitte la file, les suivantes avancent.</summary>
    private void Leave(Waiter waiter)
    {
        lock (_lock)
        {
            if (waiter.Node?.List is null)
            {
                return; // place déjà attribuée : le calcul s'arrêtera de lui-même (jeton annulé)
            }
            _waiting.Remove(waiter.Node);
            NotifyPositions();
        }
        waiter.Ready.TrySetCanceled();
    }

    /// <summary>Place libérée : elle passe directement à la première demande en attente.</summary>
    private void Release()
    {
        Waiter? next = null;
        lock (_lock)
        {
            if (_waiting.First is { } first)
            {
                next = first.Value;
                _waiting.RemoveFirst();
                NotifyPositions();
            }
            else
            {
                _running--;
            }
        }
        next?.Ready.TrySetResult();
    }

    private void NotifyPositions()
    {
        int position = 1;
        foreach (var waiter in _waiting)
        {
            waiter.OnPosition(position++);
        }
    }

    private sealed class Waiter(Action<int> onPosition)
    {
        public Action<int> OnPosition { get; } = onPosition;

        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public LinkedListNode<Waiter>? Node { get; set; }
    }

    private sealed class Slot(SimulationQueue queue) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                queue.Release();
            }
        }
    }
}
