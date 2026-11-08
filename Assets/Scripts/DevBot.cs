#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

// Editor-only bot that plays a level by itself (started from the Dev panel). It feeds Kye's input through
// GameController.ProcessInput, so the real game rules run unchanged.
//
// How it plays:
//  - Every step it looks at the live grid (DevSolver is the path finder) and follows a shortest path to the
//    nearest diamond: first without pushing anything, then pushing blocks (never sploders), through earth,
//    through doors the right way, never next to a monster, never into a blackhole.
//  - It replans whenever the world no longer matches the plan, so it copes with items that move.
//  - Magnets (stickers) are modelled: pushing/walking near one pulls blocks, so plans can drag things around.
//  - With no safe path it waits, because sliders, rockies, timers and monsters may move out of the way.
//    While waiting it steps out of the way of sliding items, and after a moment it explores: it digs the
//    nearest earth, or pushes the nearest block, then waits again to see what that freed up.
//  - If it has been stuck for a while but the start point could still reach a diamond, it walks into a
//    monster on purpose: dying sends Kye back to the start.
//  - If it is really stuck, runs out of lives, or makes no progress for a long time, it restarts the level
//    (with different random choices) and keeps trying until the level is completed.
// Completed runs never submit a best time, and analytics are switched off while it runs.
public class DevBot : MonoBehaviour
{
    public static DevBot Instance { get; private set; }
    public static bool Active { get { return Instance != null && Instance._running; } }

    public float StepDelay = 0.15f;           // seconds between Kye's moves (slower than a person holding a key)
    public float ExploreAfter = 3f;           // stuck this long: dig earth / push blocks to see what changes
    public float IdleBeforeDying = 8f;        // stuck this long: consider dying on purpose
    public float IdleBeforeRestart = 30f;     // stuck this long: restart the level
    public float NoProgressRestart = 120f;    // no diamond collected for this long: restart the level

    private enum Pending { None, Restart, ReturnToPanel }

    private bool _running;
    private bool _loadingSelf;
    private Pending _pending;
    private string _level = "";
    private string _savedLastLevel;
    private bool _savedAnalytics;

    private int _attempt;
    private float _runStart, _lastProgress, _nextAct, _idleSince, _nextDeep, _nextDyingCheck, _doneAt;
    private int _lastDiamonds;
    private string _status = "";
    private System.Random _rng;

    private readonly DevSolver _solver = new DevSolver();
    private List<int> _plan;
    private int _planIdx;
    private DevSolver.Goal _planGoal;
    private float _waitUntil;
    private int _exploreCount;

    private int _lastDir, _lastPos, _expectPos, _failKey, _failCount;

    void Awake()
    {
        Instance = this;
    }

    // ---- Starting and stopping -------------------------------------------------------------------------

    public void StartLevel(string levelName)
    {
        Stop("restarted");

        var gc = GameController.Instance;
        var settings = AppController.Instance.settings;

        _level = levelName;
        _savedLastLevel = settings.LastLevelPlayed;
        _savedAnalytics = KyeAnalytics.enabled;
        KyeAnalytics.enabled = false;

        _running = true;
        _attempt = 1;
        _runStart = Time.unscaledTime;
        _pending = Pending.None;
        _doneAt = -1f;

        _loadingSelf = true;
        gc.LoadLevelWithNoR(levelName);
        _loadingSelf = false;

        ResetAttempt();
        Debug.Log("[DevBot] Started " + levelName);
    }

    public void Stop(string reason)
    {
        if (!_running)
            return;

        _running = false;

        AppController.Instance.settings.LastLevelPlayed = _savedLastLevel;
        KyeAnalytics.enabled = _savedAnalytics;

        Debug.Log("[DevBot] Stopped " + _level + (reason != null ? " (" + reason + ")" : ""));
    }

    // Called by GameController so the bot stops when something else takes over.
    public static void NotifyLevelLoading()
    {
        if (Instance != null && Instance._running && !Instance._loadingSelf)
            Instance.Stop("level changed");
    }

    public static void NotifyMainMenu()
    {
        if (Instance != null)
            Instance.Stop("main menu");
    }

