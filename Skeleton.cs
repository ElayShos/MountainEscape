using UnityEngine;
using System.Collections;
using UnityEngine.AI;

public class Skeleton : Enemy
{
    public enum SkeletonState
    {
        Following,
        Attacking,
        Stunned,
        Dead
    }

    SkeletonState state = SkeletonState.Following;

    public Transform targetPoint;
    public GameObject firePoint;
    [SerializeField] GameObject shotObject;

    LayerMask obstructionMask;

    float distanceFromPlayer;
    float rangeToAttack = 10f;
    float rangeToKeepAttacking = 15f;

    float shotCooldown = 1f;
    float shotPrevious = -1f;

    int attackNum = 1;

    Vector3 direction;
    float distance;
    bool hasObstruction;

    Transform shotPointFX;

    protected override void Start()
    {
        base.Start();

        maxHealth = 250;
        currentHealth = maxHealth;

        moveSpeed = 5;
        agent.speed = moveSpeed;

        targetPoint = player.transform;

        obstructionMask = LayerMask.GetMask("Environment");

        shotPointFX = transform.Find("Skeleton_animations/Bone.001/Bone/Bone.003/Bone.007/Weapon/FirePoint/VFX_Trail_Dark");
        ToggleTrail(false);
    }

    void Update()
    {
        rb.isKinematic = true;

        base.BaseUpdate();

        if (currentHealth <= 0 && state != SkeletonState.Dead)
            state = SkeletonState.Dead;

        if (isStunned && state != SkeletonState.Dead)
        {
            state = SkeletonState.Stunned;
            return;
        }
        else if (state == SkeletonState.Stunned)
        {
            state = SkeletonState.Following;
        }

        if (!isStunned && agent && !agent.enabled)
            agent.enabled = true;

        if (player)
            distanceFromPlayer = Vector3.Distance(transform.position, player.transform.position);

        direction = player.transform.position - transform.position;
        distance = direction.magnitude;

        hasObstruction = Physics.Raycast(
            transform.position + transform.up * 1,
            direction.normalized,
            out RaycastHit hit,
            distance,
            obstructionMask
        );

        if (state == SkeletonState.Attacking)
        {
            if (distanceFromPlayer > rangeToKeepAttacking || hasObstruction)
            {
                AttackCancel();
                state = SkeletonState.Following;
            }
        }

        switch (state)
        {
            case SkeletonState.Following:
                FollowPlayer();
                break;

            case SkeletonState.Attacking:
                BasicAttack();
                break;

            case SkeletonState.Dead:
                if (!isDead)
                    Death();

                if (timeOfDeath + 2f < Time.time)
                    Dissolve();
                break;
        }
    }

    void FollowPlayer()
    {
        if (distanceFromPlayer <= rangeToAttack && state != SkeletonState.Attacking && !hasObstruction)
        {
            animator.SetBool("isWalking", false);
            state = SkeletonState.Attacking;
        }
        else
        {
            direction = agent.desiredVelocity;
            direction.y = 0;

            if (direction.sqrMagnitude > 0.01f)
                LookAt(direction);

            animator.SetBool("isWalking", true);

            AttackCancel();

            agent.isStopped = false;

            NavMesh.CalculatePath(
                transform.position,
                player.transform.position,
                NavMesh.AllAreas,
                path
            );

            agent.SetPath(path);
        }
    }

    void AttackCancel()
    {
        StopCoroutine(BasicAttackCoroutine());
        attackNum = 1;

        animator.SetBool("isAttacking", false);

        ToggleTrail(false);
    }

    void BasicAttack()
    {
        if (firePoint == null || targetPoint == null)
            return;

        Vector3 direction = player.transform.position - transform.position;
        LookAt(direction);

        if (Time.time - shotPrevious > shotCooldown)
        {
            StartCoroutine(BasicAttackCoroutine());
            ToggleTrail(true);
        }
    }

    IEnumerator BasicAttackCoroutine()
    {
        agent.isStopped = true;

        shotPrevious = Time.time;

        animator.SetBool("isAttacking", true);
        animator.SetInteger("attackNum", attackNum);

        yield return new WaitForSeconds(0.3f);

        var shot = Instantiate(
            shotObject,
            firePoint.transform.position,
            Quaternion.identity
        );

        shot.GetComponent<SkeletonShot>().skeletonParent = gameObject;

        if (attackNum == 1)
            attackNum = 2;
        else
            attackNum = 1;

        yield return new WaitForSeconds(0.2f);

        ToggleTrail(false);
    }

    void ToggleTrail(bool isEnabled)
    {
        TrailRenderer[] trails = shotPointFX.GetComponentsInChildren<TrailRenderer>();

        if (isEnabled)
        {
            shotPointFX.gameObject.SetActive(true);

            foreach (TrailRenderer t in trails)
            {
                t.Clear();
                t.emitting = true;
            }
        }
        else
        {
            foreach (TrailRenderer t in trails)
            {
                t.emitting = false;
            }
        }
    }

    public override void Stun(float d)
    {
        base.Stun(d);

        AttackCancel();
        state = SkeletonState.Stunned;
    }

    private void OnTriggerEnter(Collider other)
    {
        base.OnTriggerEnterLogic(other);
    }

    private void OnTriggerExit(Collider other)
    {
        base.OnTriggerExitLogic(other);
    }

    void Death()
    {
        StartCoroutine(DeathCoroutine());
        DropGems(5);
    }
}