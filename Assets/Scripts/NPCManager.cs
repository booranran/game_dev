using UnityEngine;
using TMPro;
using System;
using System.Collections.Generic;

public class NPCManager : MonoBehaviour
{
    public static NPCManager Instance;

    [Serializable]
    public class NPCData
    {
        public Sprite standingSprite;
        public Sprite sittingSprite;
    }

    [Header("NPC 데이터")]
    public NPCData elderly;
    public NPCData crush;
    // public NPCData pregnant;
    // public NPCData passenger;
    // public NPCData dog;
    // public NPCData crush;
    // public NPCData babyHappy;
    // public NPCData babyCrying;

    [Header("NPC 표시")]
    public SpriteRenderer npcSpriteRenderer;
    public TextMeshProUGUI npcNameText;
    public TextMeshProUGUI npcDialogueText;

    [Header("앉은 NPC 설정")]
    public int seatedNPCSortingOrder = -29;

    private List<GameObject> seatedNPCObjects = new List<GameObject>();

    [Header("NPC 서있는 위치 (공통)")]
    public Vector3 npcStandingPosition;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void OnEnable()
    {
        EventManager.OnEventGenerated += OnEventGenerated;
    }

    void OnDisable()
    {
        EventManager.OnEventGenerated -= OnEventGenerated;
    }

    void OnEventGenerated(EventManager.EventType eventType, EventManager.NPCType npcType)
    {
        if (eventType == EventManager.EventType.EmptySeat ||
            eventType == EventManager.EventType.None)
        {
            HideNPC();
            return;
        }
        ShowNPC(npcType, false);
    }

    public void ShowNPC(EventManager.NPCType npcType, bool afterYield)
    {
        NPCData data = GetData(npcType);
        if (data == null) return;

        if (afterYield)
        {
            if (npcSpriteRenderer) npcSpriteRenderer.sprite = null;

            Vector3 seatPos = GetNPCSittingPosition();
            GameObject obj = new GameObject("SeatedNPC");
            obj.transform.position = seatPos;
            obj.transform.localScale = Vector3.one * 0.029f;
            SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = data.sittingSprite;
            sr.sortingOrder = seatedNPCSortingOrder;
            seatedNPCObjects.Add(obj);
            var slot = GetCurrentSeatSlot();
            if (slot != null)
            {
                slot.seatedNPCObject = obj;
                SeatManager.Instance?.SetupSeatClickDetection(slot);
            }
            SeatManager.Instance?.NPCOccupySeat();
        }
        else
        {
            if (npcSpriteRenderer)
            {
                npcSpriteRenderer.sprite = data.standingSprite;
                npcSpriteRenderer.transform.position = npcStandingPosition;
            }
        }

        switch (npcType)
        {
            case EventManager.NPCType.Elderly:
                npcNameText.text = "노인";
                npcDialogueText.text = afterYield ? "고맙우이~" : "아이고~ 삭신이 쑤시네";
                break;
            case EventManager.NPCType.Crush:
                npcNameText.text = "이상형";
                npcDialogueText.text = "";
                break;
            // 추가 NPC는 스프라이트 준비 후 주석 해제
            // case EventManager.NPCType.Pregnant: ...
            // case EventManager.NPCType.Passenger: ...
            // case EventManager.NPCType.Dog: ...
            // case EventManager.NPCType.Crush: ...
            // case EventManager.NPCType.BabyHappy: ...
            // case EventManager.NPCType.BabyCrying: ...
        }
    }

    public void ShowIgnoreReaction(EventManager.NPCType npcType) { }

    public void ClearSeatedNPCs()
    {
        foreach (var obj in seatedNPCObjects)
            if (obj) Destroy(obj);
        seatedNPCObjects.Clear();
    }

    public void HideNPC()
    {
        if (npcSpriteRenderer) npcSpriteRenderer.sprite = null;
        if (npcNameText) npcNameText.text = "";
        if (npcDialogueText) npcDialogueText.text = "";
    }

    SeatManager.SeatSlot GetCurrentSeatSlot()
    {
        var sm = SeatManager.Instance;
        if (sm == null) return null;
        if (sm.currentEmptySeat != null) return sm.currentEmptySeat;
        if (sm.playerCurrentSeat != null) return sm.playerCurrentSeat;
        return null;
    }

    Vector3 GetNPCSittingPosition()
    {
        var slot = GetCurrentSeatSlot();
        if (slot != null && slot.seatPosition != null)
            return slot.seatPosition.position;
        return Vector3.zero;
    }

    NPCData GetData(EventManager.NPCType npcType)
    {
        switch (npcType)
        {
            case EventManager.NPCType.Elderly: return elderly;
            case EventManager.NPCType.Crush: return crush;
            // 추가 NPC는 스프라이트 준비 후 주석 해제
            default: return null;
        }
    }
}
