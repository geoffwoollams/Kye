using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    public float Speed = 1f;
    public bool IsSmort = false;
    public bool IsGuard = false;

    private float _nextMove;
    private bool _wandering;
    private Item _item;
    private bool _hadRightTouch;
    
    // If the guard hugs the wrong side visually, flip this ONE value.
    private const bool GuardRightIsAnticlockwise = false;

    private float _rightSign = 1f;   // learned from Rot90 in Awake

    private Vector2 RightOf(Vector2 d)  { return _rightSign * new Vector2(-d.y, d.x); }

    private void GuardTurnRight() { Common.Rot90(ref _item, GuardRightIsAnticlockwise); }
    private void GuardTurnLeft()  { Common.Rot90(ref _item, !GuardRightIsAnticlockwise); }
    
    // Angle the sprite art needs relative to "facing right". Tune once.
    private const float SpriteAngleOffset = -90f;

    private void FaceDirection(Vector2 d)
    {
        float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle + SpriteAngleOffset);
    }
    
    void Awake()
    {
        _item = GetComponent<Item>();
        _nextMove = Time.time + Speed;
    }

    void Start()
    {
        if (IsGuard)
        {
            // Learn which way Rot90 turns "right": facing (1,0), where does it end up?
            _item.Direction = new Vector2(1, 0);
            GuardTurnRight();
            _rightSign = _item.Direction.y;   // (0,1) -> +1, (0,-1) -> -1

            Vector2 iPos = new Vector2(_item.x, _item.y);
            _item.Direction = ChooseGuardStartDirection(iPos);
            _hadRightTouch = !Common.IsEmpty(iPos + RightOf(_item.Direction));
            
            FaceDirection(_item.Direction);
        }
    }
    
    private static readonly Vector2[] GuardStartDirections =
    {
        new Vector2(1, 0), new Vector2(0, 1), new Vector2(-1, 0), new Vector2(0, -1)
    };

    // Picks the direction to start facing. Best to worst:
    // a real wall on the right with a free cell ahead, a real wall on the right,
    // any item on the right with a free cell ahead, any item on the right, a free cell ahead.
    // Ties go to the first direction (right, up, left, down), so the choice is always the same.
    // With nothing around, it walks forward until it hits something or finds something on its right.
    private Vector2 ChooseGuardStartDirection(Vector2 iPos)
    {
        Vector2 best = GuardStartDirections[0];
        int bestScore = -1;

        foreach (Vector2 d in GuardStartDirections)
        {
            Vector2 rightSpot = iPos + RightOf(d);
            bool touching = !Common.IsEmpty(rightSpot);
            // The border is always wall, but GetItem does not see it.
            bool wall = touching && (!Common.InBounds(rightSpot) || Common.IsWall(Common.GetItem(rightSpot)));
            bool forwardEmpty = Common.IsEmpty(iPos + d);

            int score;
            if (wall) score = forwardEmpty ? 5 : 4;
            else if (touching) score = forwardEmpty ? 3 : 2;
            else score = forwardEmpty ? 1 : 0;

            if (score > bestScore)
            {
                best = d;
                bestScore = score;
            }
        }

        return best;
    }

    public void Tick()
    {
        if(Time.time < _nextMove)
            return;
        
        if(_item.IsStuckToSticker)
            return;

        var x = _item.x;
        var y = _item.y;

        Vector2 direction = Vector2.zero;

        if (IsGuard)
        {
            Vector2 iPos = new Vector2(x, y);
            Vector2 fwd = _item.Direction;

            bool hasRightTouch = !Common.IsEmpty(iPos + RightOf(fwd));
            bool forwardEmpty = Common.IsEmpty(iPos + fwd);

            if (_hadRightTouch && !hasRightTouch)
            {
                GuardTurnRight();                 // lost the wall: turn right, step into the gap
                direction = _item.Direction;
            }
            else if (forwardEmpty)
            {
                direction = fwd;
            }
            else
            {
                GuardTurnLeft();                  // blocked: turn left, move next tick
                direction = Vector2.zero;
            }

            _hadRightTouch = hasRightTouch;
        }

        // Find path
        if (direction == Vector2.zero && IsSmort)
        {
            _wandering = false;

            var smartDirection = FindSmartDirection();
            if (smartDirection.HasValue)
            {
                direction = smartDirection.Value;
            }
            else
            {
                _nextMove = Time.time + Speed;
                return;
            }
        }
        
        // Random direction occasionally
        if(direction == Vector2.zero && Common.GetRandom(2) == 0 && !IsGuard)
        {
            var d = Common.GetRandom(5);
            if(d == 0)
                direction = Vector2.left;
            else if(d == 1)
                direction = Vector2.right;
            else if(d == 2)
                direction = Vector2.down;
            else if(d == 3)
                direction = Vector2.up;
            else if(d == 4)
            {
                _nextMove = Time.time + Speed;
                return;
            }

            _wandering = true;
        }
        
        // Move towards Kye
        else if(direction == Vector2.zero && !IsGuard)
        {
            _wandering = false;
            
            int dx = GameController.Instance.Kye.x - _item.x;
            int dy = GameController.Instance.Kye.y - _item.y;

            if(dy == 0)
                if(dx > 0)
                    direction = Vector2.right;
                else
                    direction = Vector2.left;
            else
                if(dy > 0)
                    direction = Vector2.up;
                else
                    direction = Vector2.down;

            if(Common.GetItemBy(direction, _item) != null)
            {
                if(direction.x != 0)
                {
                    if(dx > 0)
                        direction = Vector2.right;
                    else
                        direction = Vector2.left;
                }
                else if(direction.y != 0)
                {
                    if(dy > 0)
                        direction = Vector2.up;
                    else
                        direction = Vector2.down;
                }
            }
        }

        var t = Common.GetItemBy(direction, _item);

        Enemy e = null;
        if(t != null)
            e = t._enemy;
        
        if(t == null)
            Common.MoveItemBy(direction, _item);
        else if (e != null && !e.IsSmort && !e.IsGuard && IsSmort)
        {
            AudioManager.Instance.PlayTrackerKill();
            Common.DestroyItem(t, false);
            Common.MoveItemBy(direction, _item);
        }
        else if(_wandering && t.IsBlackhole && !IsGuard)
            t._blackhole.Consume(_item);
        
        float GoSpeed = Speed;
        if(!IsGuard)
            GoSpeed = Speed * Random.Range(0.9f, 1.1f);
        
        if(IsGuard)
            FaceDirection(_item.Direction);
        
        _nextMove = Time.time + GoSpeed;
    }

    private Vector2? FindSmartDirection()
    {
        // Prefer a path through empty spots only
        var direction = FindPathDirection(false);

        // Fall back to a path that may require pushing/splodable spots out of the way
        if (direction == null)
            direction = FindPathDirection(true);

        return direction;
    }

    private const int GridWidth = 30;
    private const int GridHeight = 20;

    // Shared search buffers, so finding a path does not allocate.
    private static readonly int[,] VisitedStamp = new int[GridWidth, GridHeight];
    private static readonly Vector2Int[,] PreviousStep = new Vector2Int[GridWidth, GridHeight];
    private static readonly Queue<Vector2Int> PathQueue = new Queue<Vector2Int>();
    private static readonly Vector2Int[] PathDirections =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };
    private static int _searchStamp;

    private Vector2? FindPathDirection(bool allowObstacles)
    {
        int startX = _item.x;
        int startY = _item.y;
        int targetX = GameController.Instance.Kye.x;
        int targetY = GameController.Instance.Kye.y;

        if (startX == targetX && startY == targetY)
            return null;

        if (_searchStamp == int.MaxValue)
        {
            System.Array.Clear(VisitedStamp, 0, VisitedStamp.Length);
            _searchStamp = 0;
        }
        int stamp = ++_searchStamp;

        PathQueue.Clear();
        var start = new Vector2Int(startX, startY);
        PathQueue.Enqueue(start);
        VisitedStamp[startX, startY] = stamp;

        bool found = false;

        while (PathQueue.Count > 0 && !found)
        {
            var current = PathQueue.Dequeue();

            foreach (var dir in PathDirections)
            {
                int nx = current.x + dir.x;
                int ny = current.y + dir.y;

                if (!Common.InBounds(nx, ny))
                    continue;

                if (VisitedStamp[nx, ny] == stamp)
                    continue;

                if (nx == targetX && ny == targetY)
                {
                    VisitedStamp[nx, ny] = stamp;
                    PreviousStep[nx, ny] = current;
                    found = true;
                    break;
                }

                var spot = Common.GetItem(nx, ny);
                bool walkable = spot == null;
                if (!walkable && allowObstacles && spot.IsPotentiallyWalkablePath)
                    walkable = true;

                if (!walkable)
                    continue;

                VisitedStamp[nx, ny] = stamp;
                PreviousStep[nx, ny] = current;
                PathQueue.Enqueue(new Vector2Int(nx, ny));
            }
        }

        if (!found)
            return null;

        var step = new Vector2Int(targetX, targetY);
        while (PreviousStep[step.x, step.y] != start)
            step = PreviousStep[step.x, step.y];

        return new Vector2(step.x - startX, step.y - startY);
    }
}
