using System.Collections.Generic;
using UnityEngine;
using System;
using System.Text;

public class Common : MonoBehaviour
{
    private readonly struct ItemDef
    {
        public readonly string Name, NiceName, Code;
        public readonly Func<GameController, GameObject> Prefab;

        public ItemDef(string name, string niceName, string code, Func<GameController, GameObject> prefab)
        {
            Name = name;
            NiceName = niceName;
            Code = code;
            Prefab = prefab;
        }
    }

    // One row per item type. Wall codes follow the numpad and are flipped vertically: wall1 is '7', wall7 is '1'.
    private static readonly ItemDef[] ItemDefs =
    {
        new ItemDef("clear", "Eraser", " ", null),
        new ItemDef("kye", "Kye", "K", g => g.ItemKye),
        new ItemDef("diamond", "Diamond", "*", g => g.ItemDiamond),
        new ItemDef("wall1", "Wall", "7", g => g.Item1),
        new ItemDef("wall2", "Wall", "8", g => g.Item2),
        new ItemDef("wall3", "Wall", "9", g => g.Item3),
        new ItemDef("wall4", "Wall", "4", g => g.Item4),
        new ItemDef("wall5", "Wall", "5", g => g.Item5),
        new ItemDef("wall6", "Wall", "6", g => g.Item6),
        new ItemDef("wall7", "Wall", "1", g => g.Item7),
        new ItemDef("wall8", "Wall", "2", g => g.Item8),
        new ItemDef("wall9", "Wall", "3", g => g.Item9),
        new ItemDef("earth", "Earth", "e", g => g.ItemEarth),
        new ItemDef("blocksquare", "Block Square", "b", g => g.ItemBlockSquare),
        new ItemDef("blockrocky", "Block Rocky", "B", g => g.ItemBlockRound),
        new ItemDef("sliderup", "Slider Up", "u", g => g.ItemSliderUp),
        new ItemDef("sliderdown", "Slider Down", "d", g => g.ItemSliderDown),
        new ItemDef("sliderleft", "Slider Left", "l", g => g.ItemSliderLeft),
        new ItemDef("sliderright", "Slider Right", "r", g => g.ItemSliderRight),
        new ItemDef("stickertb", "Sticker TB", "s", g => g.ItemStickerTB),
        new ItemDef("stickerlr", "Sticker LR", "S", g => g.ItemStickerLR),
        new ItemDef("bouncerup", "Bouncer Up", "U", g => g.ItemBouncerUp),
        new ItemDef("bouncerdown", "Bouncer Down", "D", g => g.ItemBouncerDown),
        new ItemDef("bouncerleft", "Bouncer Left", "L", g => g.ItemBouncerLeft),
        new ItemDef("bouncerright", "Bouncer Right", "R", g => g.ItemBouncerRight),
        new ItemDef("rockyup", "Rocky Up", "^", g => g.ItemRockyUp),
        new ItemDef("rockydown", "Rocky Down", "v", g => g.ItemRockyDown),
        new ItemDef("rockyleft", "Rocky Left", "<", g => g.ItemRockyLeft),
        new ItemDef("rockyright", "Rocky Right", ">", g => g.ItemRockyRight),
        new ItemDef("twister", "Twister", "T", g => g.ItemTwister),
        new ItemDef("gnasher", "Gnasher", "E", g => g.ItemGnasher),
        new ItemDef("blob", "Blob", "C", g => g.ItemBlob),
        new ItemDef("virus", "Virus", "~", g => g.ItemVirus),
        new ItemDef("spike", "Spike", "[", g => g.ItemSpike),
        new ItemDef("anticlocker", "Anti-Clocker", "a", g => g.ItemAntiClocker),
        new ItemDef("clocker", "Clocker", "c", g => g.ItemClocker),
        new ItemDef("autoslider", "Auto Slider", "A", g => g.ItemAutoSlider),
        new ItemDef("autorocky", "Auto Rocky", "F", g => g.ItemAutoRocky),
        new ItemDef("blackhole", "Blackhole", "H", g => g.ItemBlackhole),
        new ItemDef("doorlr", "Door LR", "f", g => g.ItemDoorLR),
        new ItemDef("doorrl", "Door RL", "g", g => g.ItemDoorRL),
        new ItemDef("doorud", "Door UD", "h", g => g.ItemDoorUD),
        new ItemDef("doordu", "Door DU", "i", g => g.ItemDoorDU),
        new ItemDef("timer3", "Timer 3", "}", g => g.ItemTimer3),
        new ItemDef("timer4", "Timer 4", "|", g => g.ItemTimer4),
        new ItemDef("timer5", "Timer 5", "{", g => g.ItemTimer5),
        new ItemDef("timer6", "Timer 6", "z", g => g.ItemTimer6),
        new ItemDef("timer7", "Timer 7", "y", g => g.ItemTimer7),
        new ItemDef("timer8", "Timer 8", "x", g => g.ItemTimer8),
        new ItemDef("timer9", "Timer 9", "w", g => g.ItemTimer9),
        new ItemDef("sploder", "Sploder", "!", g => g.ItemSploder),
        new ItemDef("tracker", "Tracker", "#", g => g.ItemTracker),
        new ItemDef("guard", "Guard", "%", g => g.ItemGuard),
    };