    private void ResetAttempt()
    {
        _rng = new System.Random(_attempt * 7919 + _level.GetHashCode());
        _plan = null;
        _planIdx = 0;
        _lastDir = -1;
        _failCount = 0;
        _failKey = -1;
        _idleSince = -1f;
        _nextDeep = 0f;
        _nextDyingCheck = 0f;
        _waitUntil = 0f;
        _exploreCount = 0;
        _lastDiamonds = int.MaxValue;
        _lastProgress = Time.unscaledTime;
        _solver.ClearBans();
    }

    // ---- Supervising (restarts, completion) ------------------------------------------------------------

    void Update()
    {
        if (!_running)
            return;

        var gc = GameController.Instance;
        float now = Time.unscaledTime;

        if (gc.LevelComplete.activeSelf)
        {
            if (_doneAt < 0f)
            {
                _doneAt = now;
                Debug.Log("[DevBot] Completed " + _level + " on try " + _attempt + " after " + (now - _runStart).ToString("0.0") + "s");
            }
            else if (now - _doneAt > 2f)
            {
                ReturnToPanel();
            }
            return;
        }

        if (_pending == Pending.ReturnToPanel)
        {
            ReturnToPanel();
            return;
        }

        if (gc.LevelFailed.activeSelf || _pending == Pending.Restart || now - _lastProgress > NoProgressRestart)
            Restart();
    }

    private void Restart()
    {
        _attempt++;
        _pending = Pending.None;

        _loadingSelf = true;
        GameController.Instance.RestartLevel();
        _loadingSelf = false;

        ResetAttempt();
        Debug.Log("[DevBot] Restarting " + _level + ", try " + _attempt);
    }

    private void ReturnToPanel()
    {
        Stop(null);
        GameController.Instance.ShowMainMenu();
        DevPanel.Instance.ShowPanel();
    }

    // ---- Playing ---------------------------------------------------------------------------------------

    // Called by GameController.ProcessInput. Returns the direction to move this frame (or zero).
    public Vector2 NextInput()
    {
        var gc = GameController.Instance;
        if (!_running || gc.Kye == null)
            return Vector2.zero;
        if (gc.PauseMenu.activeSelf || gc.LevelFailed.activeSelf || gc.LevelComplete.activeSelf || gc.MessageContainer.activeSelf)
            return Vector2.zero;

        float now = Time.unscaledTime;
        if (now < _nextAct)
            return Vector2.zero;

        Snapshot(gc);
        int pos = DevSolver.Index(gc.Kye.x, gc.Kye.y);
        int diamonds = _solver.DiamondCount();

        // Did the last move do what we expected?
        if (_lastDir >= 0)
        {
            if (pos != _expectPos)
            {
                if (pos == _lastPos)
                {
                    // Kye did not move: after a few failures stop trying that move.
                    int key = _lastPos * 4 + _lastDir;
                    _failCount = key == _failKey ? _failCount + 1 : 1;
                    _failKey = key;
                    if (_failCount >= 3)
                    {
                        _solver.Banned[key] = true;
                        _failCount = 0;
                    }
                }
                _plan = null; // blocked, died and respawned, or got moved
            }
            _lastDir = -1;
        }

        if (diamonds != _lastDiamonds)
        {
            if (_lastDiamonds != int.MaxValue && diamonds < _lastDiamonds)
            {
                _lastProgress = now;
                _solver.ClearBans();
                _idleSince = -1f;
            }
            _lastDiamonds = diamonds;
        }

        if (diamonds == 0)
        {
            // Either the level is about to finish, or it never had any diamonds and can't be completed.
            if (_attempt == 1 && now - _runStart > 3f)
            {
                Debug.Log("[DevBot] " + _level + " has no diamonds, nothing to do.");
                _pending = Pending.ReturnToPanel;
            }
            return Vector2.zero;
        }

        // Keep the plan only while its next step is still possible.
        if (_plan != null && (_planIdx >= _plan.Count ||
            !_solver.CanStep(pos, _plan[_planIdx], _planGoal, true, _planGoal != DevSolver.Goal.Hazard)))
            _plan = null;

        if (_plan == null)
            MakePlan(gc, pos, now);

        if (_plan == null)
        {
            _nextAct = now + 0.2f;
            return Vector2.zero;
        }

        int d = _plan[_planIdx++];
        _lastDir = d;
        _lastPos = pos;
        _expectPos = pos + DevSolver.DX[d] + DevSolver.DY[d] * DevSolver.W;
        _nextAct = now + StepDelay;
        return new Vector2(DevSolver.DX[d], DevSolver.DY[d]);
    }

