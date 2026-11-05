namespace DesmosSharp.App.Services;

public sealed record ExpressionSnapshot(string Latex, string ColorHex, bool IsVisible);

public sealed record DocumentSnapshot(IReadOnlyList<ExpressionSnapshot> Expressions);

/// <summary>
/// 快照式撤销栈。连续输入同一个输入框时通过 mergeKey 合并为一次撤销。
/// </summary>
public sealed class UndoManager
{
    private readonly List<UndoEntry> _undo = [];
    private readonly List<UndoEntry> _redo = [];

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public void Push(DocumentSnapshot snapshot, string? mergeKey = null)
    {
        _redo.Clear();

        if (mergeKey is not null && _undo.Count > 0 && _undo[^1].MergeKey == mergeKey)
        {
            return;
        }

        _undo.Add(new UndoEntry(snapshot, mergeKey));
    }

    public DocumentSnapshot? Undo(DocumentSnapshot current)
    {
        if (_undo.Count == 0)
        {
            return null;
        }

        var entry = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(new UndoEntry(current, null));

        return entry.Snapshot;
    }

    public DocumentSnapshot? Redo(DocumentSnapshot current)
    {
        if (_redo.Count == 0)
        {
            return null;
        }

        var entry = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(new UndoEntry(current, null));

        return entry.Snapshot;
    }

    private readonly record struct UndoEntry(DocumentSnapshot Snapshot, string? MergeKey);
}
