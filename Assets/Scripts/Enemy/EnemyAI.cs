using UnityEngine;
using UnityEngine.AI;
using System.Collections;

namespace SuppressionDeta
{
    /// <summary>
    /// IA ennemie basée sur une machine à états finis (FSM).
    /// États : Patrol → Alert → Combat → Flee | Hacked | Laughing | Asleep
    /// Utilise NavMeshAgent pour la navigation dans Nova Déta.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Animator))]
    public class EnemyAI : MonoBehaviour
    {
        // ── Identité ───────────────────────────────────────────────────────────
        [Header("Identité")]
        public EnemyType enemyType    = EnemyType.Guard;
        public string    enemyName    = "Garde";
        public float     maxHealth    = 80f;
        public float     currentHealth;

        // ── Détection ─────────────────────────────────────────────────────────
        [Header("Détection")]
        public float sightRange       = 20f;
        public float hearRange        = 12f;
        public float attackRange      = 3f;
        public float fieldOfView      = 110f;
        public LayerMask playerLayer;
        public LayerMask obstacleLayer;

        // ── Combat ────────────────────────────────────────────────────────────
        [Header("Combat")]
        public float damage           = 15f;
        public float attackCooldown   = 1.5f;
        public float alertRadius      = 25f;

        // ── Patrol ────────────────────────────────────────────────────────────
        [Header("Patrouille")]
        public Transform[] patrolPoints;
        public float       waitAtPoint = 2f;

        // ── Références ────────────────────────────────────────────────────────
        NavMeshAgent agent;
        Animator     anim;
        Transform    player;

        // ── État FSM ──────────────────────────────────────────────────────────
        EnemyState currentState = EnemyState.Patrol;
        int        patrolIndex  = 0;
        float      lastAttackTime;
        float      waitTimer;
        bool       isWaiting;
        bool       isHacked;
        bool       isAlive = true;

        // ── Init ──────────────────────────────────────────────────────────────
        void Awake()
        {
            agent         = GetComponent<NavMeshAgent>();
            anim          = GetComponent<Animator>();
            currentHealth = maxHealth;
        }

        void Start()
        {
            // Cherche le joueur par tag
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;

            GoToNextPatrolPoint();
        }

        // ── Machine à états principale ────────────────────────────────────────
        void Update()
        {
            if (!isAlive || isHacked) return;

            switch (currentState)
            {
                case EnemyState.Patrol:  UpdatePatrol();  break;
                case EnemyState.Alert:   UpdateAlert();   break;
                case EnemyState.Combat:  UpdateCombat();  break;
                case EnemyState.Flee:    UpdateFlee();    break;
            }

            UpdateAnimator();
        }

        // ── État : Patrouille ─────────────────────────────────────────────────
        void UpdatePatrol()
        {
            if (player != null && CanSeePlayer()) { TransitionTo(EnemyState.Alert); return; }

            if (!agent.pathPending && agent.remainingDistance < 0.5f)
            {
                if (!isWaiting)
                {
                    isWaiting = true;
                    waitTimer = waitAtPoint;
                    agent.isStopped = true;
                }
                else
                {
                    waitTimer -= Time.deltaTime;
                    if (waitTimer <= 0f) { isWaiting = false; agent.isStopped = false; GoToNextPatrolPoint(); }
                }
            }
        }

        void GoToNextPatrolPoint()
        {
            if (patrolPoints.Length == 0) return;
            agent.SetDestination(patrolPoints[patrolIndex].position);
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        }

        // ── État : Alerte ─────────────────────────────────────────────────────
        void UpdateAlert()
        {
            if (player == null) { TransitionTo(EnemyState.Patrol); return; }

            agent.SetDestination(player.position);
            float dist = Vector3.Distance(transform.position, player.position);

            if (dist <= attackRange)       { TransitionTo(EnemyState.Combat); return; }
            if (!CanSeePlayer() && dist > sightRange * 1.5f)
                StartCoroutine(LostPlayerRoutine());
        }

