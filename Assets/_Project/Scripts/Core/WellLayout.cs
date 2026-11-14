using System.Collections.Generic;
using UnityEngine;

namespace GravityWells.Core
{
    // The wells the player has put on the field, and the edits that got them there: place, move, remove,
    // undo, restart. Pure, so the drag controller only translates fingers into these calls and every rule
    // (budget, where a well may go, what Undo reverses) is tested without a scene.
    //
    // A drag is one edit. Dragging a well off the tray, or picking one up, changes the layout live (the
    // preview must follow the finger) but is only written to the undo history when the finger lifts.
    public sealed class WellLayout
    {
        private enum EditKind
        {
            Place,
            Move,
            Remove,
        }

        private readonly struct Edit
        {
            public readonly EditKind Kind;
            public readonly int Index;
            public readonly Well Before;   // for Move and Remove
            public readonly Well After;    // for Place and Move

            public Edit(EditKind kind, int index, Well before, Well after)
            {
                Kind = kind;
                Index = index;
                Before = before;
                After = after;
            }
        }

        private readonly Well[] _wells = new Well[GameConfig.MaxWells];
        private readonly Well[] _simulation = new Well[GameConfig.MaxWells];
        private readonly List<Edit> _history = new List<Edit>();
        private LevelDefinition _level;
        private int _dragIndex = -1;
        private bool _dragIsNew;
        private Well _dragBefore;

        public int Count { get; private set; }
        public Well this[int index] => _wells[index];

        // The wells to fly with: everything placed, except a well being dragged into the removal zone.
        public Well[] SimulationWells => _simulation;
        public int SimulationCount { get; private set; }

        public bool IsDragging => _dragIndex >= 0;
        public int DraggedIndex => _dragIndex;

        // True while the finger holds the dragged well somewhere dropping it would remove it (over the tray,
        // or off the field). The preview then shows the flight without it.
        public bool DragWillRemove { get; private set; }

        public bool CanUndo => _history.Count > 0 && !IsDragging;

        public int RepulsorsPlaced
        {
            get
            {
                int repulsors = 0;
                for (int i = 0; i < Count; i++)
                {
                    repulsors += _wells[i].Kind == WellKind.Repulsor ? 1 : 0;
                }
                return repulsors;
            }
        }

        public void Begin(LevelDefinition level)
        {
            _level = level;
            Clear();
        }

        // Restart: no wells, no history, no drag in progress.
        public void Clear()
        {
            Count = 0;
            _history.Clear();
            _dragIndex = -1;
            DragWillRemove = false;
            RefreshSimulation();
        }

        // Wells of a kind the player can still put down. An attractor may stand in for any well, so its
        // count is the total left; repulsors are also capped by the level's repulsor budget.
        public int Remaining(WellKind kind)
        {
            int left = _level.WellBudget - Count;
            if (kind == WellKind.Repulsor)
            {
                left = Mathf.Min(left, _level.RepulsorBudget - RepulsorsPlaced);
            }
            return Mathf.Max(0, left);
        }

        public bool CanAdd(WellKind kind) => Remaining(kind) > 0 && Count < _wells.Length;

        // Index of the placed well nearest to the point within the radius, or -1.
        public int NearestWell(Vector2 point, float radius)
        {
            float best = radius * radius;
            int found = -1;
            for (int i = 0; i < Count; i++)
            {
                float dx = _wells[i].Position.x - point.x;
                float dy = _wells[i].Position.y - point.y;
                float sqr = dx * dx + dy * dy;
                if (sqr <= best)
                {
                    best = sqr;
                    found = i;
                }
            }
            return found;
        }

        // ---- a drag ----

        // Takes a well off the tray. It counts against the budget from now on. False if none is left.
        public bool BeginDragNew(WellKind kind, Vector2 position)
        {
            if (IsDragging || !CanAdd(kind))
            {
                return false;
            }
            _wells[Count] = new Well(WellRules.Clamp(_level, position), kind);
            _dragIndex = Count;
            _dragIsNew = true;
            Count++;
            DragWillRemove = false;
            RefreshSimulation();
            return true;
        }

