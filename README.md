# 🏯 Hào Khí Sử Việt
### A 2D Tower Defense Game Based on Vietnamese Folklore

> *"Defend the homeland. Command the heroes. Relive the spirit of a nation."*

[![Unity](https://img.shields.io/badge/Unity-2022.3%20LTS-black?logo=unity)](https://unity.com/)
[![Language](https://img.shields.io/badge/Language-C%23-purple?logo=csharp)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![Platform](https://img.shields.io/badge/Platform-PC-blue?logo=windows)](https://unity.com/)
[![License](https://img.shields.io/badge/License-Academic-orange)](LICENSE)
[![Status](https://img.shields.io/badge/Status-In%20Development-yellow)](LICENSE)

---

## 📖 Table of Contents

- [Overview](#-overview)
- [Gameplay](#-gameplay)
- [Architecture](#-architecture)
- [Implemented Systems](#-implemented-systems)
- [Hero Roster](#-hero-roster)
- [Tech Stack](#-tech-stack)
- [Project Structure](#-project-structure)
- [Design Patterns](#-design-patterns)
- [Getting Started](#-getting-started)
- [Current Progress](#-current-progress)
- [Development Roadmap](#-development-roadmap)
- [Thesis Context](#-thesis-context)
- [Author](#-author)

---

## 🌟 Overview

**Hào Khí Sử Việt** is a single-player, 2D lane-based tower defense game set in the world of Vietnamese history and mythology. Players strategically deploy heroic units drawn from Vietnamese folklore to defend their territory against waves of enemies, experiencing the spirit and courage of the Vietnamese people through interactive gameplay.

This project serves as a graduation thesis in Unity-based game engineering, validating software engineering principles in real-time 2D game development. Beyond delivering a playable prototype, the project demonstrates a **modular, data-driven, and scalable architecture** capable of handling complex gameplay systems within a single real-time game loop.

---

## 🎮 Gameplay

### Core Loop

```
Shuffle & Draft Heroes  ──►  Deploy Units  ──►  Defend Waves  ──►  Win / Lose
         ▲                                                              │
         └──────────────────── Retry / Progress ───────────────────────┘
```

### Key Mechanics

| Mechanic | Description |
|---|---|
| **Random Hero Drafting** | Shuffle a face-down deck and flip cards to draft your team lineup before each match |
| **Grid Placement** | Drag-and-drop ally units onto valid tiles on a lane-based battlefield |
| **Resource Economy** | Earn Gold from kills, spend on placements; resource-generating units provide passive income |
| **Wave Progression** | Face increasingly difficult enemy waves across 3 difficulty levels |
| **Combat Resolution** | Real-time lane-based combat with projectile handling and damage calculation |
| **Active Skills** | Trigger cooldown-based hero skills with distinct targeting modes and effects |
| **Ông Bụt Q&A System** | Answer Vietnamese historical questions to earn powerful divine skills mid-battle |
| **Status Effects** | Units and enemies interact through a layered status effect system (slow, burn, stun, pushback, freeze) |
| **Win/Loss Evaluation** | Stage ends based on Base HP depletion or full-wave clearance with star ratings |

---

## 🏗️ Architecture

The game is built on three interconnected architectural pillars:

```
┌──────────────────────────────────────────────────────────┐
│                     GAME ARCHITECTURE                    │
│                                                          │
│  ┌──────────────┐  ┌──────────────┐  ┌───────────────┐  │
│  │  Component-  │  │    Event-    │  │  Data-Driven  │  │
│  │    Based     │  │    Driven    │  │ Configuration │  │
│  │ Architecture │  │   Design     │  │(ScriptableObj)│  │
│  └──────┬───────┘  └──────┬───────┘  └───────┬───────┘  │
│         └─────────────────┼──────────────────┘          │
│                           ▼                              │
│              ┌────────────────────────┐                  │
│              │   Manager-Based Scene  │                  │
│              │     Coordination       │                  │
│              └────────────────────────┘                  │
└──────────────────────────────────────────────────────────┘
```

### Core Subsystems

```
GameManager
    │
    ├── GridManager              ← Tile validation, grid-based placement rules
    ├── EnemySpawner             ← Wave configs, spawn timing, enemy queuing
    ├── LevelStateManager        ← Preparing → Defending → Ending state flow
    ├── EconomyManager           ← Gold tracking, placement costs, kill rewards
    ├── CampaignManager          ← Level progression and unlocking
    ├── LineupManager            ← Drafted hero lineup for each match
    │
    ├── AI / FSM                 ← StateMachine + BaseState + StateFactory
    │   ├── EnemyIdleState       ← Spawn → wait for wave start
    │   ├── EnemyMoveState       ← Lane-locked horizontal movement
    │   ├── EnemyAttackState     ← Engage blocking troops
    │   ├── EnemyStunnedState    ← Stun/Freeze interrupt
    │   ├── EnemyKnockbackState  ← Pushback displacement
    │   └── EnemyDieState        ← Death sequence + pool release
    │
    ├── Gameplay Components
    │   ├── HealthComponent      ← HP management, destruction trigger
    │   ├── AttackComponent      ← Auto-attack, cooldown, projectile launch
    │   ├── MovementComponent    ← Enemy lane movement
    │   ├── ResourceGenerator    ← Passive Gold income from support units
    │   └── DamageCalculator     ← Physical / Magical / True damage pipeline
    │
    ├── Skill Systems
    │   ├── OngButSessionManager ← Historical Q&A session flow
    │   ├── OngButSkillExecutor  ← Divine skill activation from correct answers
    │   └── EggShowerManager     ← Special AoE skill mechanics
    │
    ├── ObjectPoolManager        ← Generic pooling for enemies, projectiles, VFX
    ├── GameEventBus             ← Typed event publish/subscribe system
    │
    └── UI Layer
        ├── DraftingUI           ← Card shuffle, flip, accept/decline flow
        ├── HeroSelector         ← Drag-and-drop hero deployment
        ├── OngButUIController   ← Q&A panels, skill HUD, result display
        ├── BaseHealthUI         ← Base HP bar
        ├── GoldDisplay          ← Economy HUD
        ├── LevelStateUI         ← Wave status and level state display
        └── GameOutcomeUI        ← Victory/Defeat screen
```

---

## ✨ Implemented Systems

### Core Gameplay
- 🏰 **Vietnamese Historical Theme** — Heroes, enemies, and skills rooted in Vietnamese folklore, mythology, and historical battles
- 🗺️ **Tile-Based Grid System** — `GridManager` + `TerrainGrid` / `TerrainCell` with tile type validation (Placeable, Path, Blocked, Base, Spawn)
- ⚔️ **Component-Based Combat** — Separate `HealthComponent`, `AttackComponent`, `MovementComponent` per unit; `DamageCalculator` for the damage pipeline
- 🤖 **FSM-Driven Enemy AI** — `StateMachine` / `BaseState` / `StateFactory` pattern with 6 enemy states (Idle, Move, Attack, Stunned, Knockback, Die)

### Hero Drafting & Deployment
- 🃏 **Random Hero Drafting** — Fisher-Yates shuffle, face-down card flip reveal, accept/decline flow with `ShuffleCutsceneUI`
- 🎯 **Drag-and-Drop Placement** — `HeroDragHandler` for deploying drafted heroes onto the grid during Preparing/Defending states

### Ông Bụt Educational System
- 📚 **Historical Q&A Mini-Game** — 10 Vietnamese history questions (`QuestionBankData` + `HistoricalQuestionData` ScriptableObjects)
- ✨ **Divine Skill Rewards** — Correct answers grant powerful skills: Geese Patrol, Golden Star Balm, Divine Crossbow Volley, Bạch Đằng Spikes, Absolute Freeze
- 🏛️ **Pagoda Interaction** — `OngButPagodaButtonUI` triggers the Q&A session; `OngButSkillHUDButtonUI` for skill activation

### Economy & Progression
- 💰 **Gold Economy** — `EconomyManager` handles starting gold, kill rewards, placement costs, and passive income via `ResourceGeneratorComponent`
- 📈 **Level Progression** — 3 difficulty levels (Easy, Medium, Hard) + test level managed by `CampaignManager`

### Technical Infrastructure
- ♻️ **Object Pooling** — `ObjectPoolManager` with configurable `PoolConfig` ScriptableObject for enemies, projectiles, and VFX
- 📡 **Event-Driven Architecture** — `GameEventBus` with typed events (`GameEvents.cs`) decoupling gameplay from UI
- 📊 **Data-Driven Configuration** — All unit stats, skills, levels, and questions defined as ScriptableObject assets

---

## 🦸 Hero Roster

| Hero | Type | Description |
|---|---|---|
| **Bộ Đội VN** | Soldier | Vietnamese infantry unit |
| **Sơn Tinh** | Ranged | Mountain God from Vietnamese mythology |
| **Thủy Tinh** | Ranged | Water God, rival of Sơn Tinh |
| **Thánh Gióng** | Melee | Legendary giant hero who defeated the Ân invaders |
| **Rồng Vàng** | Ranged | Golden Dragon, symbol of Vietnamese imperial power |
| **Rùa** | Tank | Sacred Turtle, guardian of Hoàn Kiếm Lake |
| **Tank 390** | Tank | Historic T-54 tank from the Reunification campaign |
| **Nữ Chiến Binh** | Support | Vietnamese woman warrior |

---

## 🛠️ Tech Stack

| Category | Technology |
|---|---|
| **Engine** | Unity 2022.3 LTS (URP 2D) |
| **Language** | C# |
| **Rendering** | Universal Render Pipeline (2D Renderer) |
| **Version Control** | Git / GitHub |
| **Unity Subsystems** | Tilemap, Physics2D, Animator, Canvas UI, TextMeshPro, AudioSource/AudioMixer |
| **Data Management** | ScriptableObjects (units, enemies, skills, waves, questions), JSON persistence |
| **Target Platform** | PC (Windows 10/11, 64-bit) |

---

## 📁 Project Structure

```
Assets/
├── Animation/                  # Hero and UI animations
├── Audios/                     # BGM and SFX audio assets
│
├── Data/                       # ScriptableObject configuration assets
│   ├── Levels/                 #   Level_01_Easy, Level_02_Medium, Level_03_Hard
│   ├── Units/                  #   Ally unit stats (8 heroes + enemy types)
│   ├── Skills/                 #   Skill data (EggShower, Tsunami, etc.)
│   └── OngBut/                 #   Ông Bụt Q&A system
│       ├── OngButConfig.asset  #     Session configuration
│       ├── Questions/          #     10 historical questions + QuestionBank
│       └── Skills/             #     5 divine reward skills
│
├── Resources/Data/
│   └── HeroCards/              # HeroCardData for drafting (8 cards)
│
├── Scripts/
│   ├── AI/                     # FSM framework + enemy state implementations
│   │   ├── FSM/                #   StateMachine, BaseState, StateFactory, AIComponent
│   │   └── States/Enemy/       #   Idle, Move, Attack, Stunned, Knockback, Die
│   ├── Core/                   # Game managers and infrastructure
│   │   ├── GameManager.cs
│   │   ├── EconomyManager.cs
│   │   ├── OngButSessionManager.cs
│   │   ├── OngButSkillExecutor.cs
│   │   ├── Level/              #   LevelStateManager, CampaignManager, LineupManager
│   │   ├── Events/             #   GameEventBus, GameEvents
│   │   └── Pooling/            #   ObjectPoolManager, PoolConfig, PooledObject
│   ├── Data/                   # ScriptableObject class definitions
│   │   ├── BaseUnitData.cs, DefenderUnitData.cs, EnemyUnitData.cs
│   │   ├── ActiveSkillData.cs, OngButSkillData.cs, StatusEffectData.cs
│   │   ├── HeroCardData.cs, LevelConfig.cs
│   │   └── QuestionBankData.cs, HistoricalQuestionData.cs
│   ├── Gameplay/               # Runtime gameplay components
│   │   ├── Components/         #   HealthComponent, AttackComponent, MovementComponent
│   │   ├── GridManager.cs, TileData.cs
│   │   ├── DamageCalculator.cs, BaseHealthManager.cs
│   │   ├── Projectile.cs, EnemyProjectile.cs
│   │   └── EggShowerManager.cs, LaneSweeper.cs
│   ├── Enemies/                # Enemy.cs, EnemySpawner.cs
│   ├── Heroes/                 # Hero.cs, Shooter.cs
│   ├── TerrainFloors/          # TerrainGrid.cs, TerrainCell.cs
│   └── UI/                     # All UI controllers (17 scripts)
│       ├── DraftingUI.cs, DraftCardSlot.cs, ShuffleCutsceneUI.cs
│       ├── HeroSelector.cs, HeroSlotUI.cs, HeroDragHandler.cs
│       ├── OngButUIController.cs, OngButQnAPanelUI.cs, ...
│       ├── BaseHealthUI.cs, GoldDisplay.cs, SkillButtonUI.cs
│       └── GameOutcomeUI.cs, LevelIntroUI.cs, LevelStateUI.cs
│
├── Prefabs/
│   ├── Heroes/                 # 8 hero unit prefabs
│   ├── Enemies/                # Enemy unit prefabs
│   ├── Projectiles/            # 8 projectile types
│   └── Effects/                # Card slots, preview portraits, skill slots
│
├── Sprites/                    # 2D art assets
├── Scenes/                     # Game scenes
└── Settings/                   # URP settings and scene templates
```

---

## 🧩 Design Patterns

| Pattern | Application in Project |
|---|---|
| **Singleton** | `GameManager`, `EconomyManager`, `ObjectPoolManager` — global access with controlled instantiation |
| **Observer / Event Bus** | `GameEventBus` with typed `GameEvents` — decoupled communication between gameplay and UI layers |
| **Factory Method** | `StateFactory` — centralized FSM state creation; unit instantiation from ScriptableObject data |
| **Object Pooling** | `ObjectPoolManager` with `PoolConfig` SO — enemies, projectiles, and VFX recycled at runtime |
| **Finite State Machine** | `StateMachine` + `BaseState` — enemy AI states (Idle → Move → Attack → Stunned → Knockback → Die) |
| **Component-Based Entity** | `HealthComponent`, `AttackComponent`, `MovementComponent` — single-responsibility MonoBehaviours per unit |
| **ScriptableObject Architecture** | All gameplay data (units, enemies, skills, levels, questions) stored as editable Unity assets |
| **Data-Driven Design** | Zero hard-coded constants — all balance values in ScriptableObjects |

---

## 🚀 Getting Started

### Prerequisites

- Unity **2022.3 LTS** (download via [Unity Hub](https://unity.com/download))
- Git

### Installation

```bash
# 1. Clone the repository
git clone <repository-url>

# 2. Open Unity Hub → Add → select the cloned project folder

# 3. Open the project in Unity 2022.3 LTS

# 4. Open the main scene
#    Assets/Settings/Scenes/URP2DSceneTemplate.unity

# 5. Press Play
```

---

## 📊 Current Progress

### Completed (78 scripts, 39 data assets)

| System | Status | Details |
|---|---|---|
| **AI / FSM Framework** | ✅ Done | StateMachine, BaseState, StateFactory, AIComponent + 6 enemy states |
| **Game Management** | ✅ Done | GameManager, LevelStateManager, EconomyManager, CampaignManager |
| **Grid & Placement** | ✅ Done | GridManager, TerrainGrid/TerrainCell, tile validation |
| **Combat Components** | ✅ Done | HealthComponent, AttackComponent, DamageCalculator, projectiles |
| **Hero Drafting** | ✅ Done | Shuffle cutscene, card flip, accept/decline, lineup management |
| **Hero Deployment** | ✅ Done | Drag-and-drop placement with HeroDragHandler |
| **Ông Bụt Q&A** | ✅ Done | 10 questions, skill rewards, full UI flow with history tracking |
| **Object Pooling** | ✅ Done | ObjectPoolManager with configurable PoolConfig |
| **Event System** | ✅ Done | GameEventBus with typed GameEvents |
| **UI System** | ✅ Done | 17 UI scripts covering HUD, drafting, Ông Bụt, outcomes |
| **Data Assets** | ✅ Done | 8 hero units, enemy types, 3 levels, 5 divine skills, 10 questions |
| **Level Content** | ✅ Done | Easy, Medium, Hard difficulty levels |

### In Progress / Planned

| System | Status | Notes |
|---|---|---|
| **Audio Integration** | 🔲 Planned | AudioManager with AudioMixer routing per `08-audio-system.md` |
| **Save/Load System** | 🔲 Planned | JSON persistence for progress and match history |
| **Settings System** | 🔲 Planned | Volume, resolution, fullscreen settings |
| **Pause System** | 🔲 Planned | PauseManager with Time.timeScale control |
| **Unit Tests** | 🔲 Planned | Edit Mode tests for combat, grid, and FSM systems |
| **Match History UI** | 🔲 Planned | HistoryPanel with level filtering and detail popups |

---

## 🗺️ Development Roadmap

| Phase | Weeks | Milestone | Status |
|---|---|---|---|
| **Phase 1** | 1 – 2 | Literature review, topic refinement, requirements definition | ✅ Complete |
| **Phase 2** | 3 – 5 | Gameplay rules design, content mapping, system architecture | ✅ Complete |
| **Phase 3** | 6 – 8 | Core systems — grid, placement, wave spawning, combat, FSM AI | ✅ Complete |
| **Phase 4** | 9 – 11 | Hero drafting, Ông Bụt Q&A, skill system, UI/UX flow | ✅ Complete |
| **Phase 5** | 12 – 14 | Audio, save/load, settings, testing, balancing, optimization | 🔄 In Progress |
| **Phase 6** | 15 – 16 | Thesis report completion, prototype finalization, defense prep | 🔲 Planned |

---

## 📚 Thesis Context

This project is submitted as a graduation thesis at the **University of Science and Technology — The University of Da Nang**.

| Field | Detail |
|---|---|
| **Thesis Title** | Developing 'Hào Khí Sử Việt': A 2D Tower Defense Game based on Vietnamese Folklore using Unity |
| **Student** | Nguyễn Hoàng Thanh Trâm — ID: 22020005 — Class: 22CSE |
| **Supervisor** | Trần Thế Vũ |
| **Submission Date** | March 19, 2026 |

### Thesis Chapter Structure

| Chapter | Title |
|---|---|
| 1 | Introduction |
| 2 | Theoretical Background and Related Works |
| 3 | Requirement Analysis and Game/System Design |
| 4 | Technical Implementation |
| 5 | Testing, Balancing, and Performance Evaluation |
| 6 | Conclusion and Future Development |

### Out of Scope

The following are explicitly **not** part of this thesis:

- Online multiplayer
- Procedural content generation
- Live-service features
- Commercial publishing pipeline
- Full production-scale content volume

---

## 👤 Author

**Nguyễn Hoàng Thanh Trâm**
Student ID: 22020005 | Class: 22CSE
University of Science and Technology — The University of Da Nang

**Supervisor:** Trần Thế Vũ

---

<div align="center">

*Built with ❤️ for Vietnamese culture and game engineering*

</div>
