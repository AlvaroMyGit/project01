using System.Collections;

namespace StalkerALifeSandbox.Entities.Characters;

/// <summary>Thread-safe registry of world corpses with periodic purge support.</summary>
public sealed class CorpseRegistry : IEnumerable<Corpse>
{
    private readonly List<Corpse> _corpses = new();
    private readonly object _lock = new();

    public int Count
    {
        get { lock (_lock) return _corpses.Count; }
    }

    public void Add(Corpse corpse)
    {
        lock (_lock) _corpses.Add(corpse);
    }

    /// <summary>Remove bodies that exceeded their despawn timers.</summary>
    public int Purge(Func<Corpse, bool> shouldRemove)
    {
        lock (_lock)
            return _corpses.RemoveAll(c => shouldRemove(c));
    }

    public IEnumerator<Corpse> GetEnumerator()
    {
        lock (_lock) return _corpses.ToList().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
