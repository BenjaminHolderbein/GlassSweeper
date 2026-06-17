namespace GlassSweeper.Core;

/// <summary>The lifecycle state of a game.</summary>
public enum GameState
{
    Playing,
    Won,
    Lost,
}

/// <summary>The user-applied mark on an unrevealed cell.</summary>
public enum CellMark
{
    None,
    Flag,
    Question,
}

/// <summary>
/// A single board cell. A value type carrying only data, ported faithfully
/// from SwiftSweeperKit's <c>Cell</c> struct — no behavior beyond derived flags.
/// </summary>
public struct Cell : IEquatable<Cell>
{
    public bool IsMine;
    public bool IsRevealed;
    public bool IsExploded;
    public CellMark Mark;
    public int NeighboringMines;

    public readonly bool IsFlagged => Mark == CellMark.Flag;
    public readonly bool IsQuestioned => Mark == CellMark.Question;

    public readonly bool Equals(Cell other) =>
        IsMine == other.IsMine
        && IsRevealed == other.IsRevealed
        && IsExploded == other.IsExploded
        && Mark == other.Mark
        && NeighboringMines == other.NeighboringMines;

    public override readonly bool Equals(object? obj) => obj is Cell other && Equals(other);

    public override readonly int GetHashCode() =>
        HashCode.Combine(IsMine, IsRevealed, IsExploded, Mark, NeighboringMines);

    public static bool operator ==(Cell left, Cell right) => left.Equals(right);

    public static bool operator !=(Cell left, Cell right) => !left.Equals(right);
}
