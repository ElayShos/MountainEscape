using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using TMPro;

public class Minion : NPC
{
    enum MinionState
    {
        ChoosingMine,
        MovingToMine,
        Mining,
        ReturningToWizard,
        Delivering,
        Idle,
        Dead,
        Escaping
    }

    private MinionState state;

    private BoxCollider boxCollider;

    private Wizard wizard;
    private Stairs stairs;
    private GameObject mines;

    private Transform mineTarget;
    private Vector3 targetMinePosition;

    private bool hasStone;
    private bool hasClaimedMine = false;

    bool hasReachedMine = false;
    bool hasReachedWizard = false;

    private MinionState stateBeforeIdle;

    float idleRepathTimer = 0f;
    float idleRepathInterval = 0.5f;

    protected override void Start()
    {
        base.Start();

        maxHealth = 250;
        currentHealth = maxHealth;

        agent = GetComponent<NavMeshAgent>();
        boxCollider = GetComponent<BoxCollider>();

        moveSpeed = 5;
        agent.speed = moveSpeed;
        agent.stoppingDistance = 0.5f;

        wizard = FindFirstObjectByType<Wizard>();
        stairs = FindFirstObjectByType<Stairs>();
        mines = GameObject.Find("Mines");

        state = MinionState.ChoosingMine;
        agent.autoRepath = true;

        warningImage.enabled = false;
        warningText.enabled = false;
    }

    void Update()
    {
        base.BaseUpdate();

        if (!isGameRunning || state == MinionState.Dead)
            return;

        if (currentHealth <= 0)
        {
            Death();
            return;
        }

        switch (state)
        {
            case MinionState.ChoosingMine:
                ChooseMine();
                break;

            case MinionState.MovingToMine:
                MoveToMine();
                break;

            case MinionState.ReturningToWizard:
                MoveToWizard();
                break;

            case MinionState.Idle:
                HandleIdle();
                break;
        }
    }

    void ChooseMine()
    {
        if (mines == null) return;

        int randomIndex = Random.Range(0, mines.transform.childCount);
        Transform candidate = mines.transform.GetChild(randomIndex);

        MineSpot spot = candidate.GetComponent<MineSpot>();

        if (spot.minionsCount == 0)
        {
            spot.minionsCount++;

            mineTarget = candidate;
            targetMinePosition = new Vector3(candidate.position.x, transform.position.y, candidate.position.z);

            hasClaimedMine = true;

            agent.SetDestination(targetMinePosition);
            agent.isStopped = false;

            animator.SetBool("isWalking", true);

            state = MinionState.MovingToMine;
        }
    }

    void MoveToMine()
    {
        if (agent.pathPending)
            return;

        if (agent.pathStatus != NavMeshPathStatus.PathComplete)
        {
            EnterIdleState();
            return;
        }

        if (agent.remainingDistance <= agent.stoppingDistance &&
            agent.velocity.sqrMagnitude < 0.01f)
        {
            StartCoroutine(MineRoutine());
        }
    }

    IEnumerator MineRoutine()
    {
        state = MinionState.Mining;

        agent.isStopped = true;

        animator.SetBool("isWalking", false);
        animator.SetBool("isMining", true);

        float miningDuration = 10f;
        float elapsed = 0f;

        while (elapsed < miningDuration)
        {
            int mineNum = Random.Range(1, 3);
            animator.SetInteger("mine_num", mineNum);

            float switchDelay = Random.Range(0.5f, 1f);
            yield return new WaitForSeconds(switchDelay);

            elapsed += switchDelay;
        }

        animator.SetBool("isMining", false);

        if (hasClaimedMine && mineTarget != null)
        {
            MineSpot spot = mineTarget.GetComponent<MineSpot>();
            spot.minionsCount--;

            hasClaimedMine = false;
        }

        hasStone = true;

        hasReachedMine = false;

        state = MinionState.ReturningToWizard;

        agent.isStopped = false;
        agent.SetDestination(wizard.transform.position);

        animator.SetBool("isWalking", true);
    }

    void MoveToWizard()
    {
        if (agent.pathPending)
            return;

        if (agent.pathStatus != NavMeshPathStatus.PathComplete)
        {
            EnterIdleState();
            return;
        }

        if (hasReachedWizard)
        {
            StartCoroutine(DeliverStone());
        }
    }

    IEnumerator DeliverStone()
    {
        state = MinionState.Delivering;

        agent.ResetPath();
        agent.isStopped = true;

        animator.SetBool("isWalking", false);
        animator.SetBool("isThrowing", true);

        yield return new WaitForSeconds(1f);

        hasReachedWizard = false;

        if (hasStone && stairs.takeStoneFromMinion)
        {
            stairs.CollectStones();

            hasStone = false;

            animator.SetBool("isThrowing", false);

            agent.ResetPath();
            agent.isStopped = true;

            state = MinionState.ChoosingMine;
            ChooseMine();
        }
        else
        {
            Death();
        }
    }

    void Death()
    {
        state = MinionState.Dead;

        if (hasClaimedMine && mineTarget != null)
        {
            MineSpot spot = mineTarget.GetComponent<MineSpot>();

            if (spot.minionsCount > 0)
                spot.minionsCount--;

            hasClaimedMine = false;
        }

        Destroy(gameObject);
    }

    public override void GameWon()
    {
        base.GameWon();

        state = MinionState.Escaping;

        agent.isStopped = true;

        animator.SetBool("isWalking", false);
        animator.SetBool("isMining", false);
        animator.SetBool("isThrowing", false);

        StartCoroutine(RunToStairs());
    }

    IEnumerator RunToStairs()
    {
        yield return new WaitForSeconds(3f);

        agent.isStopped = false;

        agent.speed = 25;
        agent.acceleration = 1000;
        agent.angularSpeed = 3000000;

        Vector3 stairsPos = levelManager.stairsSpot.transform.position;

        agent.SetDestination(stairsPos);

        animator.SetBool("isWalking", true);
    }

    void EnterIdleState()
    {
        if (state == MinionState.Idle)
            return;

        warningImage.enabled = true;
        warningText.enabled = true;
        warningText.text = "path blocked!";

        stateBeforeIdle = state;
        state = MinionState.Idle;

        agent.isStopped = true;

        animator.SetBool("isWalking", false);
    }

    void HandleIdle()
    {
        idleRepathTimer += Time.deltaTime;

        if (idleRepathTimer < idleRepathInterval)
            return;

        idleRepathTimer = 0f;

        if (stateBeforeIdle == MinionState.MovingToMine && mineTarget != null)
        {
            agent.SetDestination(targetMinePosition);
        }
        else if (stateBeforeIdle == MinionState.ReturningToWizard)
        {
            agent.SetDestination(wizard.transform.position);
        }

        if (agent.pathStatus == NavMeshPathStatus.PathComplete)
        {
            agent.isStopped = false;

            animator.SetBool("isWalking", true);

            state = stateBeforeIdle;

            warningImage.enabled = false;
            warningText.enabled = false;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Minion") || collision.gameObject.CompareTag("SuperMinion"))
        {
            Physics.IgnoreCollision(collision.collider, boxCollider);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        OnTriggerEnterLogic(other);

        if (other.tag == "Mines") hasReachedMine = true;
        if (other.tag == "Wizard") hasReachedWizard = true;
    }

    private void OnTriggerExit(Collider other)
    {
        OnTriggerEnterLogic(other);

        if (other.tag == "Mines") hasReachedMine = false;
        if (other.tag == "Wizard") hasReachedWizard = false;
    }
}