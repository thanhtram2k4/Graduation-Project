# Project Folder Structure — Hao Khi Su Viet

## `Assets/` — Main Unity folder

### `Scripts/` — C# source code
| Folder | Contents |
|---|---|
| **Core/** | Game management (`GameManager.cs`) |
| **Data/** | ScriptableObject definitions — unit, skill, level, status effect, enums (`BaseUnitData`, `EnemyUnitData`, `DefenderUnitData`, `HeroCardData`, `ActiveSkillData`, `StatusEffectData`, `LevelConfig`, `GameEnums`) |
| **Gameplay/** | Gameplay logic — grid, lane, projectile, tile (`GridManager`, `LaneSweeper`, `Projectile`, `TileData`) |
| **Heroes/** | Hero/troop logic (`Hero.cs`, `Shooter.cs`) |
| **Enemies/** | Enemy logic and spawning (`Enemy.cs`, `EnemySpawner.cs`) |
| **UI/** | User interface (`GoldDisplay`, `HeroSelector`) |
| **TerrainFloors/** | Terrain/floor logic |

### `Data/` — ScriptableObject assets (config data)
| Folder | Contents |
|---|---|
| **Units/** | Unit data assets (ally & enemy) |
| **Skills/** | Skill data assets |
| **HeroCards/** | Hero card assets for drafting system |

### `Prefabs/` — Prefab GameObjects
| Folder | Contents |
|---|---|
| **Heroes/** | Hero/troop prefabs |
| **Enemies/** | Enemy prefabs |
| **Projectiles/** | Projectile prefabs |
| **Effects/** | VFX prefabs |

### `Sprites/` — 2D artwork
| Folder | Contents |
|---|---|
| **Heroes/** | Hero sprites |
| **Enemies/** | Enemy sprites |
| **Backgrounds/** | Background images |
| **Projectiles/** | Projectile sprites |
| **UI/** | UI sprites |

### Other folders
- **`Animation/`** — Animation clips & controllers
- **`Audios/`** — Audio files (BGM, SFX)
- **`Scenes/`** — Unity scenes (`SampleScene.unity`)
- **`Settings/`** — URP render settings

## Outside `Assets/`
- **`ProjectSettings/`** — Unity project config
- **`Packages/`** — Package dependencies
- **`implementation_plan.md`**, **`walkthrough.md`** — Planning & guide documents
