using System.Collections.Concurrent;
using TinyNet.Example.Models;

namespace TinyNet.Example.Services;

public class NoteStore
{
    private readonly ConcurrentDictionary<int, Note> _notes = new();
    private int _lastId;

    public IReadOnlyList<Note> List(int take)
        => _notes.Values.OrderBy(n => n.Id).Take(take).ToList();

    public Note Add(NoteInput input)
    {
        var note = new Note(Interlocked.Increment(ref _lastId), input.Title, input.Text);
        _notes[note.Id] = note;
        return note;
    }

    public Note? Find(int id)
        => _notes.GetValueOrDefault(id);

    public Note? Update(int id, Func<Note, Note> change)
    {
        while (_notes.TryGetValue(id, out var current))
        {
            var updated = change(current) with { Id = id };
            if (_notes.TryUpdate(id, updated, current))
                return updated;
        }

        return null;
    }

    public bool Remove(int id)
        => _notes.TryRemove(id, out _);
}