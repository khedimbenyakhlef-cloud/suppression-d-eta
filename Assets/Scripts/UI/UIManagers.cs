using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace SuppressionDeta
{
    // ══════════════════════════════════════════════════════════════════════════
    //  QUEST MANAGER
    // ══════════════════════════════════════════════════════════════════════════
    /// <summary>Gère toutes les quêtes principales et secondaires des 4 actes.</summary>
    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }

        [Header("Quêtes")]
        public List<QuestData> allQuests = new();

        Dictionary<string, QuestData> questMap = new();
        List<string> completedIDs = new();

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            foreach (var q in allQuests) questMap[q.id] = q;
        }

        public void StartQuest(string questID)
        {
            if (!questMap.TryGetValue(questID, out var q)) return;
            q.state = QuestState.Active;
            Debug.Log($"[Quest] Démarrée : {q.title}");
        }

        public void CompleteObjective(string questID, string objectiveID)
        {
            if (!questMap.TryGetValue(questID, out var q)) return;
            var obj = q.objectives.Find(o => o.id == objectiveID);
            if (obj != null) obj.completed = true;

            if (q.objectives.TrueForAll(o => o.completed)) CompleteQuest(questID);
        }

        void CompleteQuest(string questID)
        {
            if (!questMap.TryGetValue(questID, out var q)) return;
            q.state = QuestState.Completed;
            completedIDs.Add(questID);

            ReputationSystem.Instance?.AddReputation(q.rewardFaction, q.rewardReputation);
            Debug.Log($"[Quest] Complétée : {q.title} — +{q.rewardReputation} rep {q.rewardFaction}");
        }

        public void RefreshActQuests(int act)
        {
            foreach (var q in allQuests)
                if (q.act == act && q.state == QuestState.NotStarted)
                    StartQuest(q.id);
        }

        public List<string> GetCompletedQuestIDs() => completedIDs;
    }

    [System.Serializable]
    public class QuestData
    {
        public string       id;
        public string       title;
        public string       description;
        public int          act;
        public QuestState   state          = QuestState.NotStarted;
        public FactionType  rewardFaction  = FactionType.Citizens;
        public float        rewardReputation = 5f;
        public List<QuestObjective> objectives = new();
    }

    [System.Serializable]
    public class QuestObjective { public string id; public string label; public bool completed; }
    public enum QuestState { NotStarted, Active, Completed, Failed }


    // ══════════════════════════════════════════════════════════════════════════
    //  DIALOGUE SYSTEM
    // ══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Système de dialogue avec choix moraux.
    /// Chaque choix peut impacter la réputation et débloquer des fins.
    /// </summary>
    public class DialogueSystem : MonoBehaviour
    {
        public static DialogueSystem Instance { get; private set; }

        [Header("UI Dialogue")]
        public GameObject dialoguePanel;
        public TextMeshProUGUI speakerText;
        public TextMeshProUGUI dialogueText;
        public Button[] choiceButtons;
        public TextMeshProUGUI[] choiceTexts;

        DialogueData currentDialogue;
        int          currentNodeIndex;
        bool         isActive;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            dialoguePanel?.SetActive(false);
        }

        public void StartDialogue(DialogueData data)
        {
            currentDialogue  = data;
            currentNodeIndex = 0;
            isActive         = true;
            GameManager.Instance?.ChangeState(GameState.Cutscene);
            dialoguePanel?.SetActive(true);
            ShowNode(0);
        }

        void ShowNode(int index)
        {
            if (index >= currentDialogue.nodes.Count) { EndDialogue(); return; }
            var node = currentDialogue.nodes[index];

            speakerText.text  = node.speaker;
            dialogueText.text = node.text;

            // Masquer tous les boutons
            foreach (var btn in choiceButtons) btn.gameObject.SetActive(false);

            if (node.choices.Count == 0)
            {
                // Pas de choix — avancement automatique au clic
                choiceButtons[0].gameObject.SetActive(true);
                choiceTexts[0].text = "Continuer";
                choiceButtons[0].onClick.RemoveAllListeners();
                choiceButtons[0].onClick.AddListener(() => ShowNode(index + 1));
            }
            else
            {
                for (int i = 0; i < node.choices.Count && i < choiceButtons.Length; i++)
                {
                    int ci = i;
                    var choice = node.choices[i];
                    choiceButtons[i].gameObject.SetActive(true);
                    choiceTexts[i].text = choice.label;
                    choiceButtons[i].onClick.RemoveAllListeners();
                    choiceButtons[i].onClick.AddListener(() => SelectChoice(node, choice));
                }
            }
        }

        void SelectChoice(DialogueNode node, DialogueChoice choice)
        {
            // Appliquer impact réputation
            if (choice.reputationFaction != FactionType.Citizens || choice.reputationDelta != 0)
                ReputationSystem.Instance?.AddReputation(choice.reputationFaction, choice.reputationDelta);

            // Naviguer vers le nœud cible
            int targetIndex = currentDialogue.nodes.FindIndex(n => n.id == choice.targetNodeID);
            if (targetIndex >= 0) ShowNode(targetIndex);
            else EndDialogue();
        }

        void EndDialogue()
        {
            isActive = false;
            dialoguePanel?.SetActive(false);
            GameManager.Instance?.ChangeState(GameState.Playing);
        }
    }

    [System.Serializable]
    public class DialogueData
    {
        public string            id;
        public List<DialogueNode> nodes = new();
    }

    [System.Serializable]
    public class DialogueNode
    {
        public string              id;
        public string              speaker;
        [TextArea] public string   text;
        public List<DialogueChoice> choices = new();
    }

    [System.Serializable]
    public class DialogueChoice
    {
        public string      label;
        public string      targetNodeID;
        public FactionType reputationFaction = FactionType.Citizens;
        public float       reputationDelta   = 0f;
    }


    // ══════════════════════════════════════════════════════════════════════════
    //  HUD MANAGER
    // ══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// HUD holographique — santé, énergie, gaz, réputation, minimap.
    /// Optimisé pour toutes les résolutions (PC 4K, console, mobile, WebGL).
    /// </summary>
    public class HUDManager : MonoBehaviour
    {
        public static HUDManager Instance { get; private set; }

        [Header("Santé / Énergie")]
        public Slider healthBar;
        public Slider hackEnergyBar;
        public Image  healthFill;
        public Color  healthHighColor = new Color(0.2f, 0.8f, 0.4f);
        public Color  healthLowColor  = new Color(0.9f, 0.2f, 0.2f);

        [Header("Gaz")]
        public GameObject gasIndicator;
        public Image      gasIcon;
        public TextMeshProUGUI gasTypeText;
        public Color[]    gasColors;   // [Inferno, Rire, Inerte, Fume]

        [Header("Réputation")]
        public Image[] factionIcons;   // [Citizen, Law, Merc, Deta]
        public Slider[] factionBars;

        [Header("Minimap")]
        public RawImage minimapRender;
        public GameObject minimapRoot;

        [Header("Notifications")]
        public GameObject notifPanel;
        public TextMeshProUGUI notifText;

        PlayerController player;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            player = FindObjectOfType<PlayerController>();
            ReputationSystem.Instance.onReputationChanged += OnReputationChanged;
            AdaptToResolution();
        }

        void Update()
        {
            if (player == null) return;
            UpdateHealthBar();
            UpdateHackEnergy();
            UpdateGasIndicator();
        }

        void UpdateHealthBar()
        {
            if (healthBar == null) return;
            float ratio = player.currentHealth / player.maxHealth;
            healthBar.value        = ratio;
            healthFill.color       = Color.Lerp(healthLowColor, healthHighColor, ratio);
        }

        void UpdateHackEnergy()
        {
            if (hackEnergyBar == null) return;
            hackEnergyBar.value = player.currentHackEnergy / player.hackEnergy;
            hackEnergyBar.gameObject.SetActive(player.character == CharacterType.Lana);
        }

        void UpdateGasIndicator()
        {
            if (GasSystem.Instance == null) return;
            bool inGas = GasSystem.Instance.IsInGasZone(player.transform.position, out GasType gt);
            gasIndicator?.SetActive(inGas);

            if (inGas && gasIcon != null)
            {
                gasIcon.color      = gasColors.Length > (int)gt ? gasColors[(int)gt] : Color.white;
                gasTypeText.text   = gt.ToString();
            }
        }

        void OnReputationChanged(FactionType faction, float value)
        {
            int i = (int)faction;
            if (i < factionBars.Length) factionBars[i].value = value / 100f;
        }

        public void ShowNotification(string message, float duration = 3f)
        {
            StopAllCoroutines();
            StartCoroutine(NotifRoutine(message, duration));
        }

        IEnumerator NotifRoutine(string msg, float duration)
        {
            notifPanel?.SetActive(true);
            notifText.text = msg;
            yield return new WaitForSeconds(duration);
            notifPanel?.SetActive(false);
        }

        // Adaptation résolution plateforme
        void AdaptToResolution()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null) return;

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

#if UNITY_IOS || UNITY_ANDROID
            // Réduire taille HUD sur mobile
            transform.localScale = Vector3.one * 0.85f;
#endif
        }

        void OnDestroy()
        {
            if (ReputationSystem.Instance != null)
                ReputationSystem.Instance.onReputationChanged -= OnReputationChanged;
        }
    }
}
