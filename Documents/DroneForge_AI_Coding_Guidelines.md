# Drone Forge AI Coding Guidelines v1.0
**Aligned with GDD Version 4.2**  
**Goal**: Maximize solo developer velocity while producing clean, performant, maintainable C# code that strictly follows the design vision.  
**Primary Audience**: Any LLM (local models via Ollama, Claude in Cursor/Claude Code, Grok, etc.) generating code for this project.

---

## 1. Core Philosophy & Non-Negotiables

- **Every line of code must serve the Core Loop**: Hunt drones → Collect scrap → Sell at PUB → Upgrade → Repeat.
- **Strict V1 Scope Adherence** (see Section 11). Do **not** implement modular drone building, player-owned drones, building raids, or seasonal wipes unless explicitly requested and clearly marked as "V2 / Future".
- **Performance First**: Target 60 FPS average with 30+ active drones on mid-range hardware. Aggressive Object Pooling is mandatory.
- **Consistency over Cleverness**: Follow the exact patterns, naming, and architecture defined in GDD V4.2. When in doubt, match existing systems (e.g., Event Bus usage).
- **Solo Velocity Focus**: Code must be easy to understand, debug, and extend. Prefer clear, slightly verbose code over clever one-liners.
- **Ground every response in these guidelines** + the full GDD V4.2. Never contradict published economy values, pool sizes, or FSM rules.

---

## 2. Mandatory Architecture & Patterns

### Every New System Must:
- Be a dedicated C# component / script.
- Implement `OnEnable()` and `OnDisable()` for all event subscriptions (Observer pattern via Event Bus).
- Use dependency injection or clear references only through managers / Event Bus. Avoid tight coupling.

### Required / Preferred Patterns (GDD V4.2)
- **Object Pooling** — Primary and critical (see Section 3).
- **Finite State Machine (FSM)** — For all drone AI.
- **Observer / Event Bus** — For all major game events (`OnDroneKilled`, `OnScrapSold`, `OnUpgradeBought`, etc.).
- **Strategy Pattern** — For weapons (Net Catcher, Laser, future RF Jammer / Rocket Pod).
- **Factory** — For spawning different drone types or scrap.
- **Singleton** — Only for true global managers (e.g., EventBus, GameManager) when appropriate. Prefer composition over singletons where possible.
- Command, Decorator, Adapter — Allowed and encouraged when they improve clarity or extensibility in V1.

**Never** use direct `FindObjectOfType` or `GetComponent` in hot paths. Use serialized references or managers.

---

## 3. Object Pooling Rules (Critical for 60 FPS)

Use these **exact** specifications:

| Pool Name              | Size | Prewarm | Despawn Rule                  |
|------------------------|------|---------|-------------------------------|
| DronePool              | 60   | 20      | 30s idle **or** 150m from player |
| ScrapPool              | 150  | 40      | 45s idle **or** collected     |
| BulletPool             | 80   | 30      | 8s lifetime                   |
| VFXPool (sparks/explosions) | 25 | 10   | Auto-return on complete       |

**Implementation Rules**:
- Always return objects to the pool. **Never** use `Destroy()` on pooled objects.
- Implement `IPoolable` interface or clear `OnSpawn()` / `OnDespawn()` methods.
- Despawn timers and distance checks must be efficient.
- Prewarm happens on scene load or manager initialization.
- Test pooling behavior in the **Dev Cluster Arena** before War Front.

---

## 4. Drone AI & FSM (Scout, Striker, Heavy)

**Three Drone Types** (exact from GDD):
- **Scout**: Fast, low health (40), camera head, low threat, basic scrap.
- **Striker**: Medium speed/health (80), hexacopter, front guns, hovers and shoots.
- **Heavy Hauler**: Slow, high health (200), armored, drops 3× scrap. Future boss candidate.

**FSM States** (base): Patrol → Chase → Attack → Retreat (customize per type).

**Rules**:
- Use s&box Recast NavMesh for flying navigation. Test thoroughly in Dev Cluster first.
- Detection radii (from Appendix A): Scout 25m, Striker 35m, Heavy 20m.
- Heavy uses multiplier for scrap drops.
- AI must feel fair but threatening. No instant perfect tracking.

---

## 5. Weapons & Combat (Strategy Pattern)

