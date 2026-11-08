#if UNITY_EDITOR
using System;
using System.Collections.Generic;

// Path finder for the Dev bot. Plain C# on purpose (no UnityEngine), so it can be tested outside Unity.
//
// It works on a snapshot of the grid (one byte per cell, see the constants) and finds a list of Kye
// moves (0 = up, 1 = down, 2 = left, 3 = right) that reaches a diamond. Kye can push one block at a
// time into an empty cell (or into a hungry blackhole, which eats it), so the search is a
// breadth-first search over (Kye position, block positions) and not just over Kye positions.
//
// The snapshot is frozen: monsters and sliders are not predicted. The bot replans when the world no
// longer matches the plan, and waits when no safe plan exists (so moving items can get out of the way).
public sealed class DevSolver
{
    public const int W = 30, H = 20;

    // Cell types. Door + direction (0 up, 1 down, 2 left, 3 right) is a door that can only be entered by moving that way.
    public const byte Empty = 0, Solid = 1, Push = 2, Diamond = 3, Earth = 4, Killer = 5, Hole = 6, Door = 7;
    public const byte StickerUD = 11, StickerLR = 12; // magnets: pushable, and they pull blocks towards Kye
    private const int Types = 13;

    public static readonly int[] DX = { 0, 0, -1, 1 };
    public static readonly int[] DY = { 1, -1, 0, 0 };

    public enum Goal
    {
        Diamond, // reach a diamond
        Hazard,  // reach anything that kills Kye (used to die on purpose and respawn at the start)
        Earth,   // dig the nearest earth (exploring when stuck)
        Push,    // push the nearest block (exploring when stuck)
        Clear    // reach the nearest cell that is out of every sliding item's way
    }

    private enum Step { Blocked, Ok, Reached }

    public readonly byte[] Cells = new byte[W * H];
    public readonly bool[] Danger = new bool[W * H];   // Kye dies when standing here (next to, or on, a killer)
    public readonly bool[] Rays = new bool[W * H];     // cells sliding items are heading through (set by the bot when it needs it)
    public readonly bool[] Banned = new bool[W * H * 4]; // (cell * 4 + direction) moves that failed in the real game
    public int KyeX, KyeY;
    public bool AllowEarth = true; // false: earth counts as a wall (unless digging it is the goal)

    private static readonly ulong[] Z = new ulong[W * H * Types];
    private static readonly ulong[] ZKye = new ulong[W * H];

    static DevSolver()
    {
        var r = new Random(12345);
        for (int i = 0; i < Z.Length; i++) Z[i] = NextUlong(r);
        for (int i = 0; i < ZKye.Length; i++) ZKye[i] = NextUlong(r);
    }

    private static ulong NextUlong(Random r)
    {
        return ((ulong)r.Next() << 42) ^ ((ulong)r.Next() << 21) ^ (ulong)r.Next();
    }

    public static bool InBounds(int x, int y) { return x >= 1 && x <= 28 && y >= 1 && y <= 18; }
    public static int Index(int x, int y) { return x + y * W; }

    public int DiamondCount()
    {
        int n = 0;
        for (int i = 0; i < Cells.Length; i++)
            if (Cells[i] == Diamond) n++;
        return n;
    }

    // Call after filling Cells.
    public void ComputeDanger()
    {
        Array.Clear(Danger, 0, Danger.Length);
        for (int i = 0; i < Cells.Length; i++)
        {
            if (Cells[i] != Killer) continue;
            int x = i % W, y = i / W;
            Danger[i] = true;
            for (int d = 0; d < 4; d++)
            {
                int nx = x + DX[d], ny = y + DY[d];
                if (nx >= 0 && nx < W && ny >= 0 && ny < H)
                    Danger[Index(nx, ny)] = true;
            }
        }
    }

    private static bool IsPushable(byte t) { return t == Push || t == StickerUD || t == StickerLR; }

    public void ClearBans() { Array.Clear(Banned, 0, Banned.Length); }

    // Can Kye make this move right now in the current snapshot?
    public bool CanStep(int pos, int dir, Goal goal, bool allowPush, bool avoidDanger)
    {
        int ni, pd;
        return TryStep(Cells, pos, dir, goal, allowPush, avoidDanger, -1, out ni, out pd) != Step.Blocked;
    }

    private Step TryStep(byte[] g, int pos, int d, Goal goal, bool allowPush, bool avoidDanger, int target, out int ni, out int pushDest)
    {
        pushDest = -1;
        ni = -1;

        int x = pos % W, y = pos / W;
        int nx = x + DX[d], ny = y + DY[d];
        if (!InBounds(nx, ny)) return Step.Blocked;
        if (Banned[pos * 4 + d]) return Step.Blocked;

        ni = Index(nx, ny);
        byte t = g[ni];
        bool reached = false;

        if (t == Killer || t == Hole)
            return goal == Goal.Hazard ? Step.Reached : Step.Blocked;

        if (IsPushable(t))
        {
            if (!allowPush) return Step.Blocked;
            int bx = nx + DX[d], by = ny + DY[d];
            if (!InBounds(bx, by)) return Step.Blocked;
            int dest = Index(bx, by);
            if (g[dest] != Empty && g[dest] != Hole) return Step.Blocked;
            pushDest = dest;
            reached = goal == Goal.Push;
        }
        else if (t >= Door)
        {
            if (t - Door != d) return Step.Blocked;
        }
        else if (t == Solid)
        {
            return Step.Blocked;
        }
        else if (t == Earth)
        {
            if (!AllowEarth && goal != Goal.Earth) return Step.Blocked;
            reached = goal == Goal.Earth;
        }
        else if (t == Diamond)
        {
            reached = goal == Goal.Diamond && (target < 0 || ni == target);
        }

        if (Danger[ni])
        {
            if (goal == Goal.Hazard) return Step.Reached;
            if (avoidDanger) return Step.Blocked;
        }

        if (goal == Goal.Clear && !Rays[ni] && !Danger[ni])
            reached = true;

        return reached ? Step.Reached : Step.Ok;
    }

