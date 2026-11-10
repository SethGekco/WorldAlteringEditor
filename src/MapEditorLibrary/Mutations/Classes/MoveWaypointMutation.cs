using MapEditorLibrary.GameMath;
using MapEditorLibrary.Models;

namespace MapEditorLibrary.Mutations.Classes;

/// <summary>
/// A mutation that moves an existing waypoint to another cell.
/// </summary>
public class MoveWaypointMutation : Mutation
{
    public MoveWaypointMutation(IMutationTarget mutationTarget, int waypointNumber, Point2D newPosition) : base(mutationTarget)
    {
        this.waypointNumber = waypointNumber;
        this.newPosition = newPosition;
    }

    private readonly int waypointNumber;
    private readonly Point2D newPosition;
    private Point2D oldPosition;

    public override string GetDisplayString()
    {
        return string.Format(Translate(this, "DisplayString",
            "Move waypoint {0} to {1}"),
                waypointNumber, newPosition);
    }

    private void MoveTo(Point2D from, Point2D to)
    {
        var waypoint = MutationTarget.Map.Waypoints.Find(w => w.Identifier == waypointNumber);
        if (waypoint == null)
            return;

        MutationTarget.Map.RemoveWaypoint(waypoint);
        waypoint.Position = to;
        MutationTarget.Map.AddWaypoint(waypoint);

        MutationTarget.AddRefreshPoint(from, 1);
        MutationTarget.AddRefreshPoint(to, 1);
    }

    public override void Perform()
    {
        var waypoint = MutationTarget.Map.Waypoints.Find(w => w.Identifier == waypointNumber);
        if (waypoint == null)
            return;

        oldPosition = waypoint.Position;
        MoveTo(oldPosition, newPosition);
    }

    public override void Undo()
    {
        MoveTo(newPosition, oldPosition);
    }
}
