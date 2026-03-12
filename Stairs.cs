using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Stairs : MonoBehaviour
{
    public Material buildMat;
    public Material whiteMat;

    public AnimationCurve animationCurve;
    public AnimationCurve scaleAnimation;
    public AnimationCurve materialAnimation;

    public LevelManager levelManager;

    public int childCount = 0;
    GameObject offset;

    bool canAcceptStones = true;
    bool canAcceptCrystal = true;

    int currentStair = 0;
    public int stairCount = 0;
    int totalBuiltStairs = 0;

    public int width = 3;
    public int height = 2;

    public struct StoneSlot
    {
        public Vector3 arrayPos;
        public GameObject stone;
        public GameObject originStone;
    }

    StoneSlot[,] stoneMatrix;

    Vector3 startPos;

    public Sprite image;
    GameObject go;
    SpriteRenderer sr;

    float cooldown = 1f;
    float lastbuilt = 3f;

    public bool takeStoneFromMinion = true;
    public bool takeCrystalFromMinion = true;

    Wizard wizard;
    Player player;

    [Header("Crystal")]
    public Sprite crystalImage;
    Transform crystalSlot;
    GameObject currentCrystal;
    public bool hasCrystal = false;

    void Start()
    {
        levelManager = FindFirstObjectByType<LevelManager>();
        wizard = FindFirstObjectByType<Wizard>();
        player = FindFirstObjectByType<Player>();

        go = new GameObject("MySpriteObject");
        sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = image;
        go.transform.position = new Vector3(0, -100, 0);

        offset = new GameObject("rotationObject");
        offset.transform.SetParent(wizard.transform);
        offset.transform.position = wizard.transform.position + new Vector3(0, 5, 0);

        startPos = new Vector3(transform.position.x, transform.position.y + 5, transform.position.z);

        stoneMatrix = new StoneSlot[width, height];

        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                Vector3 localOffset = new Vector3(i * -0.7f, j * 1.0f + i * -0.15f, i * 0.1f);
                Vector3 rotatedOffset = offset.transform.TransformDirection(localOffset);
                stoneMatrix[i, j].arrayPos = rotatedOffset;
            }
        }

        crystalSlot = new GameObject("CrystalSlot").transform;
        crystalSlot.SetParent(offset.transform);
        crystalSlot.localPosition = new Vector3(1.2f, 0.5f, 0);

        childCount = transform.childCount;
    }

    void Update()
    {
        takeStoneFromMinion = (stairCount < width * height) && canAcceptStones && currentStair < childCount - 1;
        takeCrystalFromMinion = !hasCrystal && canAcceptCrystal && currentStair < childCount - 1;

        if (player == null)
            player = FindFirstObjectByType<Player>();

        offset.transform.LookAt(Camera.main.transform);
    }

    void AddTrail(GameObject go)
    {
        if (go == null) return;

        TrailRenderer trail = go.AddComponent<TrailRenderer>();

        trail.time = 0.1f;
        trail.startWidth = 0.2f;
        trail.endWidth = 0f;

        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.startColor = Color.white;
    }

    public void CollectStones()
    {
        if (!takeStoneFromMinion) return;

        StoneSlot currentStoneSlot = stoneMatrix[stairCount % width, stairCount / width];

        currentStoneSlot.originStone = transform.GetChild(currentStair).gameObject;

        currentStoneSlot.stone = Instantiate(
            go,
            offset.transform.position + new Vector3(0, 5, 0),
            offset.transform.rotation,
            offset.transform
        );

        currentStoneSlot.stone.transform.localPosition = currentStoneSlot.arrayPos;
        currentStoneSlot.stone.transform.localScale /= 2;

        stoneMatrix[stairCount % width, stairCount / width].stone = currentStoneSlot.stone;
        stoneMatrix[stairCount % width, stairCount / width].originStone = currentStoneSlot.originStone;

        currentStair++;
        stairCount++;
    }

    public void StartBuildingProcess()
    {
        StartCoroutine(PlaceStones());
    }

    public IEnumerator PlaceStones()
    {
        int stairsPlaced = 0;

        canAcceptStones = false;
        canAcceptCrystal = false;

        if (currentCrystal != null)
        {
            Destroy(currentCrystal);
            currentCrystal = null;
            hasCrystal = false;
        }

        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                if (stairsPlaced >= stairCount)
                    break;

                stairsPlaced++;

                yield return new WaitForSeconds(0.1f);

                StartCoroutine(BuildStair(stoneMatrix[j, i]));
            }
        }

        stairCount = 0;
        canAcceptStones = true;
        canAcceptCrystal = true;
    }

    public void ReceiveCrystal()
    {
        if (hasCrystal) return;

        hasCrystal = true;

        currentCrystal = new GameObject("CrystalSprite");

        SpriteRenderer sr = currentCrystal.AddComponent<SpriteRenderer>();
        sr.sprite = crystalImage;

        currentCrystal.transform.SetParent(crystalSlot);
        currentCrystal.transform.localPosition = Vector3.zero;
        currentCrystal.transform.localScale = Vector3.one * 0.15f;
    }

    IEnumerator BuildStair(StoneSlot currentStoneSlot)
    {
        AddTrail(currentStoneSlot.stone);

        Vector3 currentPos = currentStoneSlot.stone.transform.position;
        Vector3 targetPos = currentStoneSlot.originStone.transform.position;

        Vector3 startScale = currentStoneSlot.stone.transform.localScale;
        Vector3 targetScale = startScale / 5;

        Quaternion startRot = currentStoneSlot.stone.transform.rotation;
        Quaternion targetRot = currentStoneSlot.originStone.transform.rotation;

        float elapsedTime = 0;
        float waitTime = 0.5f;

        while (elapsedTime < waitTime)
        {
            float animationCurvePercent = animationCurve.Evaluate(elapsedTime / waitTime);
            float scaleAnimationPercent = scaleAnimation.Evaluate(elapsedTime / waitTime);

            currentStoneSlot.stone.transform.position =
                Vector3.Lerp(currentPos, targetPos, animationCurvePercent);

            currentStoneSlot.stone.transform.localScale =
                Vector3.Lerp(startScale, targetScale, scaleAnimationPercent);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        Destroy(currentStoneSlot.stone);

        GameObject newStone = Instantiate(
            currentStoneSlot.originStone,
            currentStoneSlot.originStone.transform.position,
            currentStoneSlot.originStone.transform.rotation,
            transform
        );

        newStone.GetComponent<MeshRenderer>().material = buildMat;

        currentStoneSlot.originStone.GetComponent<MeshRenderer>().enabled = false;

        Color startColor = whiteMat.color;
        Color targetcolor = buildMat.color;

        Color startEmission = whiteMat.GetColor("_EmissionColor");
        Color targetEmission = buildMat.GetColor("_EmissionColor");

        newStone.GetComponent<MeshRenderer>().material.EnableKeyword("_EMISSION");

        elapsedTime = 0;
        waitTime = 0.2f;

        while (elapsedTime < waitTime)
        {
            float materialCurvePercent = materialAnimation.Evaluate(elapsedTime / waitTime);

            newStone.GetComponent<MeshRenderer>().material.color =
                Color.Lerp(startColor, targetcolor, materialCurvePercent);

            newStone.GetComponent<MeshRenderer>().material.SetColor(
                "_EmissionColor",
                Color.Lerp(startEmission, targetEmission, materialCurvePercent)
            );

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        newStone.GetComponent<MeshRenderer>().material.SetColor("_EmissionColor", targetEmission);
        newStone.GetComponent<MeshRenderer>().material.color = targetcolor;

        totalBuiltStairs++;
        levelManager.stairs = totalBuiltStairs;
    }
}