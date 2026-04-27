using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

namespace SuppressionDeta
{
    /// <summary>
    /// Singleton central du jeu — gère les états globaux, les transitions de scène
    /// et coordonne tous les systèmes (Gas, Réputation, Quêtes, Sauvegarde).
    /// Plateformes : PC, PS5/Xbox, iOS/Android, WebGL
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Références Systèmes")]
        public GasSystem       gasSystem;
        public ReputationSystem reputationSystem;
        public QuestManager    questManager;
        public SaveSystem      saveSystem;
        public DialogueSystem  dialogueSystem;

        [Header("État du Jeu")]
        public GameState currentState = GameState.MainMenu;
        public int       currentAct   = 1;
        public bool      isLoading    = false;

        [Header("Paramètres Plateforme")]
        public Platform targetPlatform = Platform.PC;

        public delegate void OnGameStateChanged(GameState newState);
        public event OnGameStateChanged onStateChanged;

        // ── Cycle de vie ──────────────────────────────────────────────────────
        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            DetectPlatform();
        }

        void Start()
        {
            Application.targetFrameRate = GetTargetFPS();
            QualitySettings.vSyncCount  = (targetPlatform == Platform.Mobile) ? 0 : 1;
            saveSystem?.LoadGame();
        }

        // ── Détection plateforme ───────────────────────────────────────────────
        void DetectPlatform()
        {
#if UNITY_STANDALONE
            targetPlatform = Platform.PC;
#elif UNITY_PS5 || UNITY_XBOXONE || UNITY_GAMECORE
            targetPlatform = Platform.Console;
#elif UNITY_IOS || UNITY_ANDROID
            targetPlatform = Platform.Mobile;
#elif UNITY_WEBGL
            targetPlatform = Platform.WebGL;
#endif
        }

        int GetTargetFPS()
        {
            return targetPlatform switch
            {
                Platform.PC      => 0,    // Illimité — pilote GPU
                Platform.Console => 60,
                Platform.Mobile  => 60,
                Platform.WebGL   => 60,
                _                => 60
            };
        }

        // ── Gestion des états ─────────────────────────────────────────────────
        public void ChangeState(GameState newState)
        {
            currentState = newState;
            onStateChanged?.Invoke(newState);

            switch (newState)
            {
                case GameState.Playing:
                    Time.timeScale = 1f;
                    break;
                case GameState.Paused:
                    Time.timeScale = 0f;
                    break;
                case GameState.Cutscene:
                    Time.timeScale = 1f;
                    break;
                case GameState.GameOver:
                    StartCoroutine(GameOverSequence());
                    break;
            }
        }

        // ── Chargement de scène ───────────────────────────────────────────────
        public void LoadScene(string sceneName)
        {
            if (!isLoading)
                StartCoroutine(LoadSceneAsync(sceneName));
        }

        IEnumerator LoadSceneAsync(string sceneName)
        {
            isLoading = true;
            ChangeState(GameState.Loading);

            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;

            while (op.progress < 0.9f)
                yield return null;

            yield return new WaitForSeconds(0.5f);
            op.allowSceneActivation = true;
            isLoading = false;
            ChangeState(GameState.Playing);
        }

        // ── Progression narrative ─────────────────────────────────────────────
        public void AdvanceAct()
        {
            currentAct = Mathf.Clamp(currentAct + 1, 1, 4);
            questManager?.RefreshActQuests(currentAct);
            Debug.Log($"[GameManager] Passage à l'Acte {currentAct}");
        }

        IEnumerator GameOverSequence()
        {
            yield return new WaitForSeconds(2f);
            saveSystem?.SaveGame();
            LoadScene("GameOver");
        }

        void OnApplicationQuit()
        {
            saveSystem?.SaveGame();
        }
    }

    // ── Enums ─────────────────────────────────────────────────────────────────
    public enum GameState { MainMenu, Playing, Paused, Cutscene, Loading, GameOver }
    public enum Platform  { PC, Console, Mobile, WebGL }
}
