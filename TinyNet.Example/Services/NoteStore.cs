using System.Collections.Concurrent;
using TinyNet.Example.Models;

namespace TinyNet.Example.Services;

public class NoteStore
{
    private readonly ConcurrentDictionary<int, Note> _notes = new();
    private int _lastId;

    public IReadOnlyList<Note> All()
        => _notes.Values.OrderBy(n => n.Id).ToList();

    public void Add(string title, string text)
    {
        var id = Interlocked.Increment(ref _lastId);
        _notes[id] = new Note(id, title, text);
    }

    public bool Remove(int id)
        => _notes.TryRemove(id, out _);
}