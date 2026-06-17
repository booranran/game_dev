using UnityEngine;

public class SeatClickDetector : MonoBehaviour
{
    public SeatManager.SeatSlot seat;

    void OnMouseDown()
    {
        if (seat == null) return;
        EyeGameController.Instance?.OnSelectCandidate(seat.index);
    }
}
