using UnityEngine;

public class CharacterDisplay : MonoBehaviour
{
    [Header("Student Sprites")]
    public Sprite studentStanding;
    public Sprite studentSitting;

    [Header("Worker Sprites")]
    public Sprite workerStanding;
    public Sprite workerSitting;

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

    public void UpdateSprite()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        bool isSitting = gm.playerState == GameManager.PlayerState.Sitting;

        if (gm.characterType == GameManager.CharacterType.Student)
            spriteRenderer.sprite = isSitting ? studentSitting : studentStanding;
        else
            spriteRenderer.sprite = isSitting ? workerSitting : workerStanding;

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
