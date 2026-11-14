using NUnit.Framework;
using UnityEngine;
using GravityWells.Core;

namespace GravityWells.Tests
{
    public class WellLayoutTests
    {
        private static LevelDefinition Level(int par = 2, int repulsorBudget = 0)
        {
            return new LevelDefinition(
                1, 1, new Vector2(0.5f, 0.2f), new Vector2(0f, GameConfig.LaunchSpeed), null, new Vector2(0.5f, 1.3f), 0.05f,
                null, par, repulsorBudget, null);
        }

        private static WellLayout Layout(int par = 2, int repulsorBudget = 0)
        {
            var layout = new WellLayout();
            layout.Begin(Level(par, repulsorBudget));
            return layout;
        }

        private static readonly Vector2 A = new Vector2(0.2f, 0.8f);
        private static readonly Vector2 B = new Vector2(0.8f, 0.9f);

        // ---- budget ----

        [Test]
        public void TheBudget_IsParPlusTwo()
        {
            WellLayout layout = Layout(par: 2);
            Assert.AreEqual(4, layout.Remaining(WellKind.Attractor));
            for (int i = 0; i < 4; i++)
            {
                Assert.GreaterOrEqual(layout.Place(WellKind.Attractor, new Vector2(0.1f + 0.2f * i, 1f)), 0);
            }
            Assert.AreEqual(0, layout.Remaining(WellKind.Attractor));
            Assert.IsFalse(layout.CanAdd(WellKind.Attractor));
            Assert.AreEqual(-1, layout.Place(WellKind.Attractor, A));
            Assert.AreEqual(4, layout.Count);
        }

        [Test]
        public void Repulsors_AreCappedByTheirOwnBudget_AndCountAgainstTheTotal()
        {
            WellLayout layout = Layout(par: 2, repulsorBudget: 1);
            Assert.AreEqual(4, layout.Remaining(WellKind.Attractor));
            Assert.AreEqual(1, layout.Remaining(WellKind.Repulsor));

            layout.Place(WellKind.Repulsor, A);
            Assert.AreEqual(3, layout.Remaining(WellKind.Attractor), "a repulsor takes one of the four places");
            Assert.AreEqual(0, layout.Remaining(WellKind.Repulsor));
            Assert.AreEqual(-1, layout.Place(WellKind.Repulsor, B));
        }

        [Test]
        public void WithNoRepulsorBudget_NoRepulsorCanBePlaced()
        {
            Assert.AreEqual(0, Layout(par: 3, repulsorBudget: 0).Remaining(WellKind.Repulsor));
        }

        // ---- where wells go ----

        [Test]
        public void APlacedWell_IsPulledOutOfTheCircleAroundTheStart()
        {
            WellLayout layout = Layout();
            layout.Place(WellKind.Attractor, new Vector2(0.5f, 0.21f));
            Assert.GreaterOrEqual(
                Vector2.Distance(layout[0].Position, new Vector2(0.5f, 0.2f)), GameConfig.MinWellDistanceFromStart);
        }

        [Test]
        public void APlacedWell_IsKeptInsideTheField()
        {
            WellLayout layout = Layout();
            layout.Place(WellKind.Attractor, new Vector2(-3f, 9f));
            Assert.AreEqual(new Vector2(0f, GameConfig.FieldHeight), layout[0].Position);
        }

        // ---- undo ----

        [Test]
        public void Undo_ReversesPlaceMoveAndRemove_InOrder()
        {
            WellLayout layout = Layout();
            layout.Place(WellKind.Attractor, A);
            layout.Place(WellKind.Attractor, B);
            layout.MoveTo(0, new Vector2(0.3f, 1.1f));

            // remove the first well by dragging it away
            Assert.IsTrue(layout.BeginDragExisting(0));
            layout.DragTo(new Vector2(0.3f, 1.1f), removeZone: true);
            layout.EndDrag();
            Assert.AreEqual(1, layout.Count);
            Assert.AreEqual(B, layout[0].Position);

            Assert.IsTrue(layout.Undo()); // the removal
            Assert.AreEqual(2, layout.Count);
            Assert.AreEqual(new Vector2(0.3f, 1.1f), layout[0].Position, "back in its place in the list, where it was moved to");
            Assert.AreEqual(B, layout[1].Position);

            Assert.IsTrue(layout.Undo()); // the move
            Assert.AreEqual(A, layout[0].Position);

            Assert.IsTrue(layout.Undo()); // placing B
            Assert.AreEqual(1, layout.Count);
            Assert.IsTrue(layout.Undo()); // placing A
            Assert.AreEqual(0, layout.Count);
            Assert.IsFalse(layout.Undo());
            Assert.IsFalse(layout.CanUndo);
        }

        [Test]
        public void Restart_ClearsTheWellsAndTheHistory()
        {
            WellLayout layout = Layout();
            layout.Place(WellKind.Attractor, A);
            layout.Place(WellKind.Attractor, B);

            layout.Clear();

            Assert.AreEqual(0, layout.Count);
            Assert.IsFalse(layout.CanUndo);
            Assert.AreEqual(0, layout.SimulationCount);
            Assert.AreEqual(4, layout.Remaining(WellKind.Attractor));
        }