    private void MakePlan(GameController gc, int pos, float now)
    {
        _planIdx = 0;
        _planGoal = DevSolver.Goal.Diamond;
        float idle = _idleSince < 0f ? 0f : now - _idleSince;

        List<int> plan = null;

        // On retries sometimes go for a random diamond first, so a retry doesn't repeat the same mistakes.
        if (_attempt > 1 && _rng.NextDouble() < 0.3)
        {
            int target = RandomDiamond();
            if (target >= 0)
                plan = FindLeastDisturbing(40000, target);
        }

        // Direct path to the nearest diamond, changing as little as possible (see FindLeastDisturbing).
        if (plan == null)
            plan = FindLeastDisturbing(40000, -1);

        // Waiting hasn't helped: one much bigger (slower) search now and then.
        if (plan == null && idle >= 2f && now >= _nextDeep)
        {
            _nextDeep = now + 5f;
            plan = FindLeastDisturbing(120000, -1);
        }

        if (plan != null)
        {
            _plan = plan;
            _idleSince = -1f;
            _status = "following a path (" + plan.Count + " moves)";
            return;
        }

        // No way to a diamond right now: wait, and maybe die on purpose, or give up on this try.
        if (_idleSince < 0f)
            _idleSince = now;
        idle = now - _idleSince;
        _status = "waiting (" + idle.ToString("0") + "s)";

        ComputeRays(gc);

        // Get out of the way of sliding items (they can't pass Kye) and away from monsters.
        if (_solver.Rays[pos] || _solver.Danger[pos])
        {
            var clear = _solver.Find(DevSolver.Goal.Clear, true, true, _rng, 4000);
            if (clear != null)
            {
                _plan = clear;
                _planGoal = DevSolver.Goal.Clear;
                _status = "stepping out of the way";
                return;
            }
        }

        // Explore: dig the nearest earth, otherwise push the nearest block, then wait to see what that did.
        if (idle >= ExploreAfter && now >= _waitUntil)
        {
            DevSolver.Goal first = DevSolver.Goal.Earth, second = DevSolver.Goal.Push;
            if (_exploreCount % 4 == 3) { first = DevSolver.Goal.Push; second = DevSolver.Goal.Earth; }

            var explore = _solver.Find(first, true, true, _rng, 4000);
            var goal = first;
            if (explore == null)
            {
                explore = _solver.Find(second, true, true, _rng, 4000);
                goal = second;
            }

            if (explore != null)
            {
                _exploreCount++;
                _plan = explore;
                _planGoal = goal;
                _waitUntil = now + 2f + explore.Count * StepDelay;
                _status = goal == DevSolver.Goal.Earth ? "digging earth" : "pushing a block";
                return;
            }
        }

        if (idle >= IdleBeforeDying && now >= _nextDyingCheck)
        {
            _nextDyingCheck = now + 2f;
            if (StartPointCanReachDiamond(gc))
            {
                var hazard = _solver.Find(DevSolver.Goal.Hazard, true, false, _rng, 20000);
                if (hazard != null)
                {
                    _plan = hazard;
                    _planGoal = DevSolver.Goal.Hazard;
                    _status = "dying on purpose to get back to the start";
                    return;
                }
            }
        }

        if (idle >= IdleBeforeRestart)
            _pending = Pending.Restart;
    }

    // Shortest path to a diamond that disturbs the level as little as possible. Each step is only tried
    // when the one before found nothing: walking only over empty cells, then also digging earth,
    // then also pushing blocks (without digging), then both.
    private List<int> FindLeastDisturbing(int pushNodes, int target)
    {
        List<int> plan = null;
        try
        {
            _solver.AllowEarth = false;
            plan = _solver.Find(DevSolver.Goal.Diamond, false, true, _rng, 4000, target);
            if (plan != null) return plan;

            _solver.AllowEarth = true;
            plan = _solver.Find(DevSolver.Goal.Diamond, false, true, _rng, 4000, target);
            if (plan != null) return plan;

            _solver.AllowEarth = false;
            plan = _solver.Find(DevSolver.Goal.Diamond, true, true, _rng, pushNodes, target);
            if (plan != null) return plan;

            _solver.AllowEarth = true;
            return _solver.Find(DevSolver.Goal.Diamond, true, true, _rng, pushNodes, target);
        }
        finally
        {
            _solver.AllowEarth = true;
        }
    }

