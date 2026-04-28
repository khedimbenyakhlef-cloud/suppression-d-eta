# SUPPRESSION DÉTA — Jeu Web Professionnel
## Nova Déta · 2047 · Cyberpunk Algérien

**Basé sur le projet Unity original par Khedim Benyakhlef**  
*Maison d'Édition Ahlem — ISBN 978-9969-521-15-3*

---

## 🎮 À propos

Suppression Déta est un jeu d'action cyberpunk top-down se déroulant dans la ville fictive de Nova Déta en Algérie, 2047. Incarnez Alex (combattant) et Lana (hackeuse), deux mercenaires qui affrontent la corporation totalitaire Déta Corp.

### Systèmes de jeu
- **4 types de gaz** : Inferno 🔥, Rire 😂, Inerte 💠, Fumée 🌫️
- **Système de réputation** : 4 factions (Citoyens, Forces de loi, Mercenaires, Déta Corp)
- **Arbre de compétences** : 9 compétences débloquables
- **Mini-jeu de hacking** : Interface Neural Hack pour Lana
- **Boss épique** : Liam — L'Adversaire Masqué (3 phases)
- **3 actes + 4 fins différentes**
- **IA ennemie FSM** : Patrouille → Alerte → Combat

---

## 🚀 Déploiement sur Render

### Méthode 1 — Render Dashboard (recommandée)

1. Pushez ce dossier sur GitHub :
```bash
git init
git add .
git commit -m "Suppression Déta v1.0.0"
git remote add origin https://github.com/VOTRE_USER/suppression-deta.git
git push -u origin main
```

2. Sur [render.com](https://render.com) :
   - **New** → **Web Service**
   - Connectez votre repo GitHub
   - Les paramètres sont auto-détectés via `render.yaml`
   - Cliquez **Deploy**

3. Votre jeu sera live sur : `https://suppression-deta.onrender.com`

### Méthode 2 — CLI Render
```bash
npm install -g @render-oss/cli
render deploy
```

### Configuration render.yaml
```yaml
services:
  - type: web
    name: suppression-deta
    env: node
    buildCommand: npm install
    startCommand: npm start
    healthCheckPath: /health
```

---

## 🖥️ Développement local

```bash
# Installer les dépendances
npm install

# Lancer le serveur
npm start

# Ouvrir dans le navigateur
open http://localhost:3000
```

---

## 🎯 Contrôles

| Touche | Action |
|--------|--------|
| `W A S D` / Flèches | Déplacement |
| `Clic gauche` | Tirer |
| `G` | Déployer le gaz sélectionné |
| `H` | Lancer le hack (Lana requis) |
| `E` | Interagir / Dialogue |
| `1` `2` `3` `4` | Changer le gaz |
| `Shift` | Sprint |
| `Esc` | Pause |

---

## 🗺️ Structure du projet

```
suppression-deta-game/
├── server.js          ← Serveur Express (Render)
├── package.json       ← Dépendances Node.js
├── render.yaml        ← Config déploiement Render
├── .gitignore
├── public/
│   └── index.html     ← Jeu complet (HTML5 Canvas + JS)
└── README.md
```

---

## 🔧 Technologies

- **Frontend** : HTML5 Canvas, CSS3, JavaScript vanilla
- **Backend** : Node.js + Express
- **Fonts** : Orbitron, Share Tech Mono, Rajdhani (Google Fonts)
- **Déploiement** : Render.com

---

## 📖 Lore

> Nova Déta, 2047. La mégacorporation Déta contrôle chaque litre d'eau, chaque byte d'information, chaque respiration de ses 15 millions d'habitants. Alex, mercenaire cybernétique aux augmentations illégales, et Lana, hackeuse fantôme du réseau souterrain NeuroNet, ont décidé que ça suffisait.
>
> Quatre types de gaz. Une ville entière à libérer. Un adversaire masqué à révéler.

---

## 🏆 Fins disponibles

| Fin | Condition |
|-----|-----------|
| **Fin A** — Héros du Peuple | Citoyens > 75 + Loi > 60 |
| **Fin B** — Révolution Mercenaire | Mercenaires > 75 + Déta < 25 |
| **Fin C** — Intégration Déta | Déta > 70 |
| **Fin D** — Équilibre Fragile | Toutes autres conditions |

---

## 📜 Crédits

| Rôle | |
|------|-|
| Scénario original | Khedim Benyakhlef |
| Éditeur | Maison d'Édition Ahlem |
| ISBN | 978-9969-521-15-3 |
| Adaptation web | Suppression Déta Studios |
| Moteur | HTML5 Canvas |

---

*© 2026 Suppression Déta Studios — Tous droits réservés*
