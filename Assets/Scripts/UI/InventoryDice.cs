using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using TMPro;

public class InventoryDice : MonoBehaviour
{
    [SerializeField] RectTransform rectTransform;
    [SerializeField] Image border;
    [SerializeField] Image icon;
    [SerializeField] Image dot;
    [SerializeField] TMP_Text labelIncrease;
    [SerializeField] Sprite chameleonSprite;

    [field: SerializeField]
    public Color Color { get; private set; }
    public RectTransform TransformUI => rectTransform;

    public bool inited;

    public void Init(Dice dice)
    {
        if(dice is Chameleon)
        {
            border.color = Color.white;
            border.sprite = chameleonSprite;
        }
        else
        {
            border.color = dice.Color;
        }

        dot.color = dice.Color;

        Color = dice.Color;

        icon.sprite = dice.icon.sprite;
        icon.color = dice.icon.color;

        labelIncrease.gameObject.SetActive(false);

        inited = true;
    }

    public void UpdateIncreaseView(int increaseStage)
    {
        labelIncrease.text = Dice.RomanNumeral(increaseStage);
        labelIncrease.color = Color;

        labelIncrease.gameObject.SetActive(true);
        dot.gameObject.SetActive(false);
    }

}