    // Would a diamond be reachable if Kye were back at the start point?
    private bool StartPointCanReachDiamond(GameController gc)
    {
        int sx = Mathf.RoundToInt(gc.StartPoint.x), sy = Mathf.RoundToInt(gc.StartPoint.y);
        if (!DevSolver.InBounds(sx, sy))
            return false;

        int start = DevSolver.Index(sx, sy);
        if (_solver.Cells[start] != DevSolver.Empty)
            return false; // something is on it, Kye would respawn somewhere nearby instead

        int x = _solver.KyeX, y = _solver.KyeY;
        _solver.KyeX = sx;
        _solver.KyeY = sy;
        bool ok = _solver.Find(DevSolver.Goal.Diamond, false, true, null, 4000) != null ||
                  _solver.Find(DevSolver.Goal.Diamond, true, true, null, 20000) != null;
        _solver.KyeX = x;
        _solver.KyeY = y;
        return ok;
    }

    // Cells that sliding items (sliders, rockies, bouncers) are heading into. Only worked out when waiting.
    private void ComputeRays(GameController gc)
    {
        Array.Clear(_solver.Rays, 0, _solver.Rays.Length);
        var items = gc.Items;

        for (int x = 1; x <= 28; x++)
        {
            for (int y = 1; y <= 18; y++)
            {
                var it = items[x, y];
                if (it == null || it.IsDoor || it.Direction == Vector2.zero || it.GetComponent<GoForward>() == null)
                    continue;

                _solver.Rays[DevSolver.Index(x, y)] = true;
                int dx = Mathf.RoundToInt(it.Direction.x), dy = Mathf.RoundToInt(it.Direction.y);
                for (int sign = 1; sign >= -1; sign -= 2) // bouncers go back and forth
                {
                    int cx = x + dx * sign, cy = y + dy * sign;
                    while (DevSolver.InBounds(cx, cy) && (items[cx, cy] == null || items[cx, cy].IsKye))
                    {
                        _solver.Rays[DevSolver.Index(cx, cy)] = true;
                        cx += dx * sign;
                        cy += dy * sign;
                    }
                }
            }
        }
    }

    private int RandomDiamond()
    {
        int count = 0, pick = -1;
        for (int i = 0; i < _solver.Cells.Length; i++)
        {
            if (_solver.Cells[i] != DevSolver.Diamond) continue;
            count++;
            if (_rng.Next(count) == 0) pick = i; // reservoir sampling
        }
        return pick;
    }

    // ---- Reading the live grid -------------------------------------------------------------------------

    private void Snapshot(GameController gc)
    {
        var items = gc.Items;
        for (int x = 0; x < DevSolver.W; x++)
            for (int y = 0; y < DevSolver.H; y++)
                _solver.Cells[DevSolver.Index(x, y)] = Classify(items[x, y]);

        _solver.KyeX = gc.Kye.x;
        _solver.KyeY = gc.Kye.y;
        _solver.ComputeDanger();
    }

    private static byte Classify(Item it)
    {
        if (it == null || it.IsKye) return DevSolver.Empty;
        if (it.IsDiamond) return DevSolver.Diamond;
        if (it.IsDoor) return (byte)(DevSolver.Door + DirectionIndex(it.Direction));
        if (it.IsBlackhole)
            return it._blackhole != null && it._blackhole._munches <= 0 ? DevSolver.Hole : DevSolver.Solid;
        if (it.KillsPlayer) return DevSolver.Killer;
        if (it.IsSploder) return DevSolver.Solid; // pushing one starts its countdown
        if (it.DestroyOnPlayerContact) return DevSolver.Earth;
        if (it.IsStickerUD) return DevSolver.StickerUD;
        if (it.IsStickerLR) return DevSolver.StickerLR;
        if (it.IsPushable) return DevSolver.Push;
        return DevSolver.Solid;
    }

    private static int DirectionIndex(Vector2 d)
    {
        if (d.y > 0.5f) return 0;
        if (d.y < -0.5f) return 1;
        if (d.x < -0.5f) return 2;
        return 3;
    }
}
#endif
