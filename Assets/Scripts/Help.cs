using UnityEngine;
using UnityEngine.UI;

public class Help : MonoBehaviour
{
    private int offsetY = 0;
    
    void Start()
    {
        var i = GameController.Instance;
        
        InsertItem(null, "Controls", "Keyboard (arrow keys and WASD), Controller (sticks and dpad), Mouse (click or right click to enable auto follow), Touchscreen swipe.");
        InsertItem(null, "Shortcuts", "In game: R = Restart, N = Next level, P = Previous level.\nIn editor: P = Play. Bumpers/PGDN/PGUP/TAB/ScrollWheel = Change item");
        
        InsertItem(i.ItemKye, "Kye", "You are Kye. Move around the level to collect diamonds. You can push some objects to navigate levels");
        InsertItem(i.ItemDiamond, "Diamond", "Collect all diamonds to complete the level", 90);
        
        InsertItem(i.ItemEarth, "Earth", "A basic block which disappears when Kye steps on it");
        InsertItem(i.ItemBlockSquare, "Block Square", "A basic block which can be moved");
        InsertItem(i.ItemBlockRound, "Block Round", "A round block which can be moved and lets some other objects slide past");
        InsertItem(i.ItemSliderUp, "Slider", "Block type units which moves forward into empty space", 90);
        InsertItem(i.ItemStickerLR, "Sticker", "Stickers are movable items which pull other items towards them and hold them in place", 90);
        InsertItem(i.ItemBouncerUp, "Bouncer", "Bouncers move forward and turn around when they collide with another object", 90);
        InsertItem(i.ItemRockyUp, "Rocky", "These units move forward and can slide past other objects", 90);
        
        InsertItem(i.ItemClocker, "Clocker", "Clockers and Anti-clockers rotate objects which hit them");
        InsertItem(i.ItemAutoSlider, "Auto Slider", "Spawns Sliders", 90);
        InsertItem(i.ItemAutoRocky, "Auto Rocky", "Spawns Rockys", 90);
        
        InsertItem(i.ItemBlackhole, "Black Hole", "Eats objects which move into it");
        InsertItem(i.ItemTimer9, "Timer", "Counts down to zero then disappears");
        InsertItem(i.ItemDoorLR, "Door", "Doors can only be passed through in one direction. Careful!", 90);
        
        InsertItem(i.ItemTwister, "Twister", "[REDACTED]", 45);
        InsertItem(i.ItemGnasher, "Gnasher", "[REDACTED]");
        InsertItem(i.ItemBlob, "Blob", "[REDACTED]", 90);
        InsertItem(i.ItemVirus, "Virus", "[REDACTED]", 90);
        InsertItem(i.ItemSpike, "Spike", "[REDACTED]", 45);
        InsertItem(i.ItemTracker, "Tracker", "A smart monster which hunts Kye", 20);
        InsertItem(i.ItemGuard, "Guard", "A monster which moves along walls");
        
        InsertItem(i.Item5, "Wall", "A wall. Corners on walls can allow some units to slide past");
        InsertItem(i.ItemSploder, "Sploder", "Can be touched 5 times before exploding and destroying nearby objects", 18);

        var t = this.transform as RectTransform;
        t.sizeDelta = new Vector2(t.sizeDelta.x, offsetY * -1);
    }

    private void InsertItem(GameObject prefab, string title, string notes)
    {
        InsertItem(prefab, title, notes, 0);
    }
    
    private void InsertItem(GameObject prefab, string title, string notes, int rotate)
    {
        var item = Instantiate(GameController.Instance.HelpItem, gameObject.transform);
        var rect = item.GetComponent<RectTransform>();
        rect.localPosition = new Vector2(rect.localPosition.x, offsetY);
        offsetY -= 80;
        
        item.transform.Find("Title").GetComponent<TMPro.TMP_Text>().text = title;
        item.transform.Find("Notes").GetComponent<TMPro.TMP_Text>().text = notes;
        var img = item.transform.Find("Image");
        
        if (prefab == null)
        {
            if (title == "Shortcuts")
                img.GetComponent<Image>().sprite = GameController.Instance.helpShortcuts;
            else if (title == "Controls")
                img.GetComponent<Image>().sprite = GameController.Instance.helpControls;
        }
        else
        {
            var sr = prefab.GetComponent<SpriteRenderer>();
            img.GetComponent<Image>().sprite = sr.sprite;
            
            var spriteLoop = prefab.GetComponent<SpriteLoop>();
            if (spriteLoop != null)
            {
                var sl = img.gameObject.AddComponent<SpriteLoop>();
                sl.SpritePool = spriteLoop.SpritePool;
                sl.Delay  = spriteLoop.Delay;
                sl.StopOnTimeScale = false;
            }
        }
        


        if (rotate > 0)
        {
            var rt = img.gameObject.AddComponent<RotateEvery>();
            rt.RotateAmount = rotate;
            rt.Timer = 1f;
            rt.Reverse = true;
        }
    }
}