    private static readonly Dictionary<string, ItemDef> ItemsByName = BuildItemMap(d => d.Name);
    private static readonly Dictionary<string, ItemDef> ItemsByCode = BuildItemMap(d => d.Code);

    private static Dictionary<string, ItemDef> BuildItemMap(Func<ItemDef, string> key)
    {
        var map = new Dictionary<string, ItemDef>(ItemDefs.Length);
        foreach (var def in ItemDefs)
            map.Add(key(def), def);
        return map;
    }

    public static GameObject GetPrefabFromItemChar(string itemCode)
    {
        if (itemCode != null && ItemsByCode.TryGetValue(itemCode, out var def) && def.Prefab != null)
            return def.Prefab(GameController.Instance);

        return null;
    }

    public static GameObject UnitPrefab(string itemName)
    {
        var code = GetKyeCodeFromItem(itemName);
        var prefab = GetPrefabFromItemChar(code);
        return prefab;
    }

    public static string ItemNameToNiceName(string itemName)
    {
        if (itemName != null && ItemsByName.TryGetValue(itemName, out var def))
            return def.NiceName;

        return "Unknown";
    }

    public static string GetKyeCodeFromItem(string itemName)
    {
        if (itemName != null && ItemsByName.TryGetValue(itemName, out var def))
            return def.Code;

        return " ";
    }

    public static string GetItemNameFromKyeCode(string kyeCode)
    {
        if (kyeCode != null && ItemsByCode.TryGetValue(kyeCode, out var def))
            return def.Name;

        return "clear";
    }

    // Wall prefabs are named wall1..wall9, so their clones start with "wall".
    public static bool IsWall(Item item)
    {
        return item != null && item.name.StartsWith("wall");
    }

    public static bool IsWallCode(string code)
    {
        return code != null && code.Length == 1 && code[0] >= '1' && code[0] <= '9';
    }

    public static bool CanOccupyBy(int x, int y, Item item)
    {
        if (item == null)
            return false;

        var direction = new Vector2(x, y);
        x += (int)item.transform.position.x;
        y += (int)item.transform.position.y;

        if (!InBounds(x, y))
            return false;

        var spotItem = GetItem(x, y);
        if (spotItem)
        {
            if (spotItem.IsBlackhole)
                return true;
            if (spotItem.IsDoor)
                return spotItem.Direction == direction;
            if (spotItem.DestroyOnPlayerContact)
                return true;
            if (!spotItem.CanCurrentlyBePushed(direction))
                return false;
        }

        return true;
    }

    public static bool CanMoveTo(int x, int y, Item item)
    {
        if (item == null)
            return false;

        Item spotItem = GetItem(x, y);
        
        if (spotItem != null && spotItem.IsDoor)
        {
            Vector2 moveDirection = new Vector2(x - item.x, y - item.y);
            if (item.IsKye)
            {
                return moveDirection == spotItem.Direction;
            }
            
            Enemy itemEnemy = item._enemy;
            if (itemEnemy && itemEnemy.IsSmort)
            {
                return moveDirection == spotItem.Direction;
            }

            return false;
        }
        
        if (spotItem)
            return false;

        if (!InBounds(x, y)) return false;

        return true;
    }

    public static Item GetItemBy(Vector2 pos, Item item) { return GetItem(item.transform.position.x + pos.x, item.transform.position.y + pos.y); }
    public static Item GetItemBy(int x, int y, Item item) { return GetItem(item.transform.position.x + x, item.transform.position.y + y); }
    public static Item GetItem(Vector2 pos) { return GetItem((int)pos.x, (int)pos.y); }
    public static Item GetItem(float x, float y) { return GetItem((int)x, (int)y); }
    public static Item GetItem(int x, int y)
    {
        if(!InBounds(x, y)) return null;
        return GameController.Instance.Items[x, y];
    }

    public static void MoveItemBy(Vector2 delta, Item item) { MoveItem(item.transform.position.x + delta.x, item.transform.position.y + delta.y, item); }
    public static void MoveItem(float x, float y, Item item) { MoveItem((int)x, (int)y, item); }
    public static void MoveItem(int x, int y, Item item)
    {
        if (item == null || !InBounds(x, y)) return;

        int oldX = Mathf.RoundToInt(item.transform.position.x);
        int oldY = Mathf.RoundToInt(item.transform.position.y);
        Item oldSpot = GetItem(oldX, oldY);
        Item newSpot = GetItem(x, y);

        // Kye can stand ON a door; the door keeps owning that grid cell.
        bool leavingDoor  = item.IsKye && oldSpot != null && oldSpot.IsDoor;
        bool enteringDoor = item.IsKye && newSpot != null && newSpot.IsDoor;

        if (!leavingDoor && oldSpot == item) SetItem(oldX, oldY, null);
        if (!enteringDoor)                   SetItem(x, y, item);

        item.IsStuckToSticker = false;
        item.transform.position = new Vector3(x, y, 0);
        item.x = x;
        item.y = y;
    }

    public static void Effect(int x, int y)
    {
        Instantiate(GameController.Instance.SplodeEffect, new Vector3(x, y, 0), GameController.Instance.SplodeEffect.transform.rotation, GameController.Instance.ItemContainer);
    }

    public static void DestroyItem(Item item, bool showEffect = true)
    {
        if (item == null) return;
        if (GetItem(item.x, item.y) == item) SetItem(item.x, item.y, null);
        if (showEffect) Effect(item.x, item.y);
        item.gameObject.SetActive(false);
        Destroy(item.gameObject);
    }

    public static void SetItem(float x, float y, Item item) { SetItem((int)x, (int)y, item); }
    public static void SetItem(int x, int y, Item item)
    {
        GameController.Instance.Items[x, y] = item;
    }

    public static bool IsEmptyBy(Vector2 delta, Item item) { return IsEmpty((Vector2)item.transform.position + delta); }
    public static bool IsEmpty(Vector2 spot) { return IsEmpty((int)spot.x, (int)spot.y); }
    public static bool IsEmpty(float x, float y) { return IsEmpty((int)x, (int)y); }
    public static bool IsEmpty(int x, int y)
    {
        if(!InBounds(x, y)) return false;
        var item = GameController.Instance.Items[x, y];
        if(item == null)
            return true;
        return false;
    }

