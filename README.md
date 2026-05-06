# 🏯 Hào Khí Sử Việt
### A 2D Tower Defense Game Based on Vietnamese Folklore

> *"Defend the homeland. Command the heroes. Relive the spirit of a nation."*

[![Unity](https://img.shields.io/badge/Unity-2022.3%20LTS-black?logo=unity)](https://unity.com/)
[![Language](https://img.shields.io/badge/Language-C%23-purple?logo=csharp)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![Platform](https://img.shields.io/badge/Platform-PC-blue?logo=windows)](https://unity.com/)
[![License](https://img.shields.io/badge/License-Academic-orange)](LICENSE)
[![Status](https://img.shields.io/badge/Status-Completed-green)](LICENSE)

---

## 📖 Table of Contents

- [Overview](#-overview)
- [Gameplay](#-gameplay)
- [Architecture](#-architecture)
- [Features](#-features)
- [Tech Stack](#-tech-stack)
- [Project Structure](#-project-structure)
- [Design Patterns](#-design-patterns)
- [Getting Started](#-getting-started)
- [Development Roadmap](#-development-roadmap)
- [Testing & Performance](#-testing--performance)
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
Select Deck  ──►  Deploy Units  ──►  Defend Waves  ──►  Win / Lose
     ▲                                                        │
     └────────────── Retry / Progress ──────────────────────┘
```

### Key Mechanics

| Mechanic | Description |
|---|---|
| **Grid Placement** | Drag-and-drop ally units onto valid tiles on a lane-based battlefield |
| **Resource Economy** | Collect and manage in-game resources to afford unit deployments |
| **Wave Progression** | Face increasingly difficult enemy waves defined by editable wave configs |
| **Combat Resolution** | Real-time lane-based combat with projectile handling and damage calculation |
| **Active Skills** | Trigger cooldown-based skills with distinct effects (slow, burn, stun, push-back, confusion) |
| **Status Effects** | Units and enemies interact through a layered status effect system |
| **Win/Loss Evaluation** | Stage ends based on enemy breakthrough conditions or full-wave clearance |
| **Save & Load** | Player progress, deck selection, and settings are persisted between sessions |

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
GameStateManager
    │
    ├── GridManager          ← Tile validation, placement rules
    ├── WaveSpawner          ← Wave configs, spawn timing, enemy queuing
    ├── UnitController       ← Ally FSM, attack, cooldown, skill triggers
    ├── EnemyAI              ← Enemy FSM, lane movement, targeting
    ├── CombatSystem         ← Damage calc, hit detection, projectile lifecycle
    ├── StatusEffectSystem   ← Slow, burn, stun, push-back, confusion
    ├── ProjectilePool       ← Object Pooling for runtime performance
    ├── UIManager            ← HUD, drag-and-drop, resource display, menus
    ├── AudioManager         ← SFX / BGM via AudioSource & AudioMixer
    └── PersistenceLayer     ← PlayerPrefs / JSON save-load
```

---

## ✨ Features

- 🏰 **Vietnamese Historical Theme** — Units, enemies, and environments inspired by Vietnamese folklore and historical events
- 🗺️ **Lane-Based Battlefield** — Tilemap-driven grid with tile type constraints for strategic placement
- 🃏 **Deck Selection** — Pre-battle unit deck building for strategic variety
- ⚔️ **Diverse Unit Roles** — Multiple ally archetypes with distinct attack ranges, speeds, and abilities
- 👹 **Enemy Archetypes** — Varied enemy types with different movement speeds, HP pools, and behaviors
- 💥 **Active Skill System** — Cooldown-managed skills with visual and gameplay impact
- 🌊 **Configurable Wave System** — Wave definitions editable via ScriptableObjects — no code changes required
- 💾 **Persistent Save System** — Progress and settings saved between play sessions
- 🔊 **Audio Feedback** — Contextual SFX and BGM via Unity AudioMixer
- 📊 **Scoring System** — Performance tracked and displayed at stage completion
- ⚙️ **Data-Driven Balancing** — All unit/enemy stats and wave parameters configurable through Unity assets

---

## 🛠️ Tech Stack

| Category | Technology |
|---|---|
| **Engine** | Unity 2022.3 LTS |
| **Language** | C# |
| **IDE** | Visual Studio Code |
| **Version Control** | Git / GitHub |
| **Unity Subsystems** | Tilemap, Physics2D, Animator, Canvas UI, AudioSource/AudioMixer, Scene Management |
| **Data Management** | ScriptableObjects (units, enemies, skills, waves), PlayerPrefs / JSON |
| **Profiling & Testing** | Unity Profiler, Unity Test Runner (NUnit-based) |
| **Target Platform** | PC (mid-range hardware); architecture supports future mobile deployment |

---

## 📁 Project Structure

```
Assets/
├── _Project/
│   ├── ScriptableObjects/
│   │   ├── Units/              # UnitData assets (stats, visuals, skills)
│   │   ├── Enemies/            # EnemyData assets
│   │   ├── Skills/             # SkillData assets
│   │   └── Waves/              # WaveConfig assets per stage
│   │
│   ├── Scripts/
│   │   ├── Core/
│   │   │   ├── GameStateManager.cs
│   │   │   ├── GridManager.cs
│   │   │   └── WaveSpawner.cs
│   │   ├── Units/
│   │   │   ├── UnitController.cs
│   │   │   ├── UnitStateMachine.cs
│   │   │   └── UnitData.cs
│   │   ├── Enemies/
│   │   │   ├── EnemyAI.cs
│   │   │   ├── EnemyStateMachine.cs
│   │   │   └── EnemyData.cs
│   │   ├── Combat/
│   │   │   ├── CombatSystem.cs
│   │   │   ├── ProjectileController.cs
│   │   │   └── StatusEffectHandler.cs
│   │   ├── UI/
│   │   │   ├── UIManager.cs
│   │   │   ├── HUDController.cs
│   │   │   └── DragDropHandler.cs
│   │   ├── Audio/
│   │   │   └── AudioManager.cs
│   │   └── Persistence/
│   │       └── SaveLoadManager.cs
│   │
│   ├── Prefabs/
│   │   ├── Units/
│   │   ├── Enemies/
│   │   ├── Projectiles/
│   │   └── VFX/
│   │
│   ├── Scenes/
│   │   ├── MainMenu.unity
│   │   ├── DeckSelection.unity
│   │   └── Stage_01.unity
│   │
│   ├── Art/
│   │   ├── Sprites/
│   │   ├── Tilemaps/
│   │   └── Animations/
│   │
│   └── Audio/
│       ├── BGM/
│       └── SFX/
│
└── Tests/
    ├── EditMode/
    └── PlayMode/
```

---

## 🧩 Design Patterns

| Pattern | Application in Project |
|---|---|
| **Singleton** | `GameStateManager`, `AudioManager`, `UIManager` — global access with controlled instantiation |
| **Observer** (C# Actions/Events) | Decoupled communication between subsystems (e.g., enemy death → resource drop → UI update) |
| **Factory Method** | Unit and enemy instantiation from ScriptableObject data at runtime |
| **Object Pooling** | Projectiles and VFX recycled at runtime to minimize GC allocations |
| **Finite State Machine** | Unit and enemy behavior states (Idle → Move → Attack → Die) |
| **ScriptableObject Architecture** | All gameplay content (units, enemies, waves, skills) stored as editable Unity assets |

---

## 🚀 Getting Started

### Prerequisites

- Unity **2022.3 LTS** (download via [Unity Hub](https://unity.com/download))
- Git

### Installation

```bash
# 1. Open the project folder in Unity Hub
Unity Hub → Add → select the project folder

# 2. Open Unity Hub → Add → select the cloned project folder

# 3. Open the project in Unity 2022.3 LTS

# 4. Open the MainMenu scene
#    Assets/_Project/Scenes/MainMenu.unity

# 5. Press Play
```

### Running Tests

```
Unity Editor → Window → General → Test Runner
  ├── EditMode Tests  → Run All
  └── PlayMode Tests  → Run All
```

### Profiling

```
Unity Editor → Window → Analysis → Profiler
  → Enter Play Mode to capture runtime frame data
```

---

## 🗺️ Development Roadmap

| Phase | Weeks | Milestone |
|---|---|---|
| **Phase 1** | 1 – 2 | Literature review, topic refinement, functional & technical requirements definition |
| **Phase 2** | 3 – 5 | Gameplay rules design, content mapping, system architecture & data model |
| **Phase 3** | 6 – 8 | Core systems — grid, placement, wave spawning, combat, game-state control |
| **Phase 4** | 9 – 11 | Content integration, UI/UX flow, audio feedback, save/load, balancing tools |
| **Phase 5** | 12 – 14 | Testing, debugging, gameplay balancing, profiling & optimization |
| **Phase 6** | 15 – 16 | Thesis report completion, prototype finalization, defense preparation |

---

## 🧪 Testing & Performance

### Testing Strategy

- **Functional Testing** — Each subsystem validated against specified input/output behavior
- **Integration Testing** — Full gameplay loop tested across subsystem boundaries
- **Playtesting** — Gameplay balance evaluated through iterative play sessions
- **Performance Profiling** — Frame rate stability, runtime memory allocation, and GC pressure measured via Unity Profiler

### Performance Targets

| Metric | Target |
|---|---|
| Frame Rate | Stable on mid-range PC hardware |
| GC Allocations | Minimized via Object Pooling for projectiles and VFX |
| Collision Handling | Optimized Physics2D layer configuration |
| Module Coupling | Clean boundaries enforced between all subsystems |

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
