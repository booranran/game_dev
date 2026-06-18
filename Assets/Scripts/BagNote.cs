using UnityEngine;

public class BagNote : MonoBehaviour
{
    public int lane;
    public bool isHit;
    public bool isMissed;

    private float fallSpeed;
    private float hitZoneY;
    private float missY;
    private BagDefenseController controller;
    private RectTransform rt;

    public void Init(int lane, float speed, float hitZoneY, float missY, BagDefenseController ctrl)
    {
        this.lane = lane;
        this.fallSpeed = speed;
        this.hitZoneY = hitZoneY;
        this.missY = missY;
        this.controller = ctrl;
        rt = GetComponent<RectTransform>();
    }

    void Update()
    {
        if (isHit || isMissed) return;
        rt.anchoredPosition += Vector2.down * fallSpeed * Time.deltaTime;
        if (rt.anchoredPosition.y < missY)
        {
            isMissed = true;
            controller.OnNoteMissed(this);
        }
    }

    public float GetY() => rt != null ? rt.anchoredPosition.y : 0f;
}
