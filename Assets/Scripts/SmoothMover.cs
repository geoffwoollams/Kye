using UnityEngine;

public class SmoothMover : MonoBehaviour
{
    private Transform visual;
    private float speed = 18f;          // cells per second
    private float snapDistance = 2f;  // bigger jumps snap instead of sliding

    private Vector3 _visualPos;        // where the sprite is, tracked by us
    private bool _first = true;


    private void OnEnable()
    {
        _first = true;                 // re-snap if the item is re-activated or pooled
    }

    void Awake()
    {
        visual = transform.Find("Visual").transform;
        var item = gameObject.GetComponent<Item>();
        if (item != null)
        {
            if (item.IsKye)
            {
                speed = 25f;
            }
        }
    }

    private void LateUpdate()
    {
        if (visual == null) return;

        Vector3 target = transform.position;

        if (_first || Vector3.Distance(_visualPos, target) > snapDistance)
        {
            _visualPos = target;       // spawn or teleport: no slide
            _first = false;
        }
        else
        {
            _visualPos = Vector3.MoveTowards(_visualPos, target, speed * Time.deltaTime);
        }

        visual.position = _visualPos;  // override the position inherited from the parent
    }
}