using UnityEngine;

public class CharacterDisplay : MonoBehaviour
{
    public enum PoseOverride { None, Yielding, ElbowDefense, JustSat, Phase1, Phase2 }

    [Header("Student Sprites")]
    public Sprite studentStanding;
    public Sprite studentSitting;
    public Sprite studentYielding; // 양보하는 순간(이번 턴)만 잠깐 표시되는 포즈 (서있을 때)
    public Sprite studentElbowDefense; // 팔꿈치 게임 중(앉은 채로) 표시되는 포즈
    public Sprite studentJustSat; // 눈치게임 승리로 막 앉은 그 턴에만 표시 (다음 턴부터 평소 앉은 포즈로)
    public Sprite studentPhase1; // 눈치게임 Phase1(관찰) 중 표시 (서있을 때) - 다음 턴부터 평소 서있는 포즈로
    public Sprite studentPhase2; // 눈치게임 Phase2(경쟁) 중 표시 (서있을 때) - 다음 턴부터 평소 서있는 포즈로

    [Header("Worker Sprites")]
    public Sprite workerStanding;
    public Sprite workerSitting;
    public Sprite workerYielding;
    public Sprite workerElbowDefense;
    public Sprite workerJustSat;
    public Sprite workerPhase1;
    public Sprite workerPhase2;

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
                ? (pose == PoseOverride.ElbowDefense ? studentElbowDefense : pose == PoseOverride.JustSat ? studentJustSat : studentSitting)
                : (pose == PoseOverride.Yielding ? studentYielding : pose == PoseOverride.Phase1 ? studentPhase1 : pose == PoseOverride.Phase2 ? studentPhase2 : studentStanding);
        else
            spriteRenderer.sprite = isSitting
                ? (pose == PoseOverride.ElbowDefense ? workerElbowDefense : pose == PoseOverride.JustSat ? workerJustSat : workerSitting)
                : (pose == PoseOverride.Yielding ? workerYielding : pose == PoseOverride.Phase1 ? workerPhase1 : pose == PoseOverride.Phase2 ? workerPhase2 : workerStanding);

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
