using TMPro;
using UnityEngine;

public class UI_ChatPanel : MonoBehaviour
{
    public TextMeshProUGUI Text;

    public void SetText(string text)
    {
        Text.text = text;
    }
}
