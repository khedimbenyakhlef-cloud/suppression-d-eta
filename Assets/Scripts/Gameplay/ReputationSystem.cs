using UnityEngine;
using System.Collections.Generic;

namespace SuppressionDeta
{
    /// <summary>
    /// Système de Réputation — 4 factions de Nova Déta.
    /// Chaque action du joueur impacte une ou plusieurs factions.
    /// Détermine les fins disponibles et les interactions PNJ.
    /// </summary>
    public class ReputationSystem : MonoBehaviour
    {
        public static ReputationSystem Instance { get; private set; }

        [Header("Valeurs initiales (0-100)")]
        public float citizenRep      = 50f;
        public float lawRep          = 50f;
        public float mercRep         = 50f;
        public float detaRep         = 50f;

        public delegate void OnReputationChanged(FactionType faction, float newValue);
        public event OnReputationChanged onReputationChanged;

        // Seuils de comportement
        const float HOSTILE_THRESHOLD  = 20f;
        const float FRIENDLY_THRESHOLD = 75f;
        const float ALLY_THRESHOLD     = 90f;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ── API principale ────────────────────────────────────────────────────
        public void AddReputation(FactionType faction, float delta)
        {
            float before = GetReputation(faction);
            float after  = Mathf.Clamp(before + delta, 0f, 100f);
            SetReputation(faction, after);

            onReputationChanged?.Invoke(faction, after);
            CheckReputationEvents(faction, before, after);
            Debug.Log($"[Rep] {faction}: {before:F0} → {after:F0} ({(delta >= 0 ? "+" : "")}{delta:F0})");
        }

        public float GetReputation(FactionType faction) => faction switch
        {
            FactionType.Citizens        => citizenRep,
            FactionType.LawEnforcement  => lawRep,
            FactionType.Mercenaries     => mercRep,
            FactionType.Deta            => detaRep,
            _                           => 50f
        };

        void SetReputation(FactionType faction, float value)
        {
            switch (faction)
            {
                case FactionType.Citizens:       citizenRep = value; break;
                case FactionType.LawEnforcement: lawRep     = value; break;
                case FactionType.Mercenaries:    mercRep    = value; break;
                case FactionType.Deta:           detaRep    = value; break;
            }
        }

        // ── Actions prédéfinies ───────────────────────────────────────────────
        public void OnCivilianHelped()
        {
            AddReputation(FactionType.Citizens, 8f);
            AddReputation(FactionType.LawEnforcement, 3f);
        }

        public void OnCivilianHarmed()
        {
            AddReputation(FactionType.Citizens, -15f);
            AddReputation(FactionType.LawEnforcement, -10f);
            AddReputation(FactionType.Mercenaries, 2f);
        }

        public void OnGuardNeutralizedNonLethal()
        {
            AddReputation(FactionType.LawEnforcement, -5f);
            AddReputation(FactionType.Citizens, 2f);
        }

        public void OnGuardKilled()
        {
            AddReputation(FactionType.LawEnforcement, -20f);
            AddReputation(FactionType.Citizens, -5f);
        }

        public void OnMissionForDeta()
        {
            AddReputation(FactionType.Deta, 10f);
            AddReputation(FactionType.Citizens, -5f);
        }

        public void OnDetaSabotaged()
        {
            AddReputation(FactionType.Deta, -20f);
            AddReputation(FactionType.Citizens, 10f);
            AddReputation(FactionType.LawEnforcement, 8f);
        }

        public void OnLiamSpared()
        {
            AddReputation(FactionType.Citizens, 15f);
            // Ouvre la Fin A — Rédemption
        }

        // ── Événements selon seuils ───────────────────────────────────────────
        void CheckReputationEvents(FactionType faction, float before, float after)
        {
            if (before >= HOSTILE_THRESHOLD && after < HOSTILE_THRESHOLD)
                TriggerHostileEvent(faction);
            if (before < FRIENDLY_THRESHOLD && after >= FRIENDLY_THRESHOLD)
                TriggerFriendlyEvent(faction);
            if (before < ALLY_THRESHOLD && after >= ALLY_THRESHOLD)
                TriggerAllyEvent(faction);
        }

        void TriggerHostileEvent(FactionType faction)
        {
            Debug.Log($"[Rep] {faction} est maintenant HOSTILE !");
            // Spawner des patrouilles supplémentaires, changer dialogues PNJ, etc.
        }

        void TriggerFriendlyEvent(FactionType faction)
        {
            Debug.Log($"[Rep] {faction} est maintenant AMICAL !");
            // Débloquer services, refuges, informateurs
        }

        void TriggerAllyEvent(FactionType faction)
        {
            Debug.Log($"[Rep] {faction} est maintenant ALLIÉ !");
            // Renforts disponibles, missions spéciales débloquées
        }

        // ── Calcul des fins disponibles ───────────────────────────────────────
        public EndingType GetAvailableEnding()
        {
            bool highCitizen = citizenRep >= 70f;
            bool highLaw     = lawRep     >= 60f;
            bool liamSpared  = detaRep    <= 30f && highCitizen;

            if (liamSpared && highCitizen && highLaw) return EndingType.A_Redemption;
            if (highCitizen && lawRep >= 50f)         return EndingType.B_Gardiens;
            if (detaRep >= 70f)                        return EndingType.C_Chute;
            return EndingType.B_Gardiens;
        }

        // Fin secrète D — nécessite tous les dossiers secrets
        public bool IsSecretEndingUnlocked(int collectedFiles, int totalFiles)
            => collectedFiles >= totalFiles && detaRep <= 20f;
    }

    public enum FactionType { Citizens, LawEnforcement, Mercenaries, Deta }
    public enum EndingType  { A_Redemption, B_Gardiens, C_Chute, D_Architecte }
}
