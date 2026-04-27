using UnityEngine;
using System.IO;
using System.Collections.Generic;

namespace SuppressionDeta
{
    /// <summary>
    /// Système de sauvegarde multi-plateforme.
    /// PC/Console : JSON sur disque
    /// Mobile      : PlayerPrefs + fichier local
    /// WebGL       : PlayerPrefs uniquement (pas de FileStream)
    /// Cross-save  : Structure compatible Epic Online Services (EOS)
    /// </summary>
    public class SaveSystem : MonoBehaviour
    {
        public static SaveSystem Instance { get; private set; }

        [Header("Paramètres")]
        public int   maxSaveSlots  = 10;
        public float autoSaveDelay = 300f;  // 5 minutes
        public string saveFileName = "suppression_deta_save";

        // Chemin de sauvegarde selon plateforme
        string SavePath => Application.persistentDataPath + "/" + saveFileName + ".json";

        float autoSaveTimer;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Update()
        {
            autoSaveTimer += Time.deltaTime;
            if (autoSaveTimer >= autoSaveDelay) { autoSaveTimer = 0f; SaveGame(); }
        }

        // ── Sauvegarde ────────────────────────────────────────────────────────
        public void SaveGame(int slot = 0)
        {
            var data = CollectSaveData(slot);
            string json = JsonUtility.ToJson(data, true);

#if UNITY_WEBGL
            PlayerPrefs.SetString(saveFileName + "_" + slot, json);
            PlayerPrefs.Save();
            Debug.Log("[Save] Sauvegardé dans PlayerPrefs (WebGL)");
#else
            string path = Application.persistentDataPath + "/" + saveFileName + "_" + slot + ".json";
            File.WriteAllText(path, json);
            Debug.Log($"[Save] Sauvegardé : {path}");
#endif
        }

        // ── Chargement ────────────────────────────────────────────────────────
        public void LoadGame(int slot = 0)
        {
            string json = null;

#if UNITY_WEBGL
            json = PlayerPrefs.GetString(saveFileName + "_" + slot, null);
#else
            string path = Application.persistentDataPath + "/" + saveFileName + "_" + slot + ".json";
            if (File.Exists(path)) json = File.ReadAllText(path);
#endif

            if (string.IsNullOrEmpty(json))
            {
                Debug.Log("[Save] Aucune sauvegarde trouvée — nouvelle partie");
                return;
            }

            var data = JsonUtility.FromJson<SaveData>(json);
            ApplySaveData(data);
            Debug.Log($"[Save] Partie chargée — Acte {data.currentAct}, Temps: {data.playTime:F0}s");
        }

        // ── Collecte des données ──────────────────────────────────────────────
        SaveData CollectSaveData(int slot)
        {
            var gm  = GameManager.Instance;
            var rep = ReputationSystem.Instance;

            return new SaveData
            {
                slot        = slot,
                saveDate    = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                playTime    = Time.realtimeSinceStartup,
                currentAct  = gm?.currentAct ?? 1,
                // Réputation
                citizenRep  = rep?.citizenRep ?? 50f,
                lawRep      = rep?.lawRep     ?? 50f,
                mercRep     = rep?.mercRep    ?? 50f,
                detaRep     = rep?.detaRep    ?? 50f,
                // Position
                playerPos   = new SerializableVector3(
                    GameObject.FindGameObjectWithTag("Player")?.transform.position ?? Vector3.zero),
                // Quêtes
                completedQuests = QuestManager.Instance?.GetCompletedQuestIDs() ?? new List<string>(),
                collectedFiles  = 0,
            };
        }

        void ApplySaveData(SaveData data)
        {
            var gm  = GameManager.Instance;
            var rep = ReputationSystem.Instance;

            if (gm  != null) gm.currentAct = data.currentAct;
            if (rep != null)
            {
                rep.citizenRep = data.citizenRep;
                rep.lawRep     = data.lawRep;
                rep.mercRep    = data.mercRep;
                rep.detaRep    = data.detaRep;
            }
        }

        public bool HasSave(int slot = 0)
        {
#if UNITY_WEBGL
            return PlayerPrefs.HasKey(saveFileName + "_" + slot);
#else
            return File.Exists(Application.persistentDataPath + "/" + saveFileName + "_" + slot + ".json");
#endif
        }

        public void DeleteSave(int slot)
        {
#if UNITY_WEBGL
            PlayerPrefs.DeleteKey(saveFileName + "_" + slot);
#else
            string path = Application.persistentDataPath + "/" + saveFileName + "_" + slot + ".json";
            if (File.Exists(path)) File.Delete(path);
#endif
        }
    }

    // ── Structures de données ─────────────────────────────────────────────────
    [System.Serializable]
    public class SaveData
    {
        public int    slot;
        public string saveDate;
        public float  playTime;
        public int    currentAct;
        // Réputation
        public float  citizenRep, lawRep, mercRep, detaRep;
        // Position
        public SerializableVector3 playerPos;
        // Progression
        public List<string> completedQuests;
        public int          collectedFiles;
    }

    [System.Serializable]
    public class SerializableVector3
    {
        public float x, y, z;
        public SerializableVector3(Vector3 v) { x = v.x; y = v.y; z = v.z; }
        public Vector3 ToVector3() => new Vector3(x, y, z);
    }
}