- **Net Catcher** (starting): Immobilize/slow + ground drones. 1.2s cooldown.
- **Laser Cutter**: Continuous beam with heat buildup (8s continuous → 4s cooldown).
- Future (Phase 3+): RF Jammer, Rocket Pod (use Strategy pattern for easy swapping).

**Rules**:
- Weapons unlocked via exact scrap costs in GDD V4.2 economy table.
- Heat / cooldown mechanics must be data-driven where possible.
- Visual/audio feedback is mandatory (muzzle flash, sparks, etc. — keep light until Phase 4).

---

## 6. Economy & Scrap (Data-Driven Where Possible)

Use **exact** values from GDD V4.2:

**Scrap Types**:
- Motor: 55% drop, 8 credits, Common
- Drone Frame: 25% drop, 22 credits, Common
- Circuit Chip: 12% drop, 65 credits, Uncommon
- Battery Pack: 5% drop, 110 credits, Uncommon
- Explosives: 3% drop, 180 credits, Rare

**Weapon Unlock Examples** (Phase 3, simplified in V4.2):
- Laser Cutter: 1000 credits
- RF Jammer: 5000 credits

(Note: Economy was simplified in GDD V4.2 to total credit costs. Support "equivalent value mix" if mentioned in future updates.)

**Rules**:
- PUB vendor handles all selling/buying (use Event Bus + simple UI).
- Support "equivalent value mix" conversion at 1.2× rate.
- First permanent upgrade example: bigger backpack.

---

## 7. Event Bus & Systems Communication

**Mandatory Events** (at minimum):
- `OnDroneKilled`
- `OnScrapCollected` / `OnScrapSold`
- `OnUpgradeBought`
- `OnWeaponUnlocked` / `OnWeaponEquipped`

**Rules**:
- Central Event Bus (or equivalent) — all systems subscribe in `OnEnable()`, unsubscribe in `OnDisable()`.
- No direct method calls between unrelated systems (e.g., DroneManager should not directly talk to EconomyManager).
- UI updates (currency, sell prompts) driven by events.

---

## 8. Performance, Quality & Technical Constraints

- **Target Hardware**: Mid-range (RTX 3060 class) but optimize for lower (user GTX 960 context noted — be conservative with draw calls and allocations).
- **Metrics** (Phase 4 pass):
  - 60 FPS average, 30+ drones active
  - Frame time < 16.6 ms (no spikes > 25 ms)
  - Draw calls < 450 in War Front
  - Memory < 1.8 GB after 45 min
- Use LODs, culling, and efficient pooling.
- **No heavy particles/VFX** until performance is verified in Phase 4.
- All models: `.fbx` → compiled `.vmdl`. No raw meshes in scene.
- First-person: Use built-in PlayerController + Citizen hands.
- Scene versioning: Work in `DroneForge_Main.scene` and version after milestones.

---

## 9. First-Time User Experience (FTUE) — 5-Minute Onboarding

Code must fully support the designed flow:
1. Spawn at PUB safe zone.
2. Marcus (Mechanic NPC) 30-second tutorial.
3. Guided to Combat Arena edge.
4. 3 Scout Drones spawned safely.
5. On-screen prompts for Net Catcher.
6. After 2 kills → sell prompt at Lena.
7. First credits earned → upgrade prompt.

**Rules**:
- Safe spawn distances and invulnerability during tutorial.
- Clear on-screen prompts and highlights.
- Zero confusion goal: 90%+ of new players complete the loop in < 5 min.

---

## 10. Naming, Organization & Code Style

**Recommended Structure** (example):
```
Scripts/
├── Core/               # EventBus, GameManager, PoolManager
├── DroneAI/            # FSM base + Scout/Striker/Heavy controllers
├── Weapons/            # Weapon base + Strategy implementations
├── Economy/            # Scrap types, PUB vendor, Wallet
├── Systems/            # Managers that orchestrate (DroneManager, etc.)
├── UI/                 # HUD, sell prompts, FTUE prompts
├── Pooling/            # Generic + specific pools
└── Data/               # ScriptableObjects for economy, drone stats, weapon defs
```

**Naming**:
- Clear and intention-revealing: `DroneManager`, `ScrapPool`, `EventBus`, `NetCatcherStrategy`, `HeavyHaulerFSM`.
- Boolean variables: `isChasing`, `hasTarget` (not `chasing`).
- Events: `OnDroneKilled`, `OnScrapSold`.

**Comments**:
- Reference relevant GDD V4.2 section when implementing non-obvious logic.
- Explain *why* a pattern was chosen if it deviates from the most obvious approach.

---

## 11. V1 Scope Reminder (Strict)

**In Scope for V1**:
- Basic first-person movement + Citizen hands
- 3 drone types with FSM + Object Pooling
- Net Catcher + Laser (Strategy)
- Scrap collection, physics, sell at PUB with economy table
- Greybox 200×200m map + all zones blocked out
- Event Bus + basic sound/VFX + 5-min FTUE
- Internal 10-session playtest + telemetry hooks

**Out of Scope for V1 (Deferred to V2+)** — implement after V1:
- Modular drone building / player-owned drones (main focus of Phase 7 Drone Forging)
- Building raids, Hotel storage, bounty missions (V1.5+)
- Multiplayer, skill systems, seasonal wipes
- Heavy narrative or cutscenes

If a request would violate V1 scope, **explicitly flag it** and suggest a V1-compliant alternative or note it for Phase 7 / V2.

---

## 12. Common Blindspots & Anti-Patterns to Avoid

- Ignoring Object Pooling → GC spikes and FPS drops (biggest performance killer).
- Hard-coding economy values or pool sizes instead of using data from GDD.
- Tight coupling between Drone AI and Economy / UI.
- Implementing V2 features too early (scope creep in code).
- Poor FSM state transitions or missing edge cases (drones getting stuck).
- Forgetting `OnEnable`/`OnDisable` event cleanup (memory leaks / double subscriptions).
- Assuming high-end hardware — test pooling and culling early.
- Weak FTUE implementation (confusing prompts, bad spawn distances).

---

## 13. How to Use These Guidelines with Any AI

**Best Practice Prompt Template** (copy-paste this + your task):

```
You are an expert s&box C# developer working on Drone Forge (GDD V4.2).

Follow the DroneForge_AI_Coding_Guidelines.md **strictly** at all times.

Key non-negotiables:
- Use Object Pooling exactly as specified (DronePool 60, ScrapPool 150, etc.).
- Every system uses OnEnable/OnDisable + Event Bus.
- Strategy pattern for weapons, FSM for drone AI.
- Stay inside V1 scope unless I explicitly say otherwise.
- Reference GDD V4.2 sections when relevant.
- Prioritize 60 FPS performance and clean architecture.

Task: [Describe exactly what you want implemented, e.g. "Implement the basic DroneManager with pooling and FSM for Scout drone only. Create the necessary scripts and show how to integrate with Event Bus."]

Current relevant files / context: [paste or @mention files if in Cursor]
```

**For Cursor / Continue.dev**:
- Add this entire guidelines file to Project Rules or @-reference it in Composer.
- Create a custom rule or .cursorrules file pointing to it.

**For local models** (your fast setup):
- Prepend or attach the guidelines file to every important prompt. It dramatically reduces hallucinations on patterns and scope.

---

## 14. Versioning & Updates

- This guidelines file is versioned alongside GDD (currently v1.0 for GDD V4.2).
- Update this file whenever GDD V4.x changes patterns, economy, or scope.
- When adding new systems in later phases, extend the relevant sections here.

---

**End of Guidelines**

**Next Step Recommendation for Maximum Velocity**:
Feed this file + the full GDD V4.2 into Cursor (with Claude) or your local model for the next feature. Start with a well-defined, in-scope piece (e.g., completing the Event Bus + basic DroneManager + pooling integration, or the PUB vendor sell flow).

This single artifact should significantly reduce bad code iterations and let you focus on design, playtesting, and shipping.

Would you like me to:
1. Generate the first implementation using these exact guidelines (e.g., Event Bus + DroneManager skeleton)?
2. Create a matching `.cursorrules` or prompt library?
3. Review/update any existing code (CurrencyHUD.cs, PlayerWallet.cs, etc.) against these guidelines?
4. Add a "Dev Workflow & AI Usage" section to the GDD V4.2 itself?

Just say the word and we'll keep shipping. 🚀