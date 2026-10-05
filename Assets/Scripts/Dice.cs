using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using TMPro;

public class Dice : MonoBehaviour
{
    [SerializeField] Color color;
    [SerializeField] public int baseDamage = 5;
    [SerializeField] public float fireRate = 0.5f;
    [SerializeField] AnimationCurve waveDisolveCurve;

    [Space]

    [SerializeField] public int stepDamage = 1;
    [SerializeField] public float stepFireRate = 0.03f;

    [Space]

    [SerializeField] GameObject[] stages;
    [SerializeField] TMP_Text labelIncrese;
    [SerializeField] SpriteRenderer border;
    [SerializeField] public SpriteRenderer icon;
    [SerializeField] Projectile projectilePrefab;

    [Space]
    [SerializeField] public string title;
    [TextArea] public string info;

    public int idxInInventory = -1;

    [ReadOnlyField] public float finalFireRate;

    public Color Color => color;
    public int Stage { get; set; } = 0;
    [field:SerializeField, ReadOnlyField]
    public int IncreaseStage { get; set; } = 0;
    public Cell Cell { get; set; }
    public Team Team => team;

    protected Team team;
    protected Enemy target;
    protected User user;
    protected bool stopAtack;
    protected float currentRate;

    Transform gunWavePrefab;
    int idxFireableGun;

    public virtual void Init(User user, Team team, int stage)
    {
        this.user = user;
        this.team = team;
        Stage = stage;

        currentRate = fireRate;

        DisableAllStages();

        stages[Stage].SetActive(true);

        foreach (var stageView in stages)
        {
            foreach (Transform item in stageView.transform)
            {
                item.GetComponent<SpriteRenderer>().color = color;
            }
        }

        border.color = this is Chameleon ? Color.white : color;

        labelIncrese.gameObject.SetActive(false);
        //print(user);
        //print(idxInInventory);
        //print(user.inventory.Find(d => d.idx == idxInInventory));
        baseDamage += user.inventory.Find(d => d.idx == idxInInventory).lvl * stepDamage;

        gunWavePrefab = Resources.Load<Transform>("Gun Wave");

        if(waveDisolveCurve.keys.Length == 0)
        {
            waveDisolveCurve.AddKey(new() 
            { 
                time = 0,
                value = 0,
                inTangent = 4.3f,
                outTangent = 4.3f
            });

            waveDisolveCurve.AddKey(new()
            {
                time = 0.2f,
                value = 0.6f,
                inTangent = 0.9f,
                outTangent = 0.9f
            });

            waveDisolveCurve.AddKey(new()
            {
                time = 1f,
                value = 1f,
                inTangent = 0f,
                outTangent = 0f
            });
        }
    }

    public void AddDots()
    {
        if (Stage == Settings.MAX_STAGE)
            return;

        Stage++;

        DisableAllStages();

        stages[Stage].SetActive(true);
    }


    public void Increase()
    {
        if (IncreaseStage >= Settings.MAX_INCREASE_STAGE)
            return;

        DisableAllStages();

        IncreaseStage++;

        labelIncrese.text = RomanNumeral(IncreaseStage);

        labelIncrese.gameObject.SetActive(true);
    }

    // Считаем, а не перечисляем: при поднятии MAX_INCREASE_STAGE метка не отвалится
    public static string RomanNumeral(int value)
    {
        int[] weights = { 10, 9, 5, 4, 1 };
        string[] signs = { "X", "IX", "V", "IV", "I" };

        string result = string.Empty;

        for (int i = 0; i < weights.Length; i++)
        {
            while (value >= weights[i])
            {
                result += signs[i];
                value -= weights[i];
            }
        }

        return result;
    }

    void DisableAllStages()
    {
        foreach (var item in stages)
        {
            item.SetActive(false);
        }
    }

    protected virtual void Update()
    {
        Attack();

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.I))
        {
            AddDots();
        }