        // ── État : Combat ─────────────────────────────────────────────────────
        void UpdateCombat()
        {
            if (player == null) { TransitionTo(EnemyState.Patrol); return; }

            float dist = Vector3.Distance(transform.position, player.position);

            // Reste face au joueur
            Vector3 dir = (player.position - transform.position).normalized;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z)), 10f * Time.deltaTime);

            if (dist > attackRange * 1.5f) { TransitionTo(EnemyState.Alert); return; }

            // Attaque
            if (Time.time - lastAttackTime >= attackCooldown)
            {
                lastAttackTime = Time.time;
                anim.SetTrigger("Attack");

                if (player.TryGetComponent<PlayerController>(out var p))
                    p.TakeDamage(damage);
            }
            agent.isStopped = true;
        }

        // ── État : Fuite ──────────────────────────────────────────────────────
        void UpdateFlee()
        {
            if (player == null) { TransitionTo(EnemyState.Patrol); return; }

            Vector3 fleeDir = (transform.position - player.position).normalized * 20f;
            agent.SetDestination(transform.position + fleeDir);
        }

        // ── Détection ─────────────────────────────────────────────────────────
        bool CanSeePlayer()
        {
            if (player == null) return false;
            Vector3 dir  = (player.position - transform.position).normalized;
            float   dist = Vector3.Distance(transform.position, player.position);
            float   angle= Vector3.Angle(transform.forward, dir);

            if (angle > fieldOfView / 2f || dist > sightRange) return false;
            return !Physics.Raycast(transform.position + Vector3.up, dir, dist, obstacleLayer);
        }

        // ── Transitions ───────────────────────────────────────────────────────
        void TransitionTo(EnemyState newState)
        {
            currentState = newState;
            switch (newState)
            {
                case EnemyState.Alert:
                    AlertNearbyGuards();
                    agent.speed = agent.speed * 1.5f;
                    break;
                case EnemyState.Combat:
                    agent.isStopped = false;
                    break;
                case EnemyState.Patrol:
                    agent.speed /= 1.5f;
                    agent.isStopped = false;
                    GoToNextPatrolPoint();
                    break;
            }
        }

        void AlertNearbyGuards()
        {
            Collider[] nearby = Physics.OverlapSphere(transform.position, alertRadius, LayerMask.GetMask("Enemy"));
            foreach (var col in nearby)
                if (col.TryGetComponent<EnemyAI>(out var other) && other != this && other.currentState == EnemyState.Patrol)
                    other.TransitionTo(EnemyState.Alert);
        }

        IEnumerator LostPlayerRoutine()
        {
            yield return new WaitForSeconds(5f);
            if (currentState == EnemyState.Alert)
                TransitionTo(EnemyState.Patrol);
        }

        // ── Dégâts ────────────────────────────────────────────────────────────
        public void TakeDamage(float amount)
        {
            if (!isAlive) return;
            currentHealth -= amount;
            anim.SetTrigger("Hit");
            TransitionTo(EnemyState.Combat);

            if (currentHealth <= 0f) Die();
            else if (currentHealth < maxHealth * 0.2f) TransitionTo(EnemyState.Flee);
        }

        void Die()
        {
            isAlive        = false;
            agent.isStopped= true;
            anim.SetTrigger("Die");
            GetComponent<Collider>().enabled = false;
            ReputationSystem.Instance?.AddReputation(FactionType.LawEnforcement, 5);
            Destroy(gameObject, 5f);
        }

        // ── Effets de Gaz ─────────────────────────────────────────────────────
        public void ApplyGasEffect(GasType gasType)
        {
            switch (gasType)
            {
                case GasType.Inferno: TakeDamage(40f); break;
                case GasType.Rire:    StartCoroutine(LaughRoutine()); break;
                case GasType.Inerte:  StartCoroutine(SleepRoutine()); break;
            }
        }

        IEnumerator LaughRoutine()
        {
            currentState    = EnemyState.Patrol;
            agent.isStopped = true;
            anim.SetBool("IsLaughing", true);
            yield return new WaitForSeconds(8f);
            anim.SetBool("IsLaughing", false);
            agent.isStopped = false;
            TransitionTo(EnemyState.Patrol);
        }

        IEnumerator SleepRoutine()
        {
            agent.isStopped = true;
            anim.SetTrigger("Sleep");
            isHacked        = true;          // Réutilise le flag pour bloquer FSM
            yield return new WaitForSeconds(30f);
            isHacked        = false;
            agent.isStopped = false;
            anim.SetTrigger("WakeUp");
        }

        // ── Neural Hack (Lana) ────────────────────────────────────────────────
        public void Hack()
        {
            StartCoroutine(HackRoutine());
        }

        IEnumerator HackRoutine()
        {
            isHacked = true;
            anim.SetBool("IsHacked", true);
            agent.isStopped = false;

            // L'ennemi hacké attaque ses alliés pendant 15 secondes
            LayerMask savedLayer = playerLayer;
            playerLayer = LayerMask.GetMask("Enemy");
            yield return new WaitForSeconds(15f);
            playerLayer = savedLayer;

            isHacked = false;
            anim.SetBool("IsHacked", false);
            TransitionTo(EnemyState.Patrol);
        }

        // ── Animateur ─────────────────────────────────────────────────────────
        void UpdateAnimator()
        {
            anim.SetFloat("Speed",   agent.velocity.magnitude);
            anim.SetBool("InCombat", currentState == EnemyState.Combat);
            anim.SetBool("Alert",    currentState == EnemyState.Alert);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, sightRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
            Gizmos.color = new Color(1, 0.5f, 0, 0.3f);
            Gizmos.DrawWireSphere(transform.position, alertRadius);
        }
    }

    public enum EnemyState { Patrol, Alert, Combat, Flee }
    public enum EnemyType  { Guard, Elite, Mercenary, Boss }
}
