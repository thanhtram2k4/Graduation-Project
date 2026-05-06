using UnityEngine;
using TMPro;

public class GoldDisplay : MonoBehaviour
{
    public TextMeshProUGUI goldText;

    private void Update()
    {
        if (goldText != null && GameManager.Instance != null)
        {
            goldText.text = GameManager.Instance.currentGold.ToString();
        }
    }
}
