using UnityEngine;

public class CharacterDisplay : MonoBehaviour
{
    public enum PoseOverride { None, Yielding, ElbowDefense }

    [Header("Student Sprites")]
    public Sprite studentStanding;
    public Sprite studentSitting;
    public Sprite studentYielding; // 양보하는 순간(이번 턴)만 잠깐 표시되는 포즈 (서있을 때)
    public Sprite studentElbowDefense; // 팔꿈치 게임 중(앉은 채로) 표시되는 포즈

    [Header("Worker Sprites")]
    public Sprite workerStanding;
    public Sprite workerSitting;
    public Sprite workerYielding;
    public Sprite workerElbowDefense;

    [Header("Student Positions")]
    public Vector3 studentStandingPosition;
    public Vector3 studentSittingPosition;

    [Header("Worker Positions")]
    public Vector3 workerStandingPosition;
    public Vector3 workerSittingPosition;

    private SpriteRenderer spriteRenderer;
    private Vector3? standingOverride;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void OnEnable()
    {
        GameManager.OnTurnProcessed += UpdateSprite;
        GameManager.OnGameInitialized += UpdateSprite;
    }

    void OnDisable()
    {
        GameManager.OnTurnProcessed -= UpdateSprite;
        GameManager.OnGameInitialized -= UpdateSprite;
    }

    // OnTurnProcessed/OnGameInitialized가 Action(무파라미터)이라서 구독용으로 별도 오버로드 필요
    public void UpdateSprite() => UpdateSprite(PoseOverride.None);

    public void UpdateSprite(PoseOverride pose)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        bool isSitting = gm.playerState == GameManager.PlayerState.Sitting;

        if (gm.characterType == GameManager.CharacterType.Student)
            spriteRenderer.sprite = isSitting
                ? (pose == PoseOverride.ElbowDefense ? studentElbowDefense : studentSitting)
                : (pose == PoseOverride.Yielding ? studentYielding : studentStanding);
        else
            spriteRenderer.sprite = isSitting
                ? (pose == PoseOverride.ElbowDefense ? workerElbowDefense : workerSitting)
                : (pose == PoseOverride.Yielding ? workerYielding : workerStanding);

        if (isSitting)
        {
            standingOverride = null;
            spriteRenderer.sortingOrder = -30;
            var seat = SeatManager.Instance?.playerCurrentSeat;
            if (seat != null && seat.seatPosition != null)
                transform.position = seat.seatPosition.position;
            else if (gm.characterType == GameManager.CharacterType.Student)
                transform.position = studentSittingPosition;
            else
                transform.position = workerSittingPosition;
        }
        else
        {
            spriteRenderer.sortingOrder = 1;
            var emptySeat = SeatManager.Instance?.currentEmptySeat;
            if (emptySeat != null && emptySeat.standingInFrontPosition != null)
                standingOverride = emptySeat.standingInFrontPosition.position;

            if (standingOverride.HasValue)
                transform.position = standingOverride.Value;
            else if (gm.characterType == GameManager.CharacterType.Student)
                transform.position = studentStandingPosition;
            else
                transform.position = workerStandingPosition;
        }
    }

    public void SetStandingOverride(Vector3 pos) => standingOverride = pos;

}