#endif

        finalFireRate = CurrentRateThreshold();
    }

    protected virtual void Attack()
    {
        currentRate += Time.deltaTime;

        if (currentRate < CurrentRateThreshold() || stopAtack)
            return;

        if (target)
        {
            var gunpoint = stages[Stage].transform.GetChild(idxFireableGun);
            var pos = gunpoint.position;
            pos.z = -70;
            var projectile = Instantiate(projectilePrefab, pos, Quaternion.identity);
            InitProjectile(projectile);

            idxFireableGun++;

            if (idxFireableGun > Stage)
                idxFireableGun = 0;

            currentRate = 0;

            target = null;

            if (IncreaseStage == 0)
            {
                var wave = Instantiate(gunWavePrefab, pos, Quaternion.identity);
                var renderer = wave.GetComponent<SpriteRenderer>();
                renderer.color = color;

                wave.LeanScale(Vector3.one * 1.3f, 0.18f).setEaseOutQuad();

                LeanTween.value(gameObject, a =>
                {
                    var c = renderer.color;
                    c.a = a;
                    renderer.color = c;
                }, 1f, 0, 0.19f).setEaseOutQuad();

                //LeanTween.value(gameObject, a =>
                //{
                //    var c = renderer.color;
                //    c.a = waveDisolveCurve.Evaluate(a);
                //    renderer.color = c;
                //}, 1f, 0, 0.19f);

                var sequence = LeanTween.sequence();

                sequence.append(gunpoint.LeanScale(Vector3.one * 0.7f, 0.1f).setEaseOutQuad());// .setEaseSpring();
                sequence.append(gunpoint.LeanScale(Vector3.one * 1.0f, 0.1f).setEaseOutQuad());// .setEaseSpring();
                sequence.append(() => Destroy(wave.gameObject));
            }
            else
            {
                var sequence = LeanTween.sequence();
                
                sequence.append(labelIncrese.transform.LeanScale(Vector3.one * 1.15f, 0.058f).setEaseOutQuad());// .setEaseSpring();
                sequence.append(labelIncrese.transform.LeanScale(Vector3.one * 1f, 0.078f).setEaseOutQuad());// .setEaseSpring();

                
            }
        }
        else
        {
            target = GetTarget();
        }
    }

    protected virtual float CurrentRateThreshold()
    {
        return fireRate / (Stage + 1) / ((IncreaseStage * IncreaseStage) + 1);
    }

    protected virtual void InitProjectile(Projectile projectile)
    {
        var damage = CalculateDamage();
        damage = CalculateCriticalDamage(damage, out bool isCrit);
        projectile.Init(target, damage, color);
    }

    protected virtual int CalculateDamage()
    {
        return baseDamage * (IncreaseStage * IncreaseStage + 1);
    }

    protected virtual int CalculateCriticalDamage(int damage, out bool isCrit)
    {
        isCrit = false;

        var chance = 5;
        if (IncreaseStage > 0)
        {
            chance *= IncreaseStage;
            if (IncreaseStage == 5)
            {
                chance = 100;
            }
        }
        if (Random.Range(0, 100) < chance)
        {
            damage += damage * (int)((float)user.crit * 0.01f);
            isCrit = true;
        }

        return damage;
    }

    protected virtual Enemy GetTarget()
    {
        var enemies = GameManager.Instance.allEnemies;

        if (GameManager.Instance.IsPVP)
        {
            enemies = GameManager.Instance.allEnemies.FindAll(e => e.Team == team);
        }

        if (enemies.Count == 0)
            return null;

        var minDist = enemies.Min(e => e.DistanceToFinish());
        return enemies.Find(e => Mathf.Approximately(minDist, e.DistanceToFinish()));


        //if(GameManager.Instance.IsPVP)
        //    return GameManager.Instance?.allEnemies.Find(e => e && e.Team == team);

        //return GameManager.Instance?.allEnemies.Find(e => e);
    }

    public void Stop()
    {
        stopAtack = true;
    }
}
