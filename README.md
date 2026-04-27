# SUPPRESSION DÉTA — Unity Project
## Code Source Complet v1.0
**Basé sur le scénario original de Khedim Benyakhlef**
**Maison d'Édition Ahlem — ISBN 978-9969-521-15-3**

---

## PRÉREQUIS

| Outil | Version |
|---|---|
| Unity | 6 LTS (6000.0.x) |
| Universal Render Pipeline (URP) | Inclus Unity 6 |
| Input System | 1.7+ (Package Manager) |
| TextMeshPro | 3.0+ (Package Manager) |
| Cinemachine | 3.0+ (Package Manager) |
| Newtonsoft JSON | 3.2+ (Package Manager) |
| NavMesh Components | Inclus Unity 6 |
| IDE | Visual Studio 2022 / Rider |

---

## STRUCTURE DU PROJET

```
SuppressionDeta/
├── Assets/
│   ├── Scripts/
│   │   ├── Gameplay/
│   │   │   ├── GameManager.cs          ← Singleton central
│   │   │   ├── ReputationSystem.cs     ← 4 factions, fins multiples
│   │   │   ├── SaveSystem.cs           ← Multi-plateforme
│   │   │   └── CombatHackSystem.cs     ← Combat + Neural Hack
│   │   ├── Player/
│   │   │   └── PlayerController.cs     ← Alex + Lana (Input System)
│   │   ├── Enemy/
│   │   │   └── EnemyAI.cs              ← FSM + NavMesh
│   │   ├── Gas/
│   │   │   └── GasSystem.cs            ← Mécanique signature
│   │   ├── Characters/
│   │   │   └── Characters.cs           ← Liam, Dr. Frost, CharManager
│   │   ├── World/
│   │   │   └── WorldSystems.cs         ← Jour/Nuit, Météo, Districts
│   │   └── UI/
│   │       └── UIManagers.cs           ← HUD, Quêtes, Dialogues, SkillTree
│   ├── Scenes/
│   │   ├── MainMenu.unity
│   │   ├── Act1_LeContrat.unity
│   │   ├── Act2_ChaosGazRire.unity
│   │   ├── Act3_AdversaireMasque.unity
│   │   └── Epilogue.unity
│   ├── ScriptableObjects/
│   │   ├── Weapons/                    ← WeaponData assets
│   │   └── Quests/                     ← QuestData assets
│   ├── Prefabs/
│   │   ├── Player/
│   │   ├── Enemies/
│   │   ├── Gas/
│   │   └── UI/
│   └── Resources/
├── ProjectSettings/
│   └── ProjectSettings.asset
└── README.md
```

---

## INSTALLATION

### 1. Créer le projet Unity
```
1. Ouvrir Unity Hub
2. New Project → "3D (URP)" template
3. Nommer : "SuppressionDeta"
4. Unity version : 6000.0.x LTS
```

### 2. Copier les scripts
```
Copier tous les fichiers .cs dans :
Assets/Scripts/ (en respectant la structure ci-dessus)
```

### 3. Installer les packages (Package Manager)
```
Window → Package Manager → Add by name :
- com.unity.inputsystem
- com.unity.textmeshpro
- com.unity.cinemachine
- com.unity.ai.navigation
- com.unity.render-pipelines.universal
```

### 4. Configurer Input System
```
Edit → Project Settings → Player
→ Active Input Handling : "Both" (Input System + Legacy)

Créer InputActions Asset :
Assets → Create → Input Actions → "PlayerInputActions"
Actions requises :
- Move (Vector2)
- Look (Vector2)
- Jump (Button)
- Sprint (Button)
- Crouch (Button)
- Attack (Button)
- Special (Button)
- Interact (Button)
```

### 5. Configurer NavMesh
```
Window → AI → Navigation
Bake le NavMesh sur chaque scène
Layer "Walkable" = sol, trottoirs, toits accessibles
Layer "Not Walkable" = murs, obstacles, zones de gaz
```

### 6. Tags et Layers requis
```
Tags : Player, Enemy, Head, Hackable, GasZone, Cover
Layers : Enemy, Player, Obstacle, Hackable, Cover
```

---

## CONFIGURATION PAR PLATEFORME

### PC (Steam)
```csharp
// Dans GameManager.cs — automatique via DetectPlatform()
#if UNITY_STANDALONE
    targetPlatform = Platform.PC;
    Application.targetFrameRate = 0; // Illimité
#endif

// Build Settings → PC, Mac & Linux Standalone
// Player Settings → API : IL2CPP, .NET Standard 2.1
```

### PlayStation 5 / Xbox Series
```csharp
#if UNITY_PS5 || UNITY_GAMECORE
    targetPlatform = Platform.Console;
    Application.targetFrameRate = 60; // ou 120 mode performance
#endif

// Nécessite DevKit Sony/Microsoft
// Player Settings → Haptics SDK pour DualSense
```

