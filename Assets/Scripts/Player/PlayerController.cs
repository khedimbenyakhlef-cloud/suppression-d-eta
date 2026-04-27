using UnityEngine;
using UnityEngine.InputSystem;

namespace SuppressionDeta
{
    /// <summary>
    /// Contrôleur joueur universel — gère Alex et Lana.
    /// Supporte clavier/souris (PC), manette (PS5/Xbox — haptique DualSense),
    /// tactile (iOS/Android) et WebGL.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Animator))]
    public class PlayerController : MonoBehaviour
    {
        // ── Identité ───────────────────────────────────────────────────────────
        [Header("Personnage")]
        public CharacterType character = CharacterType.Alex;
        public float maxHealth     = 100f;
        public float currentHealth;
        public bool  isAlive       = true;

        // ── Mouvement ─────────────────────────────────────────────────────────
        [Header("Mouvement")]
        public float walkSpeed     = 4f;
        public float runSpeed      = 8f;
        public float crouchSpeed   = 2f;
        public float jumpForce     = 6f;
        public float gravity       = -18f;
        public float rotationSpeed = 720f;

        // ── Combat ────────────────────────────────────────────────────────────
        [Header("Combat")]
        public float meleeDamage  = 35f;
        public float meleeRange   = 2.2f;
        public float meleeCooldown= 0.6f;
        public LayerMask enemyLayer;

        // ── Capacités spéciales ───────────────────────────────────────────────
        [Header("Capacités")]
        public float tacticalSlowDuration = 3f;   // Alex — ralenti tactique
        public float hackRange            = 50f;  // Lana — Neural Hack
        public float hackEnergy           = 100f;
        public float currentHackEnergy;

        // ── Références ────────────────────────────────────────────────────────
        [Header("Références")]
        public Camera        playerCamera;
        public Transform     weaponHold;
        public GasMask       gasMask;
        public AudioSource   audioSource;
        public AudioClip[]   footstepClips;
        public AudioClip     hurtClip;

        // ── État interne ──────────────────────────────────────────────────────
        CharacterController controller;
        Animator            anim;
        Vector3             velocity;
        Vector3             moveDir;
        float               lastMeleeTime;
        bool                isGrounded;
        bool                isCrouching;
        bool                isSprinting;
        bool                isTacticalSlow;
        bool                isHacking;

        // Inputs
        PlayerInput         playerInput;
        InputAction         moveAction, lookAction, jumpAction, sprintAction;
        InputAction         crouchAction, attackAction, specialAction, interactAction;

        // ── Initialisation ────────────────────────────────────────────────────
        void Awake()
        {
            controller       = GetComponent<CharacterController>();
            anim             = GetComponent<Animator>();
            playerInput      = GetComponent<PlayerInput>();
            currentHealth    = maxHealth;
            currentHackEnergy = hackEnergy;

            BindInputs();
        }

        void BindInputs()
        {
            moveAction     = playerInput.actions["Move"];
            lookAction     = playerInput.actions["Look"];
            jumpAction     = playerInput.actions["Jump"];
            sprintAction   = playerInput.actions["Sprint"];
            crouchAction   = playerInput.actions["Crouch"];
            attackAction   = playerInput.actions["Attack"];
            specialAction  = playerInput.actions["Special"];
            interactAction = playerInput.actions["Interact"];
        }

        // ── Update principal ──────────────────────────────────────────────────
        void Update()
        {
            if (!isAlive || GameManager.Instance?.currentState != GameState.Playing) return;

            HandleMovement();
            HandleGravity();
            HandleLook();
            HandleCombat();
            HandleSpecial();
            UpdateAnimator();

            // Régénération énergie hack (Lana)
            if (character == CharacterType.Lana && !isHacking)
                currentHackEnergy = Mathf.Min(hackEnergy, currentHackEnergy + 10f * Time.deltaTime);
        }

        // ── Mouvement ─────────────────────────────────────────────────────────
        void HandleMovement()
        {
            isGrounded = controller.isGrounded;
            if (isGrounded && velocity.y < 0) velocity.y = -2f;

            Vector2 input = moveAction.ReadValue<Vector2>();
            isSprinting   = sprintAction.IsPressed() && input.magnitude > 0.1f;
            isCrouching   = crouchAction.IsPressed();

            float speed = isCrouching ? crouchSpeed : (isSprinting ? runSpeed : walkSpeed);

            // Direction caméra
            Vector3 camForward = Vector3.Scale(playerCamera.transform.forward, new Vector3(1, 0, 1)).normalized;
            Vector3 camRight   = playerCamera.transform.right;
            moveDir            = (camForward * input.y + camRight * input.x).normalized;

            if (moveDir.magnitude > 0.1f)
            {
                controller.Move(moveDir * speed * Time.deltaTime);

                Quaternion targetRot = Quaternion.LookRotation(moveDir);
                transform.rotation   = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }

            // Saut
            if (jumpAction.WasPressedThisFrame() && isGrounded && !isCrouching)
                velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
        }

