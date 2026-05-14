# Colosseum.NetworkGameplay assembly boundary

This file documents why `Colosseum.NetworkGameplay.asmdef` currently has
`allowUnsafeCode: true`.

## Reason

`Colosseum.NetworkGameplay` contains Fusion `NetworkBehaviour` types with
`[Networked]` properties used by the non-DevMode 2-client gameplay path.
Fusion IL Weaver generates access code for those properties and that generated
code relies on `Fusion.NetworkBehaviour.Ptr`.

Disabling `allowUnsafeCode` on this assembly is not a harmless permission
tightening. The project can still build, but the game server fails at runtime
after player spawn with `FieldAccessException` on generated network accessors.
Observed failures included:

- `Colosseum.Weapon.Gun:get_IsReloading()`
- `Colosseum.Game.RoomManager:get_CurrentRoomIndex()`

## Current minimum practical scope

`Colosseum.Game` remains `allowUnsafeCode: false`.
`INetworkInput` is separated into `Colosseum.NetworkInput`.
Camera edge bullet-wall helpers live in `Colosseum.Game` because they are
plain Unity components, while Fusion bullet logic only references their marker
type through the existing one-way `NetworkGameplay -> Game` dependency.
`Colosseum.StatusEffects` contains status-effect ScriptableObject
implementations and remains `allowUnsafeCode: false`.
The dev tooling assembly contains editor tooling controllers/adapters and remains
`allowUnsafeCode: false`. It is not restricted with `includePlatforms: Editor`
because its MonoBehaviours are attached to scenes and must be loadable in
Editor Play Mode. The DevMode source files keep their `#if UNITY_EDITOR`
guards so player builds still exclude the editor-only implementation.
NetworkGameplay exposes only a tiny editor bridge for the bullet prefab
override so Gun does not depend on DevMode types.
Only the runtime gameplay assembly that Fusion must weave keeps
`allowUnsafeCode: true`.

Narrower folder-level asmdefs were checked and are not a safe one-step change
because current runtime dependencies are cyclic:

- `UI` depends on `PlayerHealth`, `RoomManager`, and `Gun`, while gameplay code
  calls UI managers directly.
- `DevMode` depends on `NetworkManager`, card data, and UI, while
  `Gun.DevMode` depends on the DevMode bullet injection adapter.
- `Card`, `Weapon`, `Player`, and `Game` reference each other through card
  application, firing, damage, room advance, and camera logic.

Splitting these further requires a separate refactor that introduces explicit
interfaces or event boundaries first.

After A303-282, remaining non-Fusion-looking files in NetworkGameplay are left
there deliberately when moving them would cross ownership or introduce a cycle:

- `Network/*` service/contract helpers are network ownership and protocol
  boundary candidates, so they should move only with a separate network review.
- Dev/debug UI has a cycle today: DevMode creates UI panels, while debug UI
  reaches back into card/network gameplay types.
- The runtime card/combat/player/room classes still contain Fusion state or
  directly participate in Fusion-ticked gameplay and remain in the weaved
  assembly.

## Guardrail

The unsafe permission is for Fusion-generated IL compatibility only.
Do not add hand-written `unsafe` blocks, pointer arithmetic, or unmanaged memory
access to this assembly. If gameplay code needs such code, create a separate
reviewed issue and a smaller dedicated assembly.
