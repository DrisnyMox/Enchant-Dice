using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using static Settings;

public class PlayerInput : MonoBehaviour
{
    Dice dice;
    Player player;
    bool splittingMode;

    private void Awake()
    {
        EventsHolder.playerSpawnedMine.AddListener(MinePlayer_Spawned);
        EventsHolder.onSplittingModeUpdated.AddListener(SplittingMode_Updated);
    }

    private void SplittingMode_Updated(bool value)
    {
        splittingMode = value;
    }

    private void MinePlayer_Spawned(Player player)
    {
        this.player = player;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            CheckClickToDice();
        }

        if (Input.GetMouseButton(0))
        {
            MoveDice();
        }

        if (Input.GetMouseButtonUp(0))
        {
            DropDice();
        }
    }

    void CheckClickToDice()
    {
        var mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        var hit = Physics2D.Raycast(mousePos, Vector2.zero);

        //var hits = Physics2D.RaycastAll(mousePos, Vector2.zero);

        if (hit)
        {
            var hitDice = hit.collider.GetComponent<Dice>();
            if (hitDice && hitDice.Team != Team.Other)
            {
                dice = hitDice;
                if (splittingMode)
                {
                    EventsHolder.onDiceToSplitClicked?.Invoke(dice);
                }
                else
                {
                    EventsHolder.onDiceClicked?.Invoke(dice);

                    if (dice.IncreaseStage > 0)
                        dice = null;
                }
            }
        }
    }

    void MoveDice()
    {
        if (!dice)
            return;

        var pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        dice.transform.position = new Vector3(pos.x, pos.y);
    }

    void DropDice()
    {
        if (!dice)
            return;

        var mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        var hits = Physics2D.RaycastAll(mousePos, Vector2.zero)
            .Select(h => h.collider.GetComponent<Dice>())
            .Where(d => d)
            .ToList();

        // Сливать можно только со своими кубиками, иначе кубик уезжает на чужой стол
        var otherDice = hits.Find(d => d != dice && d.Team == dice.Team);

        var typeCondition = otherDice && (dice.Color == otherDice.Color || (dice && dice is Chameleon) || otherDice is Chameleon);
        var canMerge = otherDice && dice.Stage == otherDice.Stage && typeCondition && dice.IncreaseStage < 1;

        // На максимальной стадии слияние поднимает пробуждение, но выше потолка его не поднять,
        // поэтому кубик не должен пропадать впустую
        if (canMerge && dice.Stage == MAX_STAGE && otherDice.IncreaseStage >= MAX_INCREASE_STAGE)
            canMerge = false;

        if (canMerge)
        {
            if(dice.Stage < MAX_STAGE)
            {
                EventsHolder.onDiceDestroyed?.Invoke(otherDice);
                EventsHolder.onDiceDestroyed?.Invoke(dice);

                var newDice = player.SpawnDice(otherDice.Cell);
                newDice.Init(player.user, player.team, otherDice.Stage + 1);
                Destroy(otherDice.gameObject);
                EventsHolder.onDiceMerged?.Invoke(newDice);
            }
            else
            {
                otherDice.Increase();
                EventsHolder.onDiceMerged?.Invoke(otherDice);
            }

            dice.Cell.IsEmpty = true;
            Destroy(dice.gameObject);
        }
        else
        {
            Move(dice);
        }

        dice = null;
    }

    void Move(Dice dice)
    {
        StartCoroutine(Delay());

        IEnumerator Delay()
        {
            var dist = Vector2.Distance(dice.Cell.transform.position, dice.transform.position);

            while (dist > 0.3f)
            {
                dice.transform.position = Vector3.MoveTowards(dice.transform.position, dice.Cell.transform.position, Time.deltaTime * 30);
                dist = Vector2.Distance(dice.Cell.transform.position, dice.transform.position);

                yield return null;
            }

            dice.transform.position = dice.Cell.transform.position;
        }
    }
}