### iOS & Android
```csharp
#if UNITY_IOS || UNITY_ANDROID
    targetPlatform = Platform.Mobile;
    Application.targetFrameRate = 60;
    QualitySettings.vSyncCount = 0;
#endif

// Android : Player Settings → Vulkan (API Graphics)
// iOS     : Player Settings → Metal
// Mobile  : Réduire qualité ombres, LOD agressif
```

### WebGL
```csharp
#if UNITY_WEBGL
    targetPlatform = Platform.WebGL;
    // Pas de FileStream → PlayerPrefs pour save
    // Pas de threading → WebWorkers limités
#endif

// Build Settings → WebGL
// Player Settings → Compression : Brotli
// Publishing Settings → Linker : IL2CPP
```

---

## GUIDE DES SYSTÈMES CLÉS

### GasSystem — Déployer un gaz
```csharp
// Depuis n'importe quel script :
GasSystem.Instance.DeployGas(GasType.Rire, transform.position, 20f, spreads: true);
GasSystem.Instance.NeutralizeGas(GasType.Inferno);

// Vérifier si le joueur est dans un gaz :
if (GasSystem.Instance.IsInGasZone(player.position, out GasType type))
    Debug.Log($"Dans zone {type}");
```

### ReputationSystem — Modifier la réputation
```csharp
// Actions prédéfinies :
ReputationSystem.Instance.OnCivilianHelped();
ReputationSystem.Instance.OnDetaSabotaged();
ReputationSystem.Instance.OnLiamSpared();

// Manuel :
ReputationSystem.Instance.AddReputation(FactionType.Citizens, 10f);

// Calculer la fin disponible :
EndingType fin = ReputationSystem.Instance.GetAvailableEnding();
```

### QuestManager — Gérer les quêtes
```csharp
// Créer un QuestData dans Inspector puis :
QuestManager.Instance.StartQuest("act1_infiltration");
QuestManager.Instance.CompleteObjective("act1_infiltration", "steal_plans");
```

### HackSystem — Pirater un objet
```csharp
// Sur l'objet hackable, ajouter HackableObject :
var hackable = door.GetComponent<HackableObject>();
hackable.hackType       = HackableType.Door;
hackable.hackDifficulty = 2;

// Lana initie le piratage :
HackSystem.Instance.BeginHack(hackable);
```

### SkillTree — Débloquer une compétence
```csharp
SkillTree.Instance.AddSkillPoints(1);
bool success = SkillTree.Instance.UnlockSkill("force_04"); // Analyse tactique

if (SkillTree.Instance.HasSkill("tech_06")) // Ghost Mode
    EnableGhostMode();
```

### SaveSystem — Sauvegarder / Charger
```csharp
SaveSystem.Instance.SaveGame(slot: 0);  // Slot 0 = autosave
SaveSystem.Instance.LoadGame(slot: 1);  // Slot 1 = sauvegarde manuelle
bool exists = SaveSystem.Instance.HasSave(slot: 0);
```

---

## SCÈNES À CRÉER

| Scène | Contenu principal | GameObjects requis |
|---|---|---|
| MainMenu | UI titre, Continue/Nouvelle partie | GameManager, SaveSystem, AudioManager |
| Act1_LeContrat | Repaire mercenaires, Installation Déta | PlayerController (Alex), EnemyAI ×10, SecurityCamera ×4 |
| Act2_ChaosGazRire | Nova Déta open world | GasSystem, WorldManager, EnemyAI ×30, Citizens ×50 |
| Act3_AdversaireMasque | Entrepôt final, boss Liam | LiamCharacter, DrFrostCharacter |
| Epilogue | Nova Déta reconstruite | Cutscene Director, Timeline |

---

## OPTIMISATION MOBILE

```csharp
// Dans Player Settings → iOS / Android :
// 1. Texture compression : ASTC (iOS) / ETC2 (Android)
// 2. Occlusion Culling : activé
// 3. LOD Group : 3 niveaux (High / Mid / Low)
// 4. Max Light Count : 4 par objet
// 5. Shadow Distance : 30m (vs 100m PC)
// 6. Particle Max : 500 (vs 2000 PC)
```

---

## BUILDS

### PC Steam
```
File → Build Settings → PC, Mac & Linux
Architecture : x86_64
Backend : IL2CPP
Strip Engine Code : ✅
```

### Android APK/AAB
```
File → Build Settings → Android
Target API : 34 (Android 14)
Scripting Backend : IL2CPP
Target Architecture : ARM64
Build → Build AAB (Google Play)
```

### iOS IPA
```
File → Build Settings → iOS
Build → Ouvrir dans Xcode → Archive → Distribute
```

### WebGL
```
File → Build Settings → WebGL
Compression : Brotli
Memory Size : 512 MB
```

---

## CRÉDITS

| Rôle | Nom |
|---|---|
| Scénario original | Khedim Benyakhlef |
| Éditeur | Maison d'Édition Ahlem |
| Architecture code | Suppression Déta Studios |
| Moteur | Unity 6 LTS |
| ISBN | 978-9969-521-15-3 |

---

*© 2026 Suppression Déta Studios — Tous droits réservés*
*Code source confidentiel — Ne pas distribuer sans autorisation*