        // Picks up a placed well.
        public bool BeginDragExisting(int index)
        {
            if (IsDragging || index < 0 || index >= Count)
            {
                return false;
            }
            _dragIndex = index;
            _dragIsNew = false;
            _dragBefore = _wells[index];
            DragWillRemove = false;
            RefreshSimulation();
            return true;
        }

        // Moves the dragged well. removeZone says the finger is somewhere letting go would remove it.
        public void DragTo(Vector2 position, bool removeZone)
        {
            if (!IsDragging)
            {
                return;
            }
            Well well = _wells[_dragIndex];
            _wells[_dragIndex] = new Well(WellRules.Clamp(_level, position), well.Kind);
            DragWillRemove = removeZone;
            RefreshSimulation();
        }

        // Lets go. A new well dropped in the removal zone just vanishes (it never happened); a placed one
        // dropped there is removed, which Undo brings back. Otherwise the place or move is recorded.
        public void EndDrag()
        {
            if (!IsDragging)
            {
                return;
            }

            int index = _dragIndex;
            Well well = _wells[index];
            bool remove = DragWillRemove;
            _dragIndex = -1;
            DragWillRemove = false;

            if (_dragIsNew)
            {
                if (remove)
                {
                    RemoveAt(index);
                }
                else
                {
                    _history.Add(new Edit(EditKind.Place, index, default, well));
                }
            }
            else if (remove)
            {
                _wells[index] = _dragBefore;
                _history.Add(new Edit(EditKind.Remove, index, _dragBefore, default));
                RemoveAt(index);
            }
            else if (well.Position != _dragBefore.Position)
            {
                _history.Add(new Edit(EditKind.Move, index, _dragBefore, well));
            }
            RefreshSimulation();
        }

        // Abandons a drag as if it had not started (input cut off mid-drag): a placed well goes back where it
        // was, a new one returns to the tray.
        public void CancelDrag()
        {
            if (!IsDragging)
            {
                return;
            }
            if (_dragIsNew)
            {
                RemoveAt(_dragIndex);
            }
            else
            {
                _wells[_dragIndex] = _dragBefore;
            }
            _dragIndex = -1;
            DragWillRemove = false;
            RefreshSimulation();
        }

        // ---- direct edits (the hint, tests) ----

        // Puts a well down at once, as one undoable edit. Returns its index, or -1 if none is left.
        public int Place(WellKind kind, Vector2 position)
        {
            if (IsDragging || !CanAdd(kind))
            {
                return -1;
            }
            Well well = new Well(WellRules.Clamp(_level, position), kind);
            _wells[Count] = well;
            _history.Add(new Edit(EditKind.Place, Count, default, well));
            Count++;
            RefreshSimulation();
            return Count - 1;
        }

        public void MoveTo(int index, Vector2 position)
        {
            if (IsDragging || index < 0 || index >= Count)
            {
                return;
            }
            Well before = _wells[index];
            var after = new Well(WellRules.Clamp(_level, position), before.Kind);
            if (after.Position == before.Position)
            {
                return;
            }
            _wells[index] = after;
            _history.Add(new Edit(EditKind.Move, index, before, after));
            RefreshSimulation();
        }

        // Reverses the last place, move or remove.
        public bool Undo()
        {
            if (!CanUndo)
            {
                return false;
            }

            Edit edit = _history[_history.Count - 1];
            _history.RemoveAt(_history.Count - 1);
            switch (edit.Kind)
            {
                case EditKind.Place:
                    RemoveAt(edit.Index);
                    break;
                case EditKind.Move:
                    _wells[edit.Index] = edit.Before;
                    break;
                default:
                    InsertAt(edit.Index, edit.Before);
                    break;
            }
            RefreshSimulation();
            return true;
        }

        private void RemoveAt(int index)
        {
            for (int i = index; i < Count - 1; i++)
            {
                _wells[i] = _wells[i + 1];
            }
            Count--;
        }

        private void InsertAt(int index, Well well)
        {
            for (int i = Count; i > index; i--)
            {
                _wells[i] = _wells[i - 1];
            }
            _wells[index] = well;
            Count++;
        }

        private void RefreshSimulation()
        {
            int count = 0;
            for (int i = 0; i < Count; i++)
            {
                if (i == _dragIndex && DragWillRemove)
                {
                    continue;
                }
                _simulation[count++] = _wells[i];
            }
            SimulationCount = count;
        }
    }
}
