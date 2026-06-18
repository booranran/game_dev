using UnityEngine;

public class OpenSeatClickDetector : MonoBehaviour
{
    public SeatManager.SeatSlot seat;

    void OnMouseDown()
    {
        if (seat == null) return;
        TurnController.Instance?.OnOpenSeatSelected(seat);
    }
}