    public static bool InBounds(Vector2 position) { return InBounds((int)position.x, (int)position.y); }
    public static bool InBounds(float x, float y) { return InBounds((int)x, (int)y); }
    public static bool InBounds(int x, int y)
    {
        if(x >= 1 && x <= 28 && y >= 1 && y <= 18)
            return true;
        return false;
    }

    public static string GetDefaultFilePath()
    {
#if UNITY_ANDROID || UNITY_IOS || UNITY_WEBGL
        return Application.persistentDataPath;
#else
        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrEmpty(docs) && !docs.EndsWith("Documents"))
        {
            string sub = System.IO.Path.Combine(docs, "Documents");
            if (System.IO.Directory.Exists(sub))
                docs = sub;
        }

        if (string.IsNullOrEmpty(docs) || !System.IO.Directory.Exists(docs))
            docs = Application.persistentDataPath;
        return docs;
#endif
    }

    public static Color ChangeAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    public static int GetRandom(int maxExclusive) { return GetRandom(0, maxExclusive); }
    public static int GetRandom(int min, int maxExclusive)
    {
        return UnityEngine.Random.Range(min, maxExclusive);
    }

    public static string Base64Encode(string text)
    {
        byte[] bytesToEncode = Encoding.UTF8.GetBytes(text);
        return Convert.ToBase64String(bytesToEncode);
    }

    public static string Base64Decode(string text)
    {
        byte[] decodedBytes = Convert.FromBase64String(text);
        return Encoding.UTF8.GetString (decodedBytes);
    }

    public static void Rot90(ref Item item, bool anti = false)
    {
        if(item == null)
            return;
        
        if(anti == true)
        {
            if(item.Direction == Vector2.up) item.Direction = Vector2.right;
            else if(item.Direction == Vector2.right) item.Direction = Vector2.down;
            else if(item.Direction == Vector2.down) item.Direction = Vector2.left;
            else if(item.Direction == Vector2.left) item.Direction = Vector2.up;
        }
        else
        {
            if(item.Direction == Vector2.up) item.Direction = Vector2.left;
            else if(item.Direction == Vector2.right) item.Direction = Vector2.up;
            else if(item.Direction == Vector2.down) item.Direction = Vector2.right;
            else if(item.Direction == Vector2.left) item.Direction = Vector2.down;
        }

        item.transform.Rotate(0, 0, 90 * (anti ? -1 : 1));
    }


    public static void StickersFollowKye(Vector2 kye)
    {
        var left = GetItem(kye.x - 2, kye.y);
        var right = GetItem(kye.x + 2, kye.y);
        var down = GetItem(kye.x, kye.y - 2);
        var up = GetItem(kye.x, kye.y + 2);

        if(up && up.IsStickerUD) up.Move(Vector2.down, true, true);
        if(down && down.IsStickerUD) down.Move(Vector2.up, true, true);
        if(left && left.IsStickerLR) left.Move(Vector2.right, true, true);
        if(right && right.IsStickerLR) right.Move(Vector2.left, true, true);
    }

    public static void PullToSticker(Item item)
    {
        if (item == null)
            return;

        if (item.IsStuckToSticker)
            return;

        if(StickToTouching(item))
            return;

        var movement = SurroundingStickers(item.x, item.y);
        if(movement == Vector2.zero)
        {
            return;
        }
        if(Common.IsEmpty(item.x + movement.x, item.y + movement.y))
        {
            item.Move(movement);
            item.IsStuckToSticker = true;
        }
    }

    private static bool StickToTouching(Item item)
    {
        if (item == null)
            return false;

        var x = item.x;
        var y = item.y;

        var left = GetItem(x - 1, y);
        var right = GetItem(x + 1, y);
        var down = GetItem(x, y - 1);
        var up = GetItem(x, y + 1);

        item.IsStuckToSticker = false;

        if(left && left.IsStickerLR) item.IsStuckToSticker = true;
        if(right && right.IsStickerLR) item.IsStuckToSticker = true;
        if(down && down.IsStickerUD) item.IsStuckToSticker = true;
        if(up && up.IsStickerUD) item.IsStuckToSticker = true;

        return item.IsStuckToSticker;
    }

    private static Vector2 SurroundingStickers(int x, int y)
    {
        var left = GetItem(x - 2, y);
        var right = GetItem(x + 2, y);
        var down = GetItem(x, y - 2);
        var up = GetItem(x, y + 2);

        if((left && left.IsStickerLR) || (right && right.IsStickerLR) || (up && up.IsStickerUD) || (down && down.IsStickerUD))
        {
            return new Vector2(
                (left && left.IsStickerLR) ? -1 : ((right && right.IsStickerLR) ? 1 : 0),
                (down && down.IsStickerUD) ? -1 : ((up && up.IsStickerUD) ? 1 : 0)
            );
        }

        return Vector2.zero;
    }

    private static List<string> _levelKeys;
    private static Dictionary<string, int> _levelIndex;

    // Built-in levels in progression order (the dictionary's insertion order).
    private static void EnsureLevelIndex()
    {
        var classicLevels = Levels.Classic;
        if (_levelKeys != null && _levelKeys.Count == classicLevels.Count)
            return;

        _levelKeys = new List<string>(classicLevels.Keys);
        _levelIndex = new Dictionary<string, int>(_levelKeys.Count);
        for (int i = 0; i < _levelKeys.Count; i++)
            _levelIndex[_levelKeys[i]] = i;
    }

    public static bool IsNextLevelUnlocked(string levelName)
    {
        if (AppController.Instance.settings.UnlockAllLevels)
            return true;
        
        var nextName = GetNextLevelKey(levelName);
        return IsLevelUnlocked(nextName);
    }

    public static bool IsLevelUnlocked(string levelName)
    {
        if (AppController.Instance.settings.UnlockAllLevels)
            return true;
        
        levelName = levelName.ToLower().Trim();

        if (levelName == "")
            levelName = "first";

        EnsureLevelIndex();

        if (!_levelIndex.TryGetValue(levelName, out int levelIdx))
            return true;

        string currentUnlocked = AppController.Instance.settings.UnlockedLevel.ToLower().Trim();

        // An unknown unlocked level unlocks everything.
        if (!_levelIndex.TryGetValue(currentUnlocked, out int unlockedIdx))
            return true;

        return levelIdx <= unlockedIdx;
    }

    public static void UpdateUnlockedLevel(string levelName)
    {
        levelName = levelName.ToLower().Trim();

        EnsureLevelIndex();

        if (!_levelIndex.TryGetValue(levelName, out int newIndex)) return;

        string currentUnlocked = AppController.Instance.settings.UnlockedLevel.ToLower().Trim();
        if (!_levelIndex.TryGetValue(currentUnlocked, out int currentIndex))
            currentIndex = -1;

        if (newIndex > (currentIndex - 1))
        {
            AppController.Instance.settings.UnlockedLevel = GetNextLevelKey(levelName);
            AppController.Instance.Save();
        }
    }

    public static string GetNextLevelKey(string currentKey)
    {
        EnsureLevelIndex();

        if (currentKey == null || !_levelIndex.TryGetValue(currentKey, out int currentIndex))
            return currentKey;

        int nextIndex = currentIndex + 1;
        
        if (nextIndex >= _levelKeys.Count)
            return currentKey;

        return _levelKeys[nextIndex];
    }
    
    public static string FormatTime(float? seconds)
    {
        if (seconds == null || seconds <= 0)
            return string.Empty;

        int totalSeconds = (int)seconds.Value;
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int secs = totalSeconds % 60;

        return hours > 0
            ? $"{hours:D2}:{minutes:D2}:{secs:D2}"
            : $"{minutes:D2}:{secs:D2}";
    }
}

