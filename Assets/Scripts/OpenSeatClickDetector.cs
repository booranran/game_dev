using UnityEngine;

public class OpenSeatClickDetector : MonoBehaviour
{
    public SeatManager.SeatSlot seat;

    void OnMouseDown()
    {
        // UI 버튼이 화면상 이 콜리더 위에 겹쳐 있으면, 그 버튼 클릭이 뒤의 월드 콜리더까지 같이 트리거되는 걸 방지
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return;

        if (seat == null) return;
        TurnController.Instance?.OnOpenSeatSelected(seat);
    }
}
