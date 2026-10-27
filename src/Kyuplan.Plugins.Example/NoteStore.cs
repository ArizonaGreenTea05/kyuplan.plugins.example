namespace Kyuplan.Plugins.Example;

/// <summary>A note of the example page. The page shows these; a real plugin shows whatever it works with.</summary>
public sealed record Note(int Id, string Title, string Category, int Priority, bool Pinned, string? Details, DateTimeOffset Created);

/// <summary>
/// Where the notes of the example page are kept: in memory, so the template does not decide for you where your data lives. They are gone when the API restarts
/// (every change of the settings or of the plugins does that). Replace this class with a database or a file of your own when yours must last.
/// The endpoints may run at the same time, so every method takes the lock.
/// </summary>
public sealed class NoteStore
{
    /// <summary>The most notes kept: a body comes from a browser, so what it adds must have a limit.</summary>
    public const int MaxNotes = 200;

    private readonly Lock _lock = new();
    private readonly List<Note> _notes = [];
    private int _nextId = 1;

    /// <summary>A store with two notes in it, so the page is not empty the first time it is opened.</summary>
    public static NoteStore WithSamples(DateTimeOffset now)
    {
        var store = new NoteStore();
        store.Add("Read the page tour", "idea", 2, pinned: true, "docs/anatomy.md explains every file of this plugin.", now);
        store.Add("Make it yours", "todo", 4, pinned: false, null, now);
        return store;
    }

    /// <summary>The notes, pinned first, then by priority (1 is the most urgent) and age.</summary>
    public IReadOnlyList<Note> All()
    {
        lock (_lock) return [.. _notes.OrderByDescending(n => n.Pinned).ThenBy(n => n.Priority).ThenBy(n => n.Id)];
    }

    /// <summary>Adds a note; <c>null</c> when the store is full.</summary>
    public Note? Add(string title, string category, int priority, bool pinned, string? details, DateTimeOffset now)
    {
        lock (_lock)
        {
            if (_notes.Count >= MaxNotes) return null;

            var note = new Note(_nextId++, title, category, priority, pinned, details, now);
            _notes.Add(note);
            return note;
        }
    }

    /// <summary>Pins the note, or unpins it when it is pinned; <c>null</c> when there is no such note.</summary>
    public Note? TogglePin(int id)
    {
        lock (_lock)
        {
            var index = _notes.FindIndex(n => n.Id == id);
            if (index < 0) return null;

            return _notes[index] = _notes[index] with { Pinned = !_notes[index].Pinned };
        }
    }

    public void Clear()
    {
        lock (_lock) _notes.Clear();
    }
}
