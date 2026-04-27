using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace SuppressionDeta
{
    /// <summary>
    /// Système de Gaz — mécanique signature de Suppression Déta.
    /// Gère les 4 types de gaz, leurs zones d'effet, particules VFX
    /// et interactions avec joueurs / ennemis / environnement.
    /// </summary>
    public class GasSystem : MonoBehaviour
    {
        public static GasSystem Instance { get; private set; }

        [Header("Paramètres Gaz")]
        public float defaultRadius      = 15f;
        public float spreadRate         = 0.5f;   // Unités/sec
        public float maxSpreadRadius    = 60f;
        public LayerMask affectedLayers;

        [Header("Prefabs VFX")]
        public GameObject infernoVFXPrefab;
        public GameObject rireVFXPrefab;
        public GameObject inerteVFXPrefab;
        public GameObject fumeVFXPrefab;

        [Header("Couleurs")]
        public Color infernoColor = new Color(1f, 0.2f, 0f, 0.5f);
        public Color rireColor    = new Color(0.8f, 1f, 0f, 0.4f);
        public Color inerteColor  = new Color(0.9f, 0.9f, 1f, 0.35f);
        public Color fumeColor    = new Color(0.5f, 0.5f, 0.5f, 0.3f);

        List<GasZone> activeZones = new();

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ── API publique ──────────────────────────────────────────────────────
        /// <summary>Déploie un gaz à une position donnée.</summary>
        public GasZone DeployGas(GasType type, Vector3 position, float radius = -1f, bool spreads = false)
        {
            float r = radius < 0 ? defaultRadius : radius;

            GameObject vfxPrefab = type switch
            {
                GasType.Inferno => infernoVFXPrefab,
                GasType.Rire    => rireVFXPrefab,
                GasType.Inerte  => inerteVFXPrefab,
                GasType.Fume    => fumeVFXPrefab,
                _               => fumeVFXPrefab
            };

            var zone = new GasZone
            {
                type       = type,
                center     = position,
                radius     = r,
                spreads    = spreads,
                vfxInstance = vfxPrefab ? Instantiate(vfxPrefab, position, Quaternion.identity) : null
            };

            activeZones.Add(zone);
            StartCoroutine(GasZoneLifecycle(zone));
            Debug.Log($"[GasSystem] {type} déployé à {position} — rayon {r}m");
            return zone;
        }

        /// <summary>Neutralise toutes les zones d'un type donné.</summary>
        public void NeutralizeGas(GasType type)
        {
            var toRemove = activeZones.FindAll(z => z.type == type);
            foreach (var z in toRemove) RemoveZone(z);
        }

        /// <summary>Vérifie si une position est dans une zone de gaz.</summary>
        public bool IsInGasZone(Vector3 pos, out GasType detectedType)
        {
            foreach (var zone in activeZones)
            {
                if (Vector3.Distance(pos, zone.center) <= zone.radius)
                {
                    detectedType = zone.type;
                    return true;
                }
            }
            detectedType = GasType.Fume;
            return false;
        }

        // ── Cycle de vie d'une zone ───────────────────────────────────────────
        IEnumerator GasZoneLifecycle(GasZone zone)
        {
            float elapsed = 0f;

            while (zone.active)
            {
                elapsed += Time.deltaTime;

                // Propagation progressive
                if (zone.spreads && zone.radius < maxSpreadRadius)
                    zone.radius += spreadRate * Time.deltaTime;

                // VFX scale
                if (zone.vfxInstance != null)
                    zone.vfxInstance.transform.localScale = Vector3.one * (zone.radius / defaultRadius);

                // Tick effet sur les entités présentes
                if (elapsed >= 1f)
                {
                    elapsed = 0f;
                    ApplyGasToEntities(zone);
                }

                yield return null;
            }

            RemoveZone(zone);
        }

        void ApplyGasToEntities(GasZone zone)
        {
            Collider[] hits = Physics.OverlapSphere(zone.center, zone.radius, affectedLayers);
            foreach (var hit in hits)
            {
                if (hit.TryGetComponent<PlayerController>(out var player))
                    player.ApplyGasEffect(zone.type);

                if (hit.TryGetComponent<EnemyAI>(out var enemy))
                    enemy.ApplyGasEffect(zone.type);
            }
        }

        void RemoveZone(GasZone zone)
        {
            zone.active = false;
            if (zone.vfxInstance != null)
                Destroy(zone.vfxInstance, 2f);
            activeZones.Remove(zone);
        }

        // ── Événement scénaristique : Chaos du Gaz Rire ───────────────────────
        /// <summary>
        /// Déclenche le chaos de l'Acte II : Gaz Rire dans toute la ville.
        /// </summary>
        public void TriggerCitywideLaugh()
        {
            StartCoroutine(CitywideLaughSequence());
        }

        IEnumerator CitywideLaughSequence()
        {
            Debug.Log("[GasSystem] CHAOS — Gaz Rire se répand dans Nova Déta !");

            // Phase 1 — quelques zones
            for (int i = 0; i < 5; i++)
            {
                Vector3 rndPos = transform.position + Random.insideUnitSphere * 100f;
                rndPos.y       = 0f;
                DeployGas(GasType.Rire, rndPos, 20f, true);
                yield return new WaitForSeconds(3f);
            }

            // Phase 2 — propagation totale
            yield return new WaitForSeconds(10f);
            GameManager.Instance?.ChangeState(GameState.Cutscene);
        }

        void OnDrawGizmos()
        {
            foreach (var zone in activeZones)
            {
                Gizmos.color = zone.type switch
                {
                    GasType.Inferno => new Color(1f, 0.2f, 0f, 0.3f),
                    GasType.Rire    => new Color(0.8f, 1f, 0f, 0.3f),
                    GasType.Inerte  => new Color(0.8f, 0.8f, 1f, 0.3f),
                    _               => new Color(0.5f, 0.5f, 0.5f, 0.2f)
                };
                Gizmos.DrawWireSphere(zone.center, zone.radius);
            }
        }
    }

    // ── Données ───────────────────────────────────────────────────────────────
    public class GasZone
    {
        public GasType    type;
        public Vector3    center;
        public float      radius;
        public bool       spreads;
        public bool       active      = true;
        public GameObject vfxInstance;
    }

    public enum GasType { Inferno, Rire, Inerte, Fume }

    [System.Serializable]
    public class GasMask : MonoBehaviour
    {
        public bool isEquipped = false;

        public void Equip()   => isEquipped = true;
        public void UnEquip() => isEquipped = false;
    }
}
