using UnityEngine;
using System.Collections;

namespace SuppressionDeta
{
    // ══════════════════════════════════════════════════════════════════════════
    //  LIAM — L'Adversaire Masqué / Gardien
    //  Boss Acte II + Allié potentiel Acte III
    // ══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Personnage central : Liam, ancien ami d'Alex.
    /// Comportement change selon la réputation et les choix du joueur.
    /// Phase 1 : Boss combat. Phase 2 : Révélation identité. Phase 3 : Allié ou ennemi final.
    /// </summary>
    public class LiamCharacter : MonoBehaviour
    {
        [Header("Identité")]
        public bool  maskRevealed     = false;
        public bool  isAlly           = false;
        public float maxHealth        = 300f;
        public float currentHealth;

        [Header("Combat Boss")]
        public float phaseTwoThreshold = 0.5f;   // 50% PV = Phase 2
        public float phaseOneThreshold = 0.25f;  // 25% PV = Phase 3
        public float attackDamage      = 30f;
        public float movementSpeed     = 7f;

        [Header("Capacités Spéciales")]
        public float smokeGrenadeRange = 8f;
        public float counterAttackWindow = 0.4f;
        public int   currentPhase     = 1;

        [Header("Dialogue")]
        public DialogueData phaseOneDialogue;
        public DialogueData revelationDialogue;
        public DialogueData pardonDialogue;

        Animator   anim;
        Transform  player;
        bool       isAlive       = true;
        bool       inCounterWindow;
        float      lastAttackTime;
        float      counterTimer;

        void Awake()
        {
            anim          = GetComponent<Animator>();
            currentHealth = maxHealth;
        }

        void Start()
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        void Update()
        {
            if (!isAlive || isAlly) return;

            float dist = player ? Vector3.Distance(transform.position, player.position) : 999f;

            switch (currentPhase)
            {
                case 1: BossPhaseOne(dist);   break;
                case 2: BossPhaseTwo(dist);   break;
                case 3: BossPhaseThree(dist); break;
            }

            CheckPhaseTransition();
        }

        // ── Phase 1 : Combat furtif et agile ─────────────────────────────────
        void BossPhaseOne(float dist)
        {
            if (dist > 20f) return;

            // Se téléporte derrière le joueur (effet furtif)
            if (dist < 5f && Time.time - lastAttackTime > 2f)
            {
                lastAttackTime = Time.time;
                AttackPlayer();
            }
            else if (dist > 6f)
            {
                Vector3 dir = (player.position - transform.position).normalized;
                transform.position += dir * movementSpeed * Time.deltaTime;
                transform.LookAt(player);
            }
        }

        // ── Phase 2 : Révélation partielle, attaques au gaz ──────────────────
        void BossPhaseTwo(float dist)
        {
            if (currentPhase != 2) return;

            // Masque fissuré — animations différentes
            anim?.SetInteger("Phase", 2);

            // Lance grenades fumigènes + attaque au cœur de la fumée
            if (Time.time - lastAttackTime > 3f)
            {
                lastAttackTime = Time.time;
                ThrowSmokeGrenade();
            }
        }

        // ── Phase 3 : Révélation totale — choix moral ─────────────────────────
        void BossPhaseThree(float dist)
        {
            // Arrête le combat — déclenche cinématique de révélation
            anim?.SetBool("Exhausted", true);
            GetComponent<UnityEngine.AI.NavMeshAgent>()?.isStopped.Equals(true);

            // Déclenchement unique de la scène de révélation
            if (!maskRevealed)
            {
                maskRevealed = true;
                StartCoroutine(RevealSequence());
            }
        }

        // ── Transition de phases ──────────────────────────────────────────────
        void CheckPhaseTransition()
        {
            float ratio = currentHealth / maxHealth;

            if (currentPhase == 1 && ratio <= phaseTwoThreshold)
            {
                currentPhase = 2;
                OnPhaseTwo();
            }
            else if (currentPhase == 2 && ratio <= phaseOneThreshold)
            {
                currentPhase = 3;
            }
        }

        void OnPhaseTwo()
        {
            anim?.SetTrigger("PhaseTwo");
            GasSystem.Instance?.DeployGas(GasType.Fume, transform.position, 15f);
            HUDManager.Instance?.ShowNotification("Le Gardien révèle de nouveaux pouvoirs !");
        }

        // ── Attaques ──────────────────────────────────────────────────────────
        void AttackPlayer()
        {
            anim?.SetTrigger("Attack");
            if (player?.TryGetComponent<PlayerController>(out var p) == true)
            {
                // Fenêtre de contre-attaque pour le joueur
                inCounterWindow = true;
                counterTimer    = counterAttackWindow;
                p.TakeDamage(attackDamage);
            }
        }

        void ThrowSmokeGrenade()
        {
            GasSystem.Instance?.DeployGas(GasType.Fume, player.position, smokeGrenadeRange, false);
            anim?.SetTrigger("ThrowGrenade");
        }

        // ── Séquence de Révélation ────────────────────────────────────────────
        IEnumerator RevealSequence()
        {
            GameManager.Instance?.ChangeState(GameState.Cutscene);
            anim?.SetTrigger("RemoveMask");
            yield return new WaitForSeconds(3f);

            // Dialogue de révélation
            DialogueSystem.Instance?.StartDialogue(revelationDialogue);
            yield return new WaitUntil(() => GameManager.Instance?.currentState == GameState.Playing);

            // Choix moral : pardonner Liam ?
            DialogueSystem.Instance?.StartDialogue(pardonDialogue);
        }

