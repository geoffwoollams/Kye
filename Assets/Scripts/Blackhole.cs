using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Blackhole : MonoBehaviour
{
    public Sprite[] SpritePool;

    private SpriteRenderer _spriteRenderer;
    public int _munches;
    private float _nextChange;
    
    private Item _item;

    void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _nextChange = Time.time + 0.5f;
        _munches = -1;
        _item = GetComponent<Item>();
    }

    public void Trigger()
    {
        _item.IsPushable = true;
        _munches = 4;
        _nextChange = Time.time;
    }

    public bool Consume(Item item)
    {
        if (item && item.IsKye)
        {
            Trigger();
            return true;
        }

        if (_munches > 0)
            return false;
        
        AudioManager.Instance.PlayBlackhole();

        Common.DestroyItem(item, false);
        Trigger();
        return true;
    }

    public void Tick()
    {
        if(Time.time > _nextChange)
        {
            if(_munches > 0 )
            {
                //_spriteRenderer.sprite = SpritePool[Common.GetRandom(4, 7)];
                _spriteRenderer.sprite = SpritePool[8 - _munches];
                _munches--;
            }
            else
            {
                _item.IsPushable = false;
                _spriteRenderer.sprite = SpritePool[Common.GetRandom(0, 3)];
            }

            _nextChange = Time.time + 0.5f;
        }
    }
}
