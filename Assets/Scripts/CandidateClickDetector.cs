using UnityEngine;

public class CandidateClickDetector : MonoBehaviour
{
    public SeatManager.SeatSlot seat;

    void OnMouseDown()
    {
        if (seat == null) return;
        EyeGameController.Instance?.OnSelectCandidate(seat.index);
    }
}