        // ---- drags ----

        [Test]
        public void ADragFromTheTray_IsOneUndoableEdit_AndCountsAgainstTheBudgetWhileItLasts()
        {
            WellLayout layout = Layout();
            Assert.IsTrue(layout.BeginDragNew(WellKind.Attractor, A));
            Assert.AreEqual(3, layout.Remaining(WellKind.Attractor));
            Assert.IsFalse(layout.CanUndo, "mid-drag nothing can be undone");

            layout.DragTo(new Vector2(0.4f, 1f), removeZone: false);
            layout.DragTo(B, removeZone: false);
            layout.EndDrag();

            Assert.AreEqual(1, layout.Count);
            Assert.AreEqual(B, layout[0].Position);
            Assert.IsTrue(layout.Undo());
            Assert.AreEqual(0, layout.Count);
            Assert.IsFalse(layout.CanUndo, "the many moves of one drag were a single edit");
        }

        [Test]
        public void ANewWell_DroppedOnTheTray_NeverHappened()
        {
            WellLayout layout = Layout();
            layout.BeginDragNew(WellKind.Attractor, A);
            layout.DragTo(new Vector2(0.5f, 0f), removeZone: true);
            layout.EndDrag();

            Assert.AreEqual(0, layout.Count);
            Assert.IsFalse(layout.CanUndo);
            Assert.AreEqual(4, layout.Remaining(WellKind.Attractor));
        }

        [Test]
        public void MovingAPlacedWell_IsOneEdit_AndLettingGoInPlaceIsNone()
        {
            WellLayout layout = Layout();
            layout.Place(WellKind.Attractor, A);

            layout.BeginDragExisting(0);
            layout.EndDrag(); // a tap that grabs and releases where it was
            Assert.AreEqual(1, layout.Count);

            layout.BeginDragExisting(0);
            layout.DragTo(B, removeZone: false);
            layout.EndDrag();
            Assert.AreEqual(B, layout[0].Position);

            Assert.IsTrue(layout.Undo());
            Assert.AreEqual(A, layout[0].Position);
            Assert.IsTrue(layout.Undo());
            Assert.AreEqual(0, layout.Count);
            Assert.IsFalse(layout.CanUndo);
        }

        [Test]
        public void ThePreview_LeavesOutAWellHeldInTheRemovalZone()
        {
            WellLayout layout = Layout();
            layout.Place(WellKind.Attractor, A);
            layout.Place(WellKind.Attractor, B);
            Assert.AreEqual(2, layout.SimulationCount);

            layout.BeginDragExisting(0);
            layout.DragTo(new Vector2(0.5f, 0f), removeZone: true);
            Assert.AreEqual(1, layout.SimulationCount);
            Assert.AreEqual(B, layout.SimulationWells[0].Position);

            layout.DragTo(A, removeZone: false);
            Assert.AreEqual(2, layout.SimulationCount);
        }

        [Test]
        public void CancellingADrag_PutsEverythingBack()
        {
            WellLayout layout = Layout();
            layout.Place(WellKind.Attractor, A);

            layout.BeginDragExisting(0);
            layout.DragTo(B, removeZone: false);
            layout.CancelDrag();
            Assert.AreEqual(A, layout[0].Position);
            Assert.IsFalse(layout.IsDragging);

            layout.BeginDragNew(WellKind.Attractor, B);
            layout.CancelDrag();
            Assert.AreEqual(1, layout.Count);
            Assert.AreEqual(1, layout.SimulationCount);
        }

        [Test]
        public void OnlyOneDragAtATime()
        {
            WellLayout layout = Layout();
            layout.Place(WellKind.Attractor, A);
            Assert.IsTrue(layout.BeginDragExisting(0));
            Assert.IsFalse(layout.BeginDragExisting(0));
            Assert.IsFalse(layout.BeginDragNew(WellKind.Attractor, B));
            Assert.AreEqual(-1, layout.Place(WellKind.Attractor, B));
        }

        [Test]
        public void TheBudget_StopsATrayDragWhenNoneAreLeft()
        {
            WellLayout layout = Layout(par: 1);
            layout.Place(WellKind.Attractor, A);
            layout.Place(WellKind.Attractor, B);
            layout.Place(WellKind.Attractor, new Vector2(0.5f, 1.2f));
            Assert.IsFalse(layout.BeginDragNew(WellKind.Attractor, A));
        }

        // ---- picking ----

        [Test]
        public void NearestWell_PicksTheClosestWithinTheRadius()
        {
            WellLayout layout = Layout();
            layout.Place(WellKind.Attractor, A);
            layout.Place(WellKind.Attractor, new Vector2(0.25f, 0.8f));

            Assert.AreEqual(1, layout.NearestWell(new Vector2(0.24f, 0.8f), 0.1f));
            Assert.AreEqual(0, layout.NearestWell(new Vector2(0.16f, 0.8f), 0.1f));
            Assert.AreEqual(-1, layout.NearestWell(new Vector2(0.9f, 1.4f), 0.1f));
        }
    }
}
