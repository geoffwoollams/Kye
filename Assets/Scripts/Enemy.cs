using System.Collections;
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
    private Vector2 LeftOf(Vector2 d)   { return -RightOf(d); }

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
    
    private Vector2 ChooseGuardStartDirection(Vector2 iPos)
    {
        Vector2[] dirs = { new Vector2(1, 0), new Vector2(0, 1), new Vector2(-1, 0), new Vector2(0, -1) };

        Vector2 touchOnly = Vector2.zero, forwardOnly = Vector2.zero;
        bool haveTouchOnly = false, haveForwardOnly = false;

        foreach (Vector2 d in dirs)
        {
            bool touching = !Common.IsEmpty(iPos + RightOf(d));
            bool forwardEmpty = Common.IsEmpty(iPos + d);

            if (touching && forwardEmpty) return d;
            if (touching && !haveTouchOnly) { touchOnly = d; haveTouchOnly = true; }
            if (forwardEmpty && !haveForwardOnly) { forwardOnly = d; haveForwardOnly = true; }
        }

        if (haveTouchOnly) return touchOnly;
        if (haveForwardOnly) return forwardOnly;
        return dirs[0];
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
            e = t.GetComponent<Enemy>();
        
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
        return;
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

    private Vector2? FindPathDirection(bool allowObstacles)
    {
        int startX = _item.x;
        int startY = _item.y;
        int targetX = GameController.Instance.Kye.x;
        int targetY = GameController.Instance.Kye.y;

        if (startX == targetX && startY == targetY)
            return null;

        var visited = new bool[29, 19];
        var prev = new Vector2Int[29, 19];

        var queue = new Queue<Vector2Int>();
        var start = new Vector2Int(startX, startY);
        queue.Enqueue(start);
        visited[startX, startY] = true;

        Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        bool found = false;

        while (queue.Count > 0 && !found)
        {
            var current = queue.Dequeue();

            foreach (var dir in directions)
            {
                int nx = current.x + dir.x;
                int ny = current.y + dir.y;

                if (!Common.InBounds(nx, ny))
                    continue;

                if (visited[nx, ny])
                    continue;

                if (nx == targetX && ny == targetY)
                {
                    visited[nx, ny] = true;
                    prev[nx, ny] = current;
                    found = true;
                    break;
                }

                var spot = Common.GetItem(nx, ny);
                bool walkable = spot == null;
                if (!walkable && allowObstacles && spot.IsPotentiallyWalkablePath)
                    walkable = true;

                if (!walkable)
                    continue;

                visited[nx, ny] = true;
                prev[nx, ny] = current;
                queue.Enqueue(new Vector2Int(nx, ny));
            }
        }

        if (!found)
            return null;

        var step = new Vector2Int(targetX, targetY);
        while (prev[step.x, step.y] != start)
            step = prev[step.x, step.y];

        return new Vector2(step.x - startX, step.y - startY);
    }
}