        void HandleGravity()
        {
            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
        }

        void HandleLook()
        {
            // Rotation caméra gérée par CinemachineFreeLook — on ne fait rien ici
        }

        // ── Combat ────────────────────────────────────────────────────────────
        void HandleCombat()
        {
            if (!attackAction.WasPressedThisFrame()) return;
            if (Time.time - lastMeleeTime < meleeCooldown) return;

            lastMeleeTime = Time.time;
            anim.SetTrigger("Melee");

            Collider[] hits = Physics.OverlapSphere(transform.position + transform.forward * 1.2f, meleeRange, enemyLayer);
            foreach (var hit in hits)
            {
                if (hit.TryGetComponent<EnemyAI>(out var enemy))
                    enemy.TakeDamage(meleeDamage);
            }
        }

        // ── Capacités spéciales ───────────────────────────────────────────────
        void HandleSpecial()
        {
            if (!specialAction.WasPressedThisFrame()) return;

            if (character == CharacterType.Alex)
                StartCoroutine(TacticalSlowRoutine());
            else if (character == CharacterType.Lana)
                TryNeuralHack();
        }

        System.Collections.IEnumerator TacticalSlowRoutine()
        {
            if (isTacticalSlow) yield break;
            isTacticalSlow      = true;
            Time.timeScale      = 0.3f;
            Time.fixedDeltaTime = 0.02f * Time.timeScale;
            yield return new WaitForSecondsRealtime(tacticalSlowDuration);
            Time.timeScale      = 1f;
            Time.fixedDeltaTime = 0.02f;
            isTacticalSlow      = false;
        }

        void TryNeuralHack()
        {
            if (currentHackEnergy < 20f) return;

            Collider[] targets = Physics.OverlapSphere(transform.position, hackRange, enemyLayer);
            if (targets.Length == 0) return;

            // Cherche la cible la plus proche
            float    best = float.MaxValue;
            EnemyAI  bestEnemy = null;
            foreach (var t in targets)
            {
                float d = Vector3.Distance(transform.position, t.transform.position);
                if (d < best) { best = d; bestEnemy = t.GetComponent<EnemyAI>(); }
            }

            if (bestEnemy != null)
            {
                bestEnemy.Hack();
                currentHackEnergy -= 30f;
                isHacking = true;
                anim.SetTrigger("Hack");
                Invoke(nameof(EndHack), 1.5f);
            }
        }

        void EndHack() => isHacking = false;

        // ── Dégâts / Santé ────────────────────────────────────────────────────
        public void TakeDamage(float amount)
        {
            if (!isAlive) return;
            currentHealth -= amount;
            audioSource.PlayOneShot(hurtClip);
            anim.SetTrigger("Hit");

            if (currentHealth <= 0f) Die();
        }

        void Die()
        {
            isAlive = false;
            anim.SetTrigger("Die");
            GameManager.Instance?.ChangeState(GameState.GameOver);
        }

        public void Heal(float amount) =>
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);

        // ── Effets de Gaz ─────────────────────────────────────────────────────
        public void ApplyGasEffect(GasType gasType)
        {
            if (gasMask != null && gasMask.isEquipped) return;

            switch (gasType)
            {
                case GasType.Inferno:
                    TakeDamage(50f);
                    break;
                case GasType.Rire:
                    StartCoroutine(LaughingEffect());
                    break;
                case GasType.Inerte:
                    StartCoroutine(SleepEffect());
                    break;
            }
        }

        System.Collections.IEnumerator LaughingEffect()
        {
            anim.SetBool("IsLaughing", true);
            yield return new WaitForSeconds(5f);
            anim.SetBool("IsLaughing", false);
        }

        System.Collections.IEnumerator SleepEffect()
        {
            isAlive = false;
            anim.SetTrigger("Sleep");
            yield return new WaitForSeconds(30f);
            isAlive = true;
            anim.SetTrigger("WakeUp");
        }

        // ── Animateur ─────────────────────────────────────────────────────────
        void UpdateAnimator()
        {
            Vector2 input = moveAction.ReadValue<Vector2>();
            anim.SetFloat("Speed",     input.magnitude);
            anim.SetBool("IsSprinting", isSprinting);
            anim.SetBool("IsCrouching", isCrouching);
            anim.SetBool("IsGrounded",  isGrounded);
            anim.SetFloat("HackEnergy", currentHackEnergy / hackEnergy);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position + transform.forward * 1.2f, meleeRange);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, hackRange);
        }
    }

    public enum CharacterType { Alex, Lana }
}
