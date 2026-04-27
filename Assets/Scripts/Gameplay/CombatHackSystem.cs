using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace SuppressionDeta
{
    // ══════════════════════════════════════════════════════════════════════════
    //  COMBAT SYSTEM — Alex (mêlée + armes)
    // ══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Gestion des armes, munitions, combos mêlée et couverture.
    /// Compatible clavier/souris (PC), manette avec vibration (PS5/Xbox),
    /// boutons tactiles (iOS/Android).
    /// </summary>
    public class CombatSystem : MonoBehaviour
    {
        public static CombatSystem Instance { get; private set; }

        [Header("Arme active")]
        public WeaponData currentWeapon;
        public Transform  weaponMount;
        public GameObject weaponModel;

        [Header("Munitions")]
        public int currentAmmo;
        public int reserveAmmo;

        [Header("Couverture")]
        public bool  inCover          = false;
        public float coverBonus       = 0.5f;   // -50% dégâts reçus
        public LayerMask coverLayer;

        [Header("Combo mêlée")]
        public float comboWindow      = 0.8f;
        public int   maxComboHits     = 4;

        // Références
        PlayerController player;
        Camera           cam;
        Animator         anim;

        // État interne
        int   comboCount;
        float lastHitTime;
        bool  isReloading;
        bool  isAiming;

        // Pool de balles (performance mobile)
        Queue<GameObject> bulletPool = new();
        [Header("Pool")]
        public GameObject bulletPrefab;
        public int        poolSize = 20;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            player = GetComponent<PlayerController>();
            cam    = Camera.main;
            anim   = GetComponent<Animator>();
            InitBulletPool();
            if (currentWeapon != null) EquipWeapon(currentWeapon);
        }

        // ── Pool de projectiles ───────────────────────────────────────────────
        void InitBulletPool()
        {
            for (int i = 0; i < poolSize; i++)
            {
                var b = Instantiate(bulletPrefab);
                b.SetActive(false);
                bulletPool.Enqueue(b);
            }
        }

        GameObject GetBullet()
        {
            if (bulletPool.Count > 0)
            {
                var b = bulletPool.Dequeue();
                b.SetActive(true);
                return b;
            }
            return Instantiate(bulletPrefab);
        }

        public void ReturnBullet(GameObject b)
        {
            b.SetActive(false);
            bulletPool.Enqueue(b);
        }

        // ── Tir ──────────────────────────────────────────────────────────────
        public void Shoot()
        {
            if (currentWeapon == null || isReloading) return;
            if (currentAmmo <= 0) { StartCoroutine(Reload()); return; }

            currentAmmo--;
            anim?.SetTrigger("Shoot");

            // Raycast (armes hitscan)
            if (currentWeapon.isHitscan)
            {
                Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
                float spread = isAiming ? currentWeapon.aimSpread : currentWeapon.hipSpread;
                ray.direction += Random.insideUnitSphere * spread;

                if (Physics.Raycast(ray, out RaycastHit hit, currentWeapon.range))
                {
                    OnBulletHit(hit);
                    SpawnImpactFX(hit.point, hit.normal);
                }
            }
            else
            {
                // Projectile physique (grenades, lance-roquettes)
                var bullet = GetBullet();
                bullet.transform.SetPositionAndRotation(weaponMount.position, weaponMount.rotation);
                if (bullet.TryGetComponent<Rigidbody>(out var rb))
                    rb.linearVelocity = weaponMount.forward * currentWeapon.projectileSpeed;
            }

            // Vibration manette PS5/Xbox
            TriggerHaptics(currentWeapon.hapticIntensity);

            // Auto-reload
            if (currentAmmo == 0) StartCoroutine(Reload());
        }

        void OnBulletHit(RaycastHit hit)
        {
            float dmg = currentWeapon.damage;

            // Bonus headshot
            if (hit.collider.CompareTag("Head")) dmg *= 2.5f;

            // Réduction si l'ennemi est en couverture
            if (hit.collider.TryGetComponent<EnemyAI>(out var enemy))
            {
                if (enemy.inCover ?? false) dmg *= (1f - coverBonus);
                enemy.TakeDamage(dmg);
            }
        }

        void SpawnImpactFX(Vector3 pos, Vector3 normal)
        {
            // Instancier sparks/decal — géré par PoolManager en production
            Debug.DrawRay(pos, normal * 0.3f, Color.yellow, 0.5f);
        }

        // ── Rechargement ─────────────────────────────────────────────────────
        IEnumerator Reload()
        {
            if (isReloading || reserveAmmo <= 0) yield break;
            isReloading = true;
            anim?.SetTrigger("Reload");
            yield return new WaitForSeconds(currentWeapon.reloadTime);

            int needed = currentWeapon.clipSize - currentAmmo;
            int taken  = Mathf.Min(needed, reserveAmmo);
            currentAmmo  += taken;
            reserveAmmo  -= taken;
            isReloading   = false;
        }

        // ── Combo mêlée ───────────────────────────────────────────────────────
        public void MeleeAttack()
        {
            if (Time.time - lastHitTime > comboWindow) comboCount = 0;

            comboCount  = Mathf.Min(comboCount + 1, maxComboHits);
            lastHitTime = Time.time;
            anim?.SetInteger("ComboStep", comboCount);
            anim?.SetTrigger("Melee");

            float dmg = player.meleeDamage * (1f + (comboCount - 1) * 0.25f);
            Collider[] hits = Physics.OverlapSphere(
                transform.position + transform.forward * 1.2f, player.meleeRange,
                LayerMask.GetMask("Enemy"));

            foreach (var h in hits)
                if (h.TryGetComponent<EnemyAI>(out var e)) e.TakeDamage(dmg);

            TriggerHaptics(0.3f + comboCount * 0.15f);
        }

        // ── Couverture ────────────────────────────────────────────────────────
        public void TakeCover()
        {
            Collider[] walls = Physics.OverlapSphere(transform.position, 1.5f, coverLayer);
            inCover = walls.Length > 0;
            anim?.SetBool("InCover", inCover);
        }

        // ── Équipement ────────────────────────────────────────────────────────
        public void EquipWeapon(WeaponData weapon)
        {
            currentWeapon = weapon;
            currentAmmo   = weapon.clipSize;
            reserveAmmo   = weapon.startingAmmo;
            Debug.Log($"[Combat] Équipé : {weapon.weaponName}");
        }

        // ── Haptique (PS5 DualSense / Xbox) ──────────────────────────────────
        void TriggerHaptics(float intensity)
        {
#if UNITY_PS5 || UNITY_XBOXONE || UNITY_GAMECORE
            // Nécessite le package Input System + Haptics API
            // UnityEngine.InputSystem.Haptics...
            Debug.Log($"[Haptics] Vibration : {intensity:F2}");
#endif
        }

        void Update()
        {
            // Aiming — zoom caméra géré par Cinemachine
            if (Input.GetMouseButton(1)) isAiming = true;
            else isAiming = false;
        }
    }

    // ── Données arme ──────────────────────────────────────────────────────────
    [CreateAssetMenu(fileName = "NewWeapon", menuName = "SuppressionDeta/Weapon")]
    public class WeaponData : ScriptableObject
    {
        public string weaponName     = "Pistolet";
        public float  damage         = 25f;
        public float  range          = 50f;
        public int    clipSize       = 12;
        public int    startingAmmo   = 48;
        public float  reloadTime     = 1.8f;
        public float  hipSpread      = 0.05f;
        public float  aimSpread      = 0.01f;
        public float  hapticIntensity= 0.4f;
        public bool   isHitscan      = true;
        public float  projectileSpeed= 30f;
    }


    // ══════════════════════════════════════════════════════════════════════════
    //  HACK SYSTEM — Lana (Neural Hack)
    // ══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Interface de piratage de Lana.
    /// Mini-jeu de piratage : puzzle logique + timer.
    /// Cibles hackables : caméras, drones, portes, systèmes de gaz, ennemis.
    /// </summary>
    public class HackSystem : MonoBehaviour
    {
        public static HackSystem Instance { get; private set; }

        [Header("Paramètres")]
        public float hackRadius      = 50f;
        public float hackDuration    = 5f;    // Durée du mini-jeu
        public float energyCostBase  = 20f;
        public LayerMask hackableLayer;

        [Header("UI Piratage")]
        public GameObject hackPanel;
        public UnityEngine.UI.Slider hackProgressBar;
        public TMPro.TextMeshProUGUI hackTargetText;
        public UnityEngine.UI.Button[] codeButtons;   // Puzzle boutons

        PlayerController lana;
        HackableObject   currentTarget;
        bool             isHacking;
        float            hackTimer;
        int[]            correctSequence;
        int[]            playerSequence;
        int              seqIndex;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            lana = FindObjectOfType<PlayerController>();
            hackPanel?.SetActive(false);
        }

        // ── Scanner les cibles ────────────────────────────────────────────────
        public List<HackableObject> ScanHackables()
        {
            var results = new List<HackableObject>();
            Collider[] hits = Physics.OverlapSphere(transform.position, hackRadius, hackableLayer);
            foreach (var h in hits)
                if (h.TryGetComponent<HackableObject>(out var obj) && obj.isHackable)
                    results.Add(obj);
            return results;
        }

        // ── Démarrer piratage ─────────────────────────────────────────────────
        public void BeginHack(HackableObject target)
        {
            if (isHacking || lana.currentHackEnergy < energyCostBase) return;

            currentTarget = target;
            isHacking     = true;
            hackTimer     = 0f;
            seqIndex      = 0;

            GeneratePuzzle(target.hackDifficulty);
            hackPanel?.SetActive(true);
            hackTargetText.text = $"Piratage : {target.targetName}";
            GameManager.Instance?.ChangeState(GameState.Cutscene);

            Debug.Log($"[Hack] Début piratage — {target.targetName} (diff: {target.hackDifficulty})");
        }

        // ── Puzzle de séquence ────────────────────────────────────────────────
        void GeneratePuzzle(int difficulty)
        {
            int length        = 3 + difficulty;
            correctSequence   = new int[length];
            playerSequence    = new int[length];

            for (int i = 0; i < length; i++)
                correctSequence[i] = Random.Range(0, codeButtons.Length);

            // Assigner les callbacks aux boutons
            for (int i = 0; i < codeButtons.Length; i++)
            {
                int idx = i;
                codeButtons[i].onClick.RemoveAllListeners();
                codeButtons[i].onClick.AddListener(() => OnCodeButtonPressed(idx));
            }
        }

        void OnCodeButtonPressed(int buttonIndex)
        {
            if (!isHacking) return;

            if (buttonIndex == correctSequence[seqIndex])
            {
                seqIndex++;
                if (seqIndex >= correctSequence.Length)
                    CompleteHack();
            }
            else
            {
                // Erreur — pénalité timer
                hackTimer += 1.5f;
                StartCoroutine(FlashError());
            }
        }

        void Update()
        {
            if (!isHacking) return;

            hackTimer += Time.unscaledDeltaTime;
            if (hackProgressBar != null)
                hackProgressBar.value = hackTimer / hackDuration;

            if (hackTimer >= hackDuration)
                FailHack();
        }

        void CompleteHack()
        {
            isHacking = false;
            hackPanel?.SetActive(false);

            float cost = energyCostBase * currentTarget.hackDifficulty;
            lana.currentHackEnergy -= cost;
            currentTarget.OnHacked();

            GameManager.Instance?.ChangeState(GameState.Playing);
            HUDManager.Instance?.ShowNotification($"{currentTarget.targetName} piraté !");
            Debug.Log($"[Hack] Succès : {currentTarget.targetName}");
        }

        void FailHack()
        {
            isHacking = false;
            hackPanel?.SetActive(false);
            GameManager.Instance?.ChangeState(GameState.Playing);
            HUDManager.Instance?.ShowNotification("Piratage échoué !");
        }

        IEnumerator FlashError()
        {
            // Flash rouge du panel
            yield return new WaitForSecondsRealtime(0.2f);
        }
    }

    // ── Objet Hackable ────────────────────────────────────────────────────────
    public class HackableObject : MonoBehaviour
    {
        [Header("Propriétés")]
        public string targetName   = "Caméra";
        public int    hackDifficulty = 1;      // 1 à 5
        public bool   isHackable   = true;
        public HackableType hackType = HackableType.Camera;

        [Header("Effets")]
        public float hackDuration  = 30f;      // Combien de temps reste hacké
        public bool  isHacked      = false;

        public delegate void OnHackedEvent();
        public event OnHackedEvent onHackedEvent;

        public void OnHacked()
        {
            isHacked = true;
            onHackedEvent?.Invoke();
            StartCoroutine(HackExpiry());

            switch (hackType)
            {
                case HackableType.Camera:
                    DisableCamera();
                    break;
                case HackableType.Door:
                    OpenDoor();
                    break;
                case HackableType.Drone:
                    TakeDroneControl();
                    break;
                case HackableType.GasTank:
                    ManipulateGas();
                    break;
                case HackableType.Terminal:
                    AccessTerminal();
                    break;
            }
        }

        void DisableCamera()
        {
            var cam = GetComponent<SecurityCamera>();
            cam?.Disable();
            Debug.Log($"[Hack] Caméra {name} désactivée");
        }

        void OpenDoor()
        {
            var door = GetComponent<Animator>();
            door?.SetTrigger("Open");
            Debug.Log($"[Hack] Porte {name} ouverte");
        }

        void TakeDroneControl()
        {
            // Transfert contrôle vers drone — logique dans DroneController
            Debug.Log($"[Hack] Drone {name} sous contrôle");
        }

        void ManipulateGas()
        {
            // Remplacer ou neutraliser le gaz dans ce réservoir
            GasSystem.Instance?.NeutralizeGas(GasType.Inferno);
            GasSystem.Instance?.DeployGas(GasType.Rire, transform.position, 10f);
            Debug.Log($"[Hack] Réservoir {name} — Inferno → Rire !");
        }

        void AccessTerminal()
        {
            // Accéder aux données secrètes — contribution Fin D
            Debug.Log($"[Hack] Terminal {name} — données extraites");
        }

        IEnumerator HackExpiry()
        {
            yield return new WaitForSeconds(hackDuration);
            isHacked = false;
        }
    }

    public enum HackableType { Camera, Door, Drone, GasTank, Terminal, Enemy }
}
