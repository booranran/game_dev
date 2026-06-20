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
        [HideInInspector] public GameObject spawnedIcon; // 동적으로 생성된 선택 아이콘 인스턴스
        [HideInInspector] public int index;
    }

    public SeatSlot[] seats;
    public bool randomizePlayerSeat = false;
    public int playerStartSeatIndex = 0;

    [Header("좌석 실루엣 랜덤 스프라이트 풀 (좌석 수보다 많아도 됨, 시작 시 중복 없이 배정)")]
    public Sprite[] silhouetteSpritePool;

    [Header("빈자리 선택 아이콘")]
    public GameObject selectableIconPrefab; // 좌석 위에 동적으로 생성될 프리팹 하나로 통일
    public float selectableIconOffsetX = 0f; // 좌석 x좌표에 더해지는 오프셋 (좌석마다 다름)
    public float selectableIconFixedY = 0.5f; // y좌표는 좌석과 무관하게 항상 이 값 사용

    [Header("빈자리 선택 콜리더 (의도적으로 좌석까지 덮도록 넓게 잡음 - 아이콘뿐 아니라 좌석 클릭도 선택되게)")]
    public Vector2 selectableIconColliderSize = new Vector2(2.8f, 5f); // 기존 프리팹에 박혀있던 값 그대로 이식
    public Vector2 selectableIconColliderOffset = new Vector2(-0.1f, -1.8f);

    public SeatSlot currentEmptySeat { get; private set; }
    public SeatSlot playerCurrentSeat { get; private set; }
    public List<SeatSlot> openSeats { get; } = new List<SeatSlot>(); // 선택 대기 중인 빈자리들 (1개 이상)

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        currentEmptySeat = null;
        AssignRandomSilhouetteSprites();
        for (int i = 0; i < seats.Length; i++)
        {
            seats[i].index = i;
            seats[i].isOccupied = true;
            SetupSeatClickDetection(seats[i]);
        }
    }

    // 게임 시작 시 풀에서 중복 없이 랜덤하게 뽑아서 좌석마다 다른 스프라이트로 배정
    void AssignRandomSilhouetteSprites()
    {
        if (silhouetteSpritePool == null || silhouetteSpritePool.Length == 0) return;

        var pool = new List<Sprite>(silhouetteSpritePool);
        foreach (var seat in seats)
        {
            if (seat.silhouette == null || pool.Count == 0) continue;
            var sr = seat.silhouette.GetComponentInChildren<SpriteRenderer>(true);
            if (sr == null) continue;

            int r = UnityEngine.Random.Range(0, pool.Count);
            sr.sprite = pool[r];
            pool.RemoveAt(r);
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

    // 한 번에 여러 자리를 동시에 열어서 openSeats에 등록 (플레이어가 그중 하나를 클릭해서 선택)
    public void OpenSeats(int count)
    {
        List<SeatSlot> occupied = new List<SeatSlot>();
        foreach (var seat in seats)
            if (seat.isOccupied) occupied.Add(seat);

        int n = Mathf.Min(count, occupied.Count);
        for (int i = 0; i < n; i++)
        {
            int r = UnityEngine.Random.Range(0, occupied.Count);
            var seat = occupied[r];
            occupied.RemoveAt(r);

            seat.isOccupied = false;
            if (seat.silhouette) seat.silhouette.SetActive(false);
            if (seat.seatedNPCObject)
            {
                Destroy(seat.seatedNPCObject);
                seat.seatedNPCObject = null;
            }

            openSeats.Add(seat);
            ActivateSelectableIcon(seat);
            Debug.Log($"[SeatManager] OpenSeats → index {seat.index} 열림, 아이콘 생성 {(seat.spawnedIcon != null ? "성공" : "실패(prefab/seatPosition 확인)")}");
        }

        Debug.Log($"[SeatManager] 빈자리 {n}개 열림 (요청 {count}개)");
    }

    // 열린 자리 중 하나를 플레이어가 선택 - 그 자리만 currentEmptySeat로 넘기고 나머지는 openSeats에 남음
    public void SelectOpenSeat(SeatSlot seat)
    {
        if (!openSeats.Remove(seat)) return;
        DeactivateSelectableIcon(seat);
        currentEmptySeat = seat;
    }

    // 좌석 하나를 선택한 직후, 남은 빈자리 아이콘들도 더 못 누르게 전부 비활성화 (중복 선택 방지)
    public void DeactivateAllOpenSeatIcons()
    {
        foreach (var seat in openSeats)
            DeactivateSelectableIcon(seat);
    }

    // openSeats에 남아있는 자리들을 전부 다른 랜덤 NPC로 채움 (서있기 선택 시 즉시, 다음 턴 시작 시 자동)
    public void FillAllOpenSeats()
    {
        foreach (var seat in openSeats)
        {
            DeactivateSelectableIcon(seat);
            FillSeatWithRandomNPC(seat);
        }
        openSeats.Clear();
    }

    // 특정 좌석에 다른 점유 좌석에서 빌려온 스프라이트로 랜덤 NPC를 채워 앉힘
    public void FillSeatWithRandomNPC(SeatSlot seat)
    {
        Debug.Log($"[SeatManager] FillSeatWithRandomNPC 호출 → index {seat.index} (호출 스택은 Console에서 이 로그 클릭하면 확인 가능)");
        if (seat.seatPosition == null) return;

        var pool = new List<GameObject>();
        foreach (var s in seats)
        {
            if (s.index == seat.index) continue;
            if (s.isOccupied && s.silhouette != null)
            {
                var sr = s.silhouette.GetComponentInChildren<SpriteRenderer>(true);
                if (sr && sr.sprite != null) pool.Add(s.silhouette);
            }
        }
        if (pool.Count == 0) return;

        if (seat.silhouette) seat.silhouette.SetActive(false);
        if (seat.seatedNPCObject)
        {
            Destroy(seat.seatedNPCObject);
            seat.seatedNPCObject = null;
        }

        var source = pool[UnityEngine.Random.Range(0, pool.Count)];
        var sourceSr = source.GetComponentInChildren<SpriteRenderer>(true);

        var go = new GameObject("FilledNPC");
        go.transform.position = seat.seatPosition.position;
        go.transform.localScale = source.transform.localScale; // 빌려온 원본 실루엣과 동일한 크기로 보이게
        var sr2 = go.AddComponent<SpriteRenderer>();
        sr2.sprite = sourceSr.sprite;
        sr2.sortingOrder = -29;

        seat.seatedNPCObject = go;
        seat.isOccupied = true;
        SetupSeatClickDetection(seat);
    }

    // 좌석 위치에 선택 아이콘을 생성 (Phase1 후보 표시, 빈자리 선택 등 공용으로 사용)
    // extraY: 좌석에 사람이 앉아있는 경우처럼 기본 높이보다 더 올려야 할 때 추가로 더하는 값
    public GameObject SpawnSeatIcon(SeatSlot seat, float extraY = 0f)
    {
        if (selectableIconPrefab == null || seat.seatPosition == null) return null;
        if (seat.spawnedIcon != null) return seat.spawnedIcon;

        Vector3 iconPos = seat.seatPosition.position;
        iconPos.x += selectableIconOffsetX;
        iconPos.y = selectableIconFixedY + extraY;

        var icon = Instantiate(selectableIconPrefab, iconPos, Quaternion.identity);
        var col = icon.GetComponent<BoxCollider2D>();
        if (col == null) col = icon.AddComponent<BoxCollider2D>();

        // 아이콘뿐 아니라 그 아래 좌석까지 클릭 영역으로 잡기 위해 의도적으로 넓게 설정 (인스펙터에서 조절)
        col.size = selectableIconColliderSize;
        col.offset = selectableIconColliderOffset;

        seat.spawnedIcon = icon;
        return icon;
    }

    public void RemoveSeatIcon(SeatSlot seat)
    {
        if (seat.spawnedIcon == null) return;
        Destroy(seat.spawnedIcon);
        seat.spawnedIcon = null;
    }

    void ActivateSelectableIcon(SeatSlot seat)
    {
        var icon = SpawnSeatIcon(seat);
        if (icon == null) return;

        var detector = icon.GetComponent<OpenSeatClickDetector>();
        if (detector == null)
            detector = icon.AddComponent<OpenSeatClickDetector>();
        detector.seat = seat;
    }

    void DeactivateSelectableIcon(SeatSlot seat) => RemoveSeatIcon(seat);

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
        if (seat.seatedNPCObject) // 그 자리가 FillSeatWithRandomNPC 등으로 이미 채워져있던 경우 - 실루엣만 꺼서는 안 사라짐
        {
            Destroy(seat.seatedNPCObject);
            seat.seatedNPCObject = null;
        }
    }

    public void ResetAllSeats()
    {
        foreach (var seat in seats)
        {
            seat.isOccupied = true;
            if (seat.silhouette) seat.silhouette.SetActive(true);
            DeactivateSelectableIcon(seat);
        }
        openSeats.Clear();
        currentEmptySeat = null;
    }
}
