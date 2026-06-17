using UnityEngine;
using System.Collections.Generic;
using System;

public class SeatManager : MonoBehaviour
{
    public static SeatManager Instance;

    [Serializable]
    public class SeatSlot
    {
        public Transform seatPosition;
        public Transform standingInFrontPosition;
        public GameObject silhouette;
        [HideInInspector] public bool isOccupied = true;
        [HideInInspector] public GameObject seatedNPCObject;
        [HideInInspector] public int index;
    }

    public SeatSlot[] seats;
    public bool randomizePlayerSeat = false;
    public int playerStartSeatIndex = 0;

    public SeatSlot currentEmptySeat { get; private set; }
    public SeatSlot playerCurrentSeat { get; private set; }

    public static event Action<bool> OnSeatOpened;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        currentEmptySeat = null;
        for (int i = 0; i < seats.Length; i++)
        {
            seats[i].index = i;
            seats[i].isOccupied = true;
            SetupSeatClickDetection(seats[i]);
        }
    }

    public void SetupSeatClickDetection(SeatSlot seat)
    {
        var obj = seat.silhouette ?? seat.seatedNPCObject;
        if (obj == null) return;

        if (obj.GetComponent<Collider2D>() == null)
            obj.AddComponent<BoxCollider2D>();

        var detector = obj.GetComponent<SeatClickDetector>();
        if (detector == null)
            detector = obj.AddComponent<SeatClickDetector>();
        detector.seat = seat;
    }

    public void HidePlayerSeat()
    {
        if (seats.Length == 0) return;
        if (randomizePlayerSeat)
            playerStartSeatIndex = UnityEngine.Random.Range(0, seats.Length);

        var slot = seats[playerStartSeatIndex];
        slot.isOccupied = false;
        if (slot.silhouette) slot.silhouette.SetActive(false);
        playerCurrentSeat = slot;
        Debug.Log($"[SeatManager] 플레이어 시작 자리 숨김 → index {playerStartSeatIndex}");
    }

    public void OpenRandomSeat(bool hasCompetingNPC)
    {
        List<SeatSlot> occupied = new List<SeatSlot>();
        foreach (var seat in seats)
            if (seat.isOccupied) occupied.Add(seat);

        Debug.Log($"[SeatManager] OpenRandomSeat 호출 | 점유된 자리: {occupied.Count}/{seats.Length}");

        if (occupied.Count == 0)
        {
            Debug.LogWarning("[SeatManager] 점유된 자리 없음 → 빈자리 못 열었음");
            return;
        }

        currentEmptySeat = occupied[UnityEngine.Random.Range(0, occupied.Count)];
        currentEmptySeat.isOccupied = false;
        if (currentEmptySeat.silhouette)
            currentEmptySeat.silhouette.SetActive(false);
        if (currentEmptySeat.seatedNPCObject)
        {
            Destroy(currentEmptySeat.seatedNPCObject);
            currentEmptySeat.seatedNPCObject = null;
        }

        Debug.Log($"[SeatManager] 빈자리 열림 → {currentEmptySeat.silhouette?.name}");
        OnSeatOpened?.Invoke(hasCompetingNPC);
    }

    public void OccupySeat()
    {
        if (currentEmptySeat == null) return;
        if (currentEmptySeat.seatedNPCObject != null)
        {
            Destroy(currentEmptySeat.seatedNPCObject);
            currentEmptySeat.seatedNPCObject = null;
        }
        currentEmptySeat.isOccupied = true;
        playerCurrentSeat = currentEmptySeat;
        currentEmptySeat = null;
    }

    public void ClearPlayerSeat()
    {
        if (playerCurrentSeat != null)
            playerCurrentSeat.isOccupied = true;
        playerCurrentSeat = null;
    }

    public void NPCOccupySeat()
    {
        if (currentEmptySeat != null)
        {
            currentEmptySeat.isOccupied = true;
            currentEmptySeat = null;
        }
    }

    public void RestoreCurrentEmptySeat()
    {
        if (currentEmptySeat == null) return;
        if (currentEmptySeat.seatedNPCObject != null)
        {
            Destroy(currentEmptySeat.seatedNPCObject);
            currentEmptySeat.seatedNPCObject = null;
        }
        currentEmptySeat.isOccupied = true;
        if (currentEmptySeat.silhouette)
            currentEmptySeat.silhouette.SetActive(true);
        currentEmptySeat = null;
    }

    public int GetCurrentEmptySeatCount()
    {
        int count = 0;
        foreach (var seat in seats)
            if (!seat.isOccupied) count++;
        return count;
    }

    public SeatSlot[] GetCandidateSeats(int count)
    {
        List<SeatSlot> occupied = new List<SeatSlot>();
        foreach (var seat in seats)
            if (seat.isOccupied) occupied.Add(seat);

        List<SeatSlot> result = new List<SeatSlot>();
        while (result.Count < count && occupied.Count > 0)
        {
            int r = UnityEngine.Random.Range(0, occupied.Count);
            result.Add(occupied[r]);
            occupied.RemoveAt(r);
        }
        return result.ToArray();
    }

    // Phase 1에서 정답 자리를 빈자리로 등록할 때 사용
    public void SetCurrentEmptySeat(SeatSlot seat)
    {
        currentEmptySeat = seat;
        seat.isOccupied = false;
        if (seat.silhouette) seat.silhouette.SetActive(false);
    }

    public void ResetAllSeats()
    {
        foreach (var seat in seats)
        {
            seat.isOccupied = true;
            if (seat.silhouette) seat.silhouette.SetActive(true);
        }
        currentEmptySeat = null;
    }
}
