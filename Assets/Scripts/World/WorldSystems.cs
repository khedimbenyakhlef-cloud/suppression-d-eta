using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace SuppressionDeta
{
    // ══════════════════════════════════════════════════════════════════════════
    //  SKILL TREE — Alex / Lana / Dr. Frost
    // ══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Arbre de compétences à 3 branches.
    /// Persisté dans SaveSystem. Interface 3D neurale rendue dans SkillTreeUI.
    /// </summary>
    public class SkillTree : MonoBehaviour
    {
        public static SkillTree Instance { get; private set; }

        [Header("Points disponibles")]
        public int skillPoints = 0;

        [Header("Compétences débloquées")]
        public List<string> unlockedSkills = new();

        // Catalogue complet des compétences (3 branches × N nœuds)
        static readonly Dictionary<string, SkillNode> Catalog = new()
        {
            // ── Branche FORCE (Alex) ────────────────────────────────────────
            ["force_01"] = new SkillNode("Endurance I",      "force", 1, "+25 PV max",        1),
            ["force_02"] = new SkillNode("Endurance II",     "force", 2, "+50 PV max",        2, "force_01"),
            ["force_03"] = new SkillNode("Frappe lourde",    "force", 1, "+40% dégâts mêlée", 2, "force_01"),
            ["force_04"] = new SkillNode("Analyse tactique", "force", 2, "Ralenti 5 sec",     3, "force_02","force_03"),
            ["force_05"] = new SkillNode("Résistance gaz",   "force", 1, "Immunité Gaz Rire", 2, "force_01"),
            ["force_06"] = new SkillNode("Berserker",        "force", 3, "Combo 6 hits + dmg", 4,"force_03","force_04"),

            // ── Branche TECH (Lana) ─────────────────────────────────────────
            ["tech_01"]  = new SkillNode("Hack Range I",     "tech",  1, "+20m portée hack",  1),
            ["tech_02"]  = new SkillNode("Hack Range II",    "tech",  2, "+40m portée hack",  2, "tech_01"),
            ["tech_03"]  = new SkillNode("Drones Avancés",   "tech",  2, "Drone armé ×2",     3, "tech_01"),
            ["tech_04"]  = new SkillNode("Décryptage Rapide","tech",  1, "Puzzle -2 sec",     2, "tech_01"),
            ["tech_05"]  = new SkillNode("EMP de Zone",      "tech",  3, "Désactive tout 15m",4, "tech_02","tech_03"),
            ["tech_06"]  = new SkillNode("Ghost Mode",       "tech",  3, "Invisibilité 8 sec",4, "tech_02","tech_04"),

            // ── Branche CHIMIE (Dr. Frost) ───────────────────────────────────
            ["chem_01"]  = new SkillNode("Fabrication I",    "chem",  1, "Crée Gaz Fumée",    1),
            ["chem_02"]  = new SkillNode("Fabrication II",   "chem",  2, "Crée Gaz Inerte",   2, "chem_01"),
            ["chem_03"]  = new SkillNode("Antidote",         "chem",  2, "Immunité 60 sec",   2, "chem_01"),
            ["chem_04"]  = new SkillNode("Gaz Rire Maîtrisé","chem",  3, "Gaz Rire ciblé",    3, "chem_02"),
            ["chem_05"]  = new SkillNode("Bombe Fumigène+",  "chem",  2, "Rayon ×3, dur. ×2", 3, "chem_01","chem_02"),
            ["chem_06"]  = new SkillNode("Alchimiste",       "chem",  3, "Mélange 2 gaz",     4, "chem_04","chem_05"),
        };

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ── API ───────────────────────────────────────────────────────────────
        public bool CanUnlock(string id)
        {
            if (!Catalog.TryGetValue(id, out var node)) return false;
            if (unlockedSkills.Contains(id))            return false;
            if (skillPoints < node.cost)                return false;

            foreach (var prereq in node.prerequisites)
                if (!unlockedSkills.Contains(prereq))   return false;

            return true;
        }

        public bool UnlockSkill(string id)
        {
            if (!CanUnlock(id)) return false;
            var node = Catalog[id];
            skillPoints -= node.cost;
            unlockedSkills.Add(id);
            ApplySkillEffect(id);
            Debug.Log($"[SkillTree] Débloqué : {node.displayName}");
            return true;
        }

        public bool HasSkill(string id) => unlockedSkills.Contains(id);

        public void AddSkillPoints(int amount)
        {
            skillPoints += amount;
            HUDManager.Instance?.ShowNotification($"+{amount} points de compétence !");
        }

        void ApplySkillEffect(string id)
        {
            var player = FindObjectOfType<PlayerController>();
            if (player == null) return;

            switch (id)
            {
                case "force_01": player.maxHealth += 25f; break;
                case "force_02": player.maxHealth += 50f; break;
                case "force_03": player.meleeDamage *= 1.4f; break;
                case "force_04": player.tacticalSlowDuration = 5f; break;
                case "tech_01":  player.hackRange += 20f; break;
                case "tech_02":  player.hackRange += 40f; break;
                case "chem_03":  Debug.Log("[Skill] Antidote activé"); break;
            }
        }

        public static Dictionary<string, SkillNode> GetCatalog() => Catalog;
    }

    [System.Serializable]
    public class SkillNode
    {
        public string   id;
        public string   displayName;
        public string   branch;        // force / tech / chem
        public int      tier;
        public string   description;
        public int      cost;
        public string[] prerequisites;

        public SkillNode(string name, string branch, int tier, string desc, int cost, params string[] prereqs)
        {
            this.displayName   = name;
            this.branch        = branch;
            this.tier          = tier;
            this.description   = desc;
            this.cost          = cost;
            this.prerequisites = prereqs;
        }
    }


    // ══════════════════════════════════════════════════════════════════════════
    //  WORLD MANAGER — Nova Déta (cycle jour/nuit, météo, districts)
    // ══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Gestion du monde ouvert Nova Déta.
    /// Cycle jour/nuit (48 min réelles = 24h en jeu), météo dynamique,
    /// 7 districts avec factions dominantes.
    /// </summary>
    public class WorldManager : MonoBehaviour
    {
        public static WorldManager Instance { get; private set; }

        [Header("Cycle Jour/Nuit")]
        public Light  sunLight;
        public float  dayDuration   = 2880f;  // 48 min réelles
        public float  currentTime   = 0f;     // 0 = minuit, 0.5 = midi
        public Gradient skyGradient;
        public AnimationCurve sunIntensityCurve;

        [Header("Météo")]
        public WeatherType currentWeather = WeatherType.Clear;
        public float       weatherChangeProbability = 0.02f;  // Par minute
        public GameObject  rainParticles;
        public GameObject  fogVolume;
        public AudioSource ambientAudio;
        public AudioClip   rainSound;
        public AudioClip[] cityAmbient;

        [Header("Districts")]
        public List<DistrictData> districts = new();

        // État
        bool   isDaytime  = true;
        float  weatherTimer;
        const float WEATHER_INTERVAL = 60f;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Update()
        {
            UpdateDayNightCycle();
            UpdateWeatherTimer();
        }

        // ── Cycle Jour/Nuit ───────────────────────────────────────────────────
        void UpdateDayNightCycle()
        {
            currentTime += Time.deltaTime / dayDuration;
            if (currentTime >= 1f) currentTime = 0f;

            float angle     = currentTime * 360f - 90f;
            sunLight.transform.rotation = Quaternion.Euler(angle, 170f, 0f);
            sunLight.intensity          = sunIntensityCurve.Evaluate(currentTime);

            bool wasDay = isDaytime;
            isDaytime   = currentTime > 0.25f && currentTime < 0.75f;

            if (wasDay != isDaytime)
                OnDayNightTransition(isDaytime);
        }

        void OnDayNightTransition(bool toDay)
        {
            string period = toDay ? "Jour" : "Nuit";
            Debug.Log($"[World] Transition : {period}");

            // La nuit : marché noir actif, patrouilles changent
            if (!toDay)
            {
                foreach (var d in districts)
                    d.nightPatrolMultiplier = 1.5f;
            }
            else
            {
                foreach (var d in districts)
                    d.nightPatrolMultiplier = 1f;
            }
        }

        // ── Météo ─────────────────────────────────────────────────────────────
        void UpdateWeatherTimer()
        {
            weatherTimer += Time.deltaTime;
            if (weatherTimer < WEATHER_INTERVAL) return;
            weatherTimer = 0f;

            if (Random.value < weatherChangeProbability)
                ChangeWeather(GetRandomWeather());
        }

        WeatherType GetRandomWeather()
        {
            float r = Random.value;
            if (r < 0.5f)  return WeatherType.Clear;
            if (r < 0.75f) return WeatherType.Rain;
            if (r < 0.90f) return WeatherType.Fog;
            return WeatherType.ChemicalFog;   // Événement rare — Gaz Rire atmosphérique
        }

        public void ChangeWeather(WeatherType newWeather)
        {
            currentWeather = newWeather;

            rainParticles?.SetActive(newWeather == WeatherType.Rain);
            fogVolume?.SetActive(newWeather == WeatherType.Fog || newWeather == WeatherType.ChemicalFog);

            if (ambientAudio != null)
            {
                ambientAudio.clip   = newWeather == WeatherType.Rain ? rainSound : cityAmbient[0];
                ambientAudio.Play();
            }

            if (newWeather == WeatherType.ChemicalFog)
                TriggerChemicalFogEvent();

            Debug.Log($"[World] Météo : {newWeather}");
        }

        void TriggerChemicalFogEvent()
        {
            // Événement aléatoire : brouillard chimique = Gaz Rire atmosphérique faible
            var npcList = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
            foreach (var npc in npcList)
            {
                if (Random.value < 0.3f)
                    npc.ApplyGasEffect(GasType.Rire);
            }
        }

        // ── Districts ─────────────────────────────────────────────────────────
        public DistrictData GetCurrentDistrict(Vector3 playerPos)
        {
            foreach (var d in districts)
                if (d.bounds.Contains(playerPos))
                    return d;
            return null;
        }

        public float GetTimeAsHours() => currentTime * 24f;
        public bool  IsNight()         => !isDaytime;
    }

    [System.Serializable]
    public class DistrictData
    {
        public string      districtName;
        public FactionType dominantFaction;
        public Bounds      bounds;
        public float       nightPatrolMultiplier = 1f;
        public bool        isUnlocked            = true;
    }

    public enum WeatherType { Clear, Rain, Fog, ChemicalFog }


    // ══════════════════════════════════════════════════════════════════════════
    //  SECURITY CAMERA — Hackable par Lana
    // ══════════════════════════════════════════════════════════════════════════
    public class SecurityCamera : MonoBehaviour
    {
        [Header("Paramètres")]
        public float rotationSpeed  = 20f;
        public float maxAngle       = 60f;
        public float detectionRange = 15f;
        public LayerMask playerLayer;

        bool   isDisabled;
        bool   directionRight = true;
        float  currentAngle   = 0f;
        float  disableTimer;

        void Update()
        {
            if (isDisabled)
            {
                disableTimer -= Time.deltaTime;
                if (disableTimer <= 0f) isDisabled = false;
                return;
            }

            Rotate();
            DetectPlayer();
        }

        void Rotate()
        {
            float delta = rotationSpeed * Time.deltaTime * (directionRight ? 1f : -1f);
            currentAngle += delta;
            transform.Rotate(0f, delta, 0f);

            if (Mathf.Abs(currentAngle) >= maxAngle)
                directionRight = !directionRight;
        }

        void DetectPlayer()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, detectionRange, playerLayer);
            foreach (var h in hits)
            {
                Vector3 dir   = (h.transform.position - transform.position).normalized;
                float   angle = Vector3.Angle(transform.forward, dir);
                if (angle < 45f)
                {
                    Debug.Log("[Camera] Joueur détecté !");
                    // Alerter les ennemis du district
                }
            }
        }

        public void Disable(float duration = 30f)
        {
            isDisabled   = true;
            disableTimer = duration;
            Debug.Log($"[Camera] Désactivée {duration}s");
        }
    }


    // ══════════════════════════════════════════════════════════════════════════
    //  DRONE CONTROLLER — Contrôlé par Lana après hack
    // ══════════════════════════════════════════════════════════════════════════
    public class DroneController : MonoBehaviour
    {
        [Header("Vol")]
        public float moveSpeed    = 8f;
        public float hoverHeight  = 5f;
        public float rotateSpeed  = 180f;

        [Header("Combat")]
        public bool  isArmed      = false;
        public float droneDamage  = 20f;
        public float fireRate     = 0.5f;

        bool    isPlayerControlled;
        float   lastFireTime;
        Rigidbody rb;

        void Awake() => rb = GetComponent<Rigidbody>();

        public void TakeControl()
        {
            isPlayerControlled = true;
            Debug.Log("[Drone] Contrôle acquis par Lana");
        }

        public void ReleaseControl()
        {
            isPlayerControlled = false;
        }

        void FixedUpdate()
        {
            if (!isPlayerControlled) return;

            // Récupérer input (même système que PlayerController)
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            float y = Input.GetKey(KeyCode.Space) ? 1f : (Input.GetKey(KeyCode.LeftControl) ? -1f : 0f);

            Vector3 move = new Vector3(h, y, v) * moveSpeed;
            rb.linearVelocity = move;

            if (isArmed && Input.GetMouseButton(0))
                if (Time.time - lastFireTime >= fireRate)
                {
                    lastFireTime = Time.time;
                    ShootFromDrone();
                }
        }

        void ShootFromDrone()
        {
            if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, 30f))
                if (hit.collider.TryGetComponent<EnemyAI>(out var enemy))
                    enemy.TakeDamage(droneDamage);
        }
    }
}
