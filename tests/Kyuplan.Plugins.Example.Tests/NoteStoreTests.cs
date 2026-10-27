namespace Kyuplan.Plugins.Example.Tests;

public class NoteStoreTests
{
    private static readonly DateTimeOffset Now = ExampleHost.Now;

    private static Note Add(NoteStore store, string title, int priority = 3, bool pinned = false) =>
        store.Add(title, "general", priority, pinned, null, Now)!;

    [Fact]
    public void A_new_store_is_empty()
    {
        Assert.Empty(new NoteStore().All());
    }

    [Fact]
    public void The_samples_are_two_notes_with_the_time_they_were_made()
    {
        var notes = NoteStore.WithSamples(Now).All();

        Assert.Equal(2, notes.Count);
        Assert.All(notes, n => Assert.Equal(Now, n.Created));
    }

    [Fact]
    public void Ids_count_up_and_are_not_reused_after_a_clear()
    {
        var store = new NoteStore();

        var first = Add(store, "a");
        var second = Add(store, "b");
        store.Clear();
        var third = Add(store, "c");

        Assert.Equal([1, 2, 3], [first.Id, second.Id, third.Id]);
    }

    [Fact]
    public void Notes_are_listed_pinned_first_then_the_most_urgent_then_the_oldest()
    {
        var store = new NoteStore();
        Add(store, "late", priority: 3);
        Add(store, "urgent", priority: 1);
        Add(store, "pinned", priority: 5, pinned: true);
        Add(store, "also late", priority: 3);

        Assert.Equal(["pinned", "urgent", "late", "also late"], store.All().Select(n => n.Title));
    }

    [Fact]
    public void Pinning_twice_unpins_and_an_unknown_note_changes_nothing()
    {
        var store = new NoteStore();
        var note = Add(store, "a");

        Assert.True(store.TogglePin(note.Id)!.Pinned);
        Assert.False(store.TogglePin(note.Id)!.Pinned);
        Assert.Null(store.TogglePin(99));
        Assert.Null(store.TogglePin(0));
        Assert.Single(store.All());
    }

    [Fact]
    public void The_store_holds_at_most_the_limit_and_takes_notes_again_after_a_clear()
    {
        var store = new NoteStore();
        for (var i = 0; i < NoteStore.MaxNotes; i++) Add(store, $"n{i}");

        Assert.Null(store.Add("one too many", "general", 3, false, null, Now));
        Assert.Equal(NoteStore.MaxNotes, store.All().Count);

        store.Clear();
        Assert.NotNull(store.Add("again", "general", 3, false, null, Now));
    }

    [Fact]
    public void A_list_that_was_handed_out_does_not_change_when_the_store_does()
    {
        var store = new NoteStore();
        Add(store, "a");
        var before = store.All();

        Add(store, "b");
        store.Clear();

        Assert.Equal(["a"], before.Select(n => n.Title));
    }

    [Fact]
    public void Adding_from_many_threads_gives_every_note_its_own_id()
    {
        var store = new NoteStore();

        Parallel.For(0, NoteStore.MaxNotes, i => Add(store, $"n{i}"));

        var ids = store.All().Select(n => n.Id).ToList();
        Assert.Equal(NoteStore.MaxNotes, ids.Distinct().Count());
        Assert.Equal(Enumerable.Range(1, NoteStore.MaxNotes), ids.Order());
    }
}
