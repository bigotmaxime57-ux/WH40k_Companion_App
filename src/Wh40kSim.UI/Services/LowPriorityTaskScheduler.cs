using System.Collections.Concurrent;
using System.Runtime.Versioning;

namespace Wh40kSim.UI.Services;

/// <summary>
/// Ordonnanceur à threads dédiés en priorité basse : Windows sert d'abord l'interface et les autres programmes, le
/// calcul prend ce qui reste. Threads d'arrière-plan, créés une fois et gardés jusqu'à la fermeture de l'appli.
/// Indisponible dans le navigateur (pas de threads).
/// </summary>
[UnsupportedOSPlatform("browser")]
internal sealed class LowPriorityTaskScheduler : TaskScheduler
{
    private readonly BlockingCollection<Task> _tasks = new();

    [ThreadStatic]
    private static bool _isWorker;

    public LowPriorityTaskScheduler(int threadCount)
    {
        MaximumConcurrencyLevel = threadCount;
        for (int i = 0; i < threadCount; i++)
        {
            new Thread(Work) { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = $"Wh40kSim calcul {i + 1}" }.Start();
        }
    }

    public override int MaximumConcurrencyLevel { get; }

    private void Work()
    {
        _isWorker = true;
        foreach (var task in _tasks.GetConsumingEnumerable())
        {
            TryExecuteTask(task);
        }
    }

    protected override void QueueTask(Task task) => _tasks.Add(task);

    /// <summary>Exécution sur place seulement depuis un de ces threads (jamais sur le thread de l'interface).</summary>
    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => _isWorker && TryExecuteTask(task);

    protected override IEnumerable<Task> GetScheduledTasks() => _tasks.ToArray();
}