        // ── Pardon / Alliance ─────────────────────────────────────────────────
        /// <summary>Appelé quand le joueur choisit de pardonner Liam.</summary>
        public void Pardon()
        {
            isAlly = true;
            ReputationSystem.Instance?.OnLiamSpared();
            HUDManager.Instance?.ShowNotification("Liam rejoint l'équipe !");
            anim?.SetBool("IsAlly", true);
            Debug.Log("[Liam] Pardonné — devient allié");
        }

        /// <summary>Appelé si le joueur affronte Liam jusqu'à la mort.</summary>
        public void TakeDamage(float amount)
        {
            if (!isAlive) return;
            currentHealth -= amount;

            if (currentHealth <= 0f)
            {
                isAlive = false;
                anim?.SetTrigger("Die");
                // Fin B débloquée si Liam meurt
                Debug.Log("[Liam] Mort — Fin B disponible");
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, 20f);
        }
    }


    // ══════════════════════════════════════════════════════════════════════════
    //  DR. WILLIAM FROST — Chimiste / NPC clé
    // ══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Dr. Frost — passe de recrue mercenaire à allié des chimistes.
    /// Donne accès aux formules chimiques et compétences spéciales.
    /// </summary>
    public class DrFrostCharacter : MonoBehaviour
    {
        [Header("Statut")]
        public FrostAlignment alignment = FrostAlignment.Mercenary;
        public bool hasBetrayed         = false;   // A-t-il saboté le gaz ?

        [Header("Dialogues")]
        public DialogueData introDialogue;
        public DialogueData betrayalDialogue;
        public DialogueData allianceDialogue;

        [Header("Récompenses")]
        public string[] unlockedRecipes;   // IDs de formules chimiques débloquées

        Animator anim;

        void Awake() => anim = GetComponent<Animator>();

        // ── Arc narratif ──────────────────────────────────────────────────────
        public void TriggerBetrayal()
        {
            if (hasBetrayed) return;
            hasBetrayed = true;
            alignment   = FrostAlignment.Chemists;

            // Remplace Gaz Inferno → Gaz Rire — événement clé de l'Acte II
            GasSystem.Instance?.NeutralizeGas(GasType.Inferno);
            StartCoroutine(SabotageSequence());
        }

        IEnumerator SabotageSequence()
        {
            anim?.SetTrigger("Sabotage");
            yield return new WaitForSeconds(2f);

            // Déclenche le chaos du Gaz Rire sur toute la ville
            GasSystem.Instance?.TriggerCitywideLaugh();
            DialogueSystem.Instance?.StartDialogue(betrayalDialogue);

            GameManager.Instance?.AdvanceAct();  // Passage Acte I → II
            Debug.Log("[Frost] SABOTAGE — Gaz Inferno remplacé par Gaz Rire !");
        }

        // ── Devenir allié ─────────────────────────────────────────────────────
        public void BecomeAlly()
        {
            alignment = FrostAlignment.Ally;
            UnlockChemistryRecipes();
            SkillTree.Instance?.AddSkillPoints(3);  // Bonus compétences chimie
            HUDManager.Instance?.ShowNotification("Dr. Frost — Formules chimiques débloquées !");
        }

        void UnlockChemistryRecipes()
        {
            foreach (var recipe in unlockedRecipes)
            {
                SkillTree.Instance?.UnlockSkill(recipe);
                Debug.Log($"[Frost] Recette débloquée : {recipe}");
            }
        }
    }

    public enum FrostAlignment { Mercenary, Chemists, Ally }


    // ══════════════════════════════════════════════════════════════════════════
    //  CHARACTER MANAGER — Switching Alex / Lana
    // ══════════════════════════════════════════════════════════════════════════
    /// <summary>Gère le switch entre Alex et Lana en jeu.</summary>
    public class CharacterManager : MonoBehaviour
    {
        public static CharacterManager Instance { get; private set; }

        [Header("Personnages")]
        public PlayerController alexController;
        public PlayerController lanaController;
        public CharacterType    activeCharacter = CharacterType.Alex;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Update()
        {
            // Switch : Tab (PC) ou L3+R3 (manette)
            if (Input.GetKeyDown(KeyCode.Tab)) SwitchCharacter();
        }

        public void SwitchCharacter()
        {
            activeCharacter = activeCharacter == CharacterType.Alex
                ? CharacterType.Lana : CharacterType.Alex;

            bool alexActive = activeCharacter == CharacterType.Alex;
            alexController.gameObject.SetActive(alexActive);
            lanaController.gameObject.SetActive(!alexActive);

            // Caméra — Cinemachine suit le personnage actif
            var cam = FindFirstObjectByType<Cinemachine.CinemachineVirtualCamera>();
            if (cam != null)
                cam.Follow = alexActive ? alexController.transform : lanaController.transform;

            HUDManager.Instance?.ShowNotification(
                alexActive ? "Alex — Mode Combat" : "Lana — Mode Piratage");
            Debug.Log($"[CharManager] Personnage actif : {activeCharacter}");
        }

        public PlayerController GetActiveController() =>
            activeCharacter == CharacterType.Alex ? alexController : lanaController;
    }
}