    private struct Node
    {
        public int Parent;
        public int Pos;
        public int Dir;
        public byte[] Grid;
        public ulong Hash;
    }

    private readonly List<Node> _nodes = new List<Node>(4096);
    private readonly HashSet<ulong> _visited = new HashSet<ulong>();

    // Breadth-first search from (KyeX, KyeY), so the first goal found takes the fewest moves.
    // Returns the moves, or null when there is no way (or maxNodes states were not enough).
    // target >= 0 only accepts that one diamond cell. With a Random the order the directions are tried in
    // is shuffled, so retries can take different (equally short) routes.
    public List<int> Find(Goal goal, bool allowPush, bool avoidDanger, Random rng, int maxNodes, int target = -1)
    {
        int start = Index(KyeX, KyeY);

        int[] order = { 0, 1, 2, 3 };
        if (rng != null)
        {
            for (int i = 3; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int tmp = order[i]; order[i] = order[j]; order[j] = tmp;
            }
        }

        _nodes.Clear();
        _visited.Clear();

        ulong h0 = 0;
        for (int i = 0; i < Cells.Length; i++)
            if (Cells[i] != Empty) h0 ^= Z[i * Types + Cells[i]];

        bool hasStickers = false;
        for (int i = 0; i < Cells.Length; i++)
            if (Cells[i] == StickerUD || Cells[i] == StickerLR) { hasStickers = true; break; }

        _nodes.Add(new Node { Parent = -1, Pos = start, Dir = -1, Grid = Cells, Hash = h0 });
        _visited.Add(h0 ^ ZKye[start]);

        for (int i = 0; i < _nodes.Count && _nodes.Count < maxNodes; i++)
        {
            Node n = _nodes[i];

            for (int k = 0; k < 4; k++)
            {
                int d = order[k];
                int ni, pd;
                Step s = TryStep(n.Grid, n.Pos, d, goal, allowPush, avoidDanger, target, out ni, out pd);
                if (s == Step.Blocked) continue;
                if (s == Step.Reached) return BuildPath(i, d);

                byte[] grid = n.Grid;
                ulong hash = n.Hash;
                bool owned = false;

                if (pd >= 0)
                {
                    byte block = grid[ni];
                    grid = (byte[])n.Grid.Clone();
                    owned = true;
                    grid[ni] = Empty;
                    hash ^= Z[ni * Types + block];
                    if (grid[pd] == Empty) // otherwise it went into a blackhole and is gone
                    {
                        grid[pd] = block;
                        hash ^= Z[pd * Types + block];
                    }
                }

                if (hasStickers)
                    ApplyStickers(ref grid, ref hash, ref owned, ni);

                if (_visited.Add(hash ^ ZKye[ni]))
                    _nodes.Add(new Node { Parent = i, Pos = ni, Dir = d, Grid = grid, Hash = hash });
            }
        }

        return null;
    }

    // Mirrors Common.StickersFollowKye: after Kye moves, a magnet exactly two cells away in a straight line
    // (up/down magnets vertically, left/right magnets horizontally) is pulled next to Kye if that cell is empty,
    // and the blocks behind it come along (a chain, for as long as the next one is a magnet too).
    private static void ApplyStickers(ref byte[] grid, ref ulong hash, ref bool owned, int kyePos)
    {
        int kx = kyePos % W, ky = kyePos / W;

        for (int d = 0; d < 4; d++)
        {
            int sx = kx + 2 * DX[d], sy = ky + 2 * DY[d];
            if (sx < 0 || sx >= W || sy < 0 || sy >= H) continue;

            int si = Index(sx, sy);
            byte magnet = grid[si];
            if (magnet != (d < 2 ? StickerUD : StickerLR)) continue;

            int ti = Index(kx + DX[d], ky + DY[d]);
            if (grid[ti] != Empty) continue;

            if (!owned)
            {
                grid = (byte[])grid.Clone();
                owned = true;
            }

            grid[si] = Empty;
            grid[ti] = magnet;
            hash ^= Z[si * Types + magnet] ^ Z[ti * Types + magnet];

            // Pull what is stuck behind it, one cell at a time.
            int cur = si;
            byte pulled = magnet;
            int bx = sx + DX[d], by = sy + DY[d];
            while (pulled == StickerUD || pulled == StickerLR)
            {
                if (bx < 0 || bx >= W || by < 0 || by >= H) break;
                int bi = Index(bx, by);
                byte behind = grid[bi];
                if (!IsPushable(behind)) break;

                grid[bi] = Empty;
                grid[cur] = behind;
                hash ^= Z[bi * Types + behind] ^ Z[cur * Types + behind];

                cur = bi;
                pulled = behind;
                bx += DX[d];
                by += DY[d];
            }
        }
    }

    private List<int> BuildPath(int node, int lastDir)
    {
        var path = new List<int> { lastDir };
        while (_nodes[node].Parent >= 0)
        {
            path.Add(_nodes[node].Dir);
            node = _nodes[node].Parent;
        }
        path.Reverse();
        return path;
    }
}
#endif
