# Refinements And Changes

This file is the running log for project refinements made by Codex.

## 2026-05-12

### Added Robot Command System

Created `Assets/Scripts/Robots/Commands/` with:

- `RobotCommand.cs`
- `RobotCommandSequence.cs`
- `RobotCommandParser.cs`
- `RobotCommandValidator.cs`
- `CommandTarget.cs`
- `CommandTargetRegistry.cs`

Purpose:

- Parse single-action JSON and sequence JSON from Ollama responses.
- Normalize command actions, targets, resources, priorities, and amounts.
- Register scene targets by stable ids.
- Validate LLM output before commands reach robot execution.

### Added Robot Runtime Components

Created `Assets/Scripts/Robots/` with:

- `MiningRobotController.cs`
- `MiningRobotInventory.cs`
- `RobotCommandQueue.cs`
- `RobotCommandExecutor.cs`
- `RobotAnimationController.cs`

Purpose:

- Queue validated robot commands.
- Drive robot movement through `NavMeshAgent`.
- Mine existing `IMineable` targets.
- Pick up command-targeted resources.
- Deliver inventory to compatible targets.
- Expose simple animation hooks for movement, mining, pickup, and delivery.

### Added Robot State Classes

Created `Assets/Scripts/Robots/States/` with:

- `RobotIdleState.cs`
- `RobotMoveState.cs`
- `RobotMineState.cs`
- `RobotPickupState.cs`
- `RobotDeliverState.cs`

Purpose:

- Match the existing project preference for state-machine style behavior.
- Provide lightweight state wrappers for robot status and animation transitions.

### Added Ollama Integration Scripts

Created `Assets/Scripts/Robots/LLM/` with:

- `OllamaRobotCommandClient.cs`
- `RobotCommandPromptBuilder.cs`

Purpose:

- Build strict robot-command prompts for Ollama.
- Send player instructions through `OllamaRequest`.
- Parse and validate model responses.
- Submit accepted command sequences to the selected mining robot.

### Build Verification

Ran:

```powershell
dotnet build .\Assembly-CSharp.csproj -v:minimal
```

Result:

- Build succeeded.
- Remaining warnings are from pre-existing non-robot scripts.

### Notes For Scene Setup

Still required in Unity:

- Add `CommandTargetRegistry` to gameplay scenes.
- Add `CommandTarget` components to rocks, pickup items, storage, refinery, and robot objects.
- Create a mining robot prefab with `NavMeshAgent` and the robot runtime scripts.
- Bake NavMesh data before testing robot movement.

### Completed Robot State Logic

Updated the robot state-machine integration after the robot animation logic was refactored.

Changed:

- Filled `MiningRobotController.HandleMining`.
- Filled `MiningRobotController.HandleMovement`.
- Filled the robot state transition checks.
- Added controller `Update` and `FixedUpdate` calls into the robot state machine.
- Added `IsMiningAnimationReady` so command execution can wait for the mining animation state.
- Updated robot state classes to properly override `RobotBaseState` methods.
- Updated `RobotCommandExecutor` so mining hits wait until `RobotStartMiningState` finishes and `RobotMineState` begins.

Result:

- `RobotWorkState.Mining` now transitions through `RobotStartMiningState` first.
- Actual mining damage starts after the start-mining transition animation completes.
- Movement and mining states now stop/start the `NavMeshAgent` consistently.
- Build verification passed with `dotnet build .\Assembly-CSharp.csproj -v:minimal`.

### Split Robot Logic By Robot Type

Refactored shared robot behavior into base classes so mining, collecting, and scanning robots can share command submission, validation, queue polling, movement, waiting, stopping, NavMesh control, inventory access, and status events.

Changed:

- Reworked `BaseRobotController` to own shared controller state and dependencies.
- Reworked `BaseRobotCommandExecutor` to execute common actions: `move`, `wait`, and `stop`.
- Updated `MiningRobotController` so it only contains mining-specific animation state-machine logic.
- Updated `MiningRobotCommandExecutor` so it only handles `mine_resource`.
- Updated `CollectingRobotController` so it declares pickup/delivery capabilities.
- Updated `CollectingRobotCommandExecutor` so it only handles `pickup` and `deliver`.
- Updated `RobotCommandValidator` to validate against `BaseRobotController` and reject actions unsupported by a robot type.
- Updated `OllamaRobotCommandClient` to target any `BaseRobotController`, not only mining robots.
- Added `scan` as a command action.
- Added `ScanningRobotController` and `ScanningRobotCommandExecutor` as the scanning robot skeleton.

Result:

- Mining robots support `move`, `wait`, `stop`, and `mine_resource`.
- Collecting robots support `move`, `wait`, `stop`, `pickup`, and `deliver`.
- Scanning robots support `move`, `wait`, `stop`, and `scan`.
- Build verification passed with `dotnet build .\Assembly-CSharp.csproj -v:minimal`.

### Fixed Collecting Robot Inventory Ownership

Updated the collecting robot code so inventory access stays on the collecting robot only.

Changed:

- `CollectingRobotController` now ensures a `CollectingRobotInventory` component exists in `Awake`.
- `CollectingRobotCommandExecutor` now caches `CollectingRobotController`.
- Pickup and delivery logic now uses `collectingRobot.Inventory` instead of the base robot controller.

Result:

- `BaseRobotController` remains inventory-free.
- Mining and scanning robots no longer imply inventory ownership.
- Build verification passed with `dotnet build .\Assembly-CSharp.csproj -v:minimal`.

### Connected Robot Interaction To LLM Chat UI

Finished the runtime flow for opening the Nova/Neocortex `LLMChatRoot` chat when the player interacts with a robot.

Changed:

- Updated `RobotInteraction` to find the nearby player's `PlayerInteractionDetector`, show/hide the existing interaction indicator, and open the LLM chat for the selected robot.
- Added `RobotChatUIController` beside the robot interaction logic so the scene `LLMChatRoot` can be found automatically and wired to the robot command pipeline at runtime.
- Connected `NeocortexTextChatInput.OnSendButtonClicked` to `OllamaRobotCommandClient.SubmitPrompt`.
- Made the chat controller target the currently interacted `BaseRobotController`, so mining, collecting, and scanning robots can all receive prompts through the same UI.
- Added runtime setup for missing `RobotCommandPromptBuilder`, `RobotCommandValidator`, and `OllamaRobotCommandClient` components on the chat root.
- Updated `OllamaRobotCommandClient` so it re-checks its prompt builder, validator, target registry, and default robot references before initializing or submitting prompts.
- Updated `PlayerInteractionDetector` so interaction fires once per button press and works when the trigger hits a child collider of an interactable object.
- Updated `PlayerController` so movement, look, jumping, attacking, and cursor locking pause while the robot chat is open.

Result:

- Approaching a robot still shows the interaction indicator.
- Interacting with the robot opens `LLMChatRoot`.
- Sending text from the chat input submits the prompt to the selected robot's Ollama command client.
- Leaving the robot interaction range closes the chat for that robot.
- Build verification passed with `dotnet build .\Assembly-CSharp.csproj -v:minimal`; remaining warnings are unrelated existing project warnings.

### Close Chat After Sending Prompt

Updated `RobotChatUIController` so pressing the send button closes the LLM chat after a non-empty prompt is submitted to the selected robot.

Changed:

- Added a `closeAfterSubmit` option to `RobotChatUIController`.
- Called `Close()` after `OllamaRobotCommandClient.SubmitPrompt(prompt)` succeeds past the local empty-prompt checks.

Result:

- Empty prompts keep the chat open.
- Valid prompts are submitted first, then `LLMChatRoot` is hidden.
- Build verification passed with `dotnet build .\Assembly-CSharp.csproj -v:minimal`; remaining warnings are unrelated existing project warnings.

### Improved Resource Target Resolution

Debugged a case where Ollama returned a generic target type instead of a real target id:

```json
{"action":"mine_resource","target":"ResourceNode","resource":"coal","priority":"normal","amount":1}
```

Changed:

- Updated `RobotCommandValidator` so mining and pickup commands can still validate when the target is blank or invalid but a matching `resource` is present.
- Kept invalid/generic targets from blocking execution; the command executor can now resolve the nearest matching resource target at runtime.
- Tightened `RobotCommandPromptBuilder` instructions so the LLM is told not to use target type labels like `ResourceNode`, `Storage`, or `Refinery` as `target` values.
- Changed target list formatting in prompts to explicitly show `id`, `type`, and `resource`.
- Added an accepted-command debug log in `OllamaRobotCommandClient` so successful validation/queue submission is visible separately from the raw Ollama response.

Result:

- A response like `target: ResourceNode, resource: coal` should now resolve to the nearest registered coal resource instead of being rejected as an unknown target.
- Future prompts should be less likely to produce generic target labels.
- Build verification passed with `dotnet build .\Assembly-CSharp.csproj -v:minimal`; remaining warnings are unrelated existing project warnings.

### Mining Robot Faces Resource Before Mining

Updated the mining command execution so the robot turns toward the resource node before starting the mining animation/state.

Changed:

- Added configurable facing settings to `MiningRobotCommandExecutor`.
- Stopped the NavMeshAgent after reaching the resource target.
- Added a short Y-axis-only turn toward the target collider center before `RobotWorkState.Mining` begins.

Result:

- The mining robot should now face the resource node when the mining animation starts.
- Build verification passed with `dotnet build .\Assembly-CSharp.csproj -v:minimal`; remaining warnings are unrelated existing project warnings.

### Added Return-To-Player Robot Commands

Implemented support for prompts like `Go and mine some coal and then return back to me`.

Changed:

- Added runtime configuration support to `CommandTarget` so dynamic targets can be registered from code.
- Updated `RobotChatUIController` to register the interacting player as a command target with id `player`.
- Updated `RobotInteraction` so the player that opened the chat is passed into the chat controller.
- Updated `RobotCommandPromptBuilder` to instruct the LLM to add `{"action":"move","target":"player"}` when the player asks the robot to return or come back.
- Added target aliases in `RobotCommand` so `me`, `user`, and similar model outputs normalize to `player`.
- Added return-style action aliases like `return`, `return_to_player`, and `come_back` as movement commands.
- Updated `BaseRobotCommandExecutor` to refresh moving-target destinations while travelling, so return-to-player commands can follow the player's current position.

Result:

- A sequence can mine a resource until depletion and then move back to the player.
- The accepted command should look like a sequence with `mine_resource` followed by `move` to `player`.
- Build verification passed with `dotnet build .\Assembly-CSharp.csproj -v:minimal`; remaining warnings are unrelated existing project warnings.

### Accepted Raw JSON Command Arrays

Debugged a case where Ollama returned the correct commands as a top-level JSON array instead of wrapping them in a `sequence` object:

```json
[{"action":"mine_resource","resource":"coal","priority":"normal"},{"action":"move","target":"player"}]
```

Changed:

- Updated `RobotCommandParser` to parse top-level JSON arrays as command sequences.
- Kept support for the existing single-command object and `{ "sequence": [...] }` formats.
- Updated `RobotCommandPromptBuilder` to explicitly ask for `{ "sequence": [...] }` rather than a raw top-level array.

Result:

- `Go mine some coal and return back to me` should now work even if the model returns a raw array.
- Build verification passed with `dotnet build .\Assembly-CSharp.csproj -v:minimal`; remaining warnings are unrelated existing project warnings.

### Mine All Visible Resource Nodes By Default

Updated mining command validation so a vague resource mining command mines every matching visible node before continuing to later queued commands like returning to the player.

Changed:

- Added `node_count` support to `RobotCommand` for explicit node-count requests.
- Updated `RobotCommandParser` to detect explicit `amount`, `node_count`, and `nodeCount` fields.
- Updated `RobotCommandValidator` to expand a resource-only `mine_resource` command into one targeted mining command per matching resource node.
- Sorted expanded resource-node commands nearest-first from the robot.
- Added `CommandTargetRegistry.FindTargets` to gather all matching targets.
- Updated `CommandTarget` so generic resource ids like `coal` become unique runtime ids, preventing several nodes from resolving to the same target.
- Updated prompt rules so vague commands like `mine some coal` omit `amount` and mean all visible matching nodes, while specific node-count requests use `node_count`.

Result:

- `Go mine some coal and return to me` should mine all registered coal nodes, then return to the player.
- `Go mine two coal nodes and return to me` should mine two nearest coal nodes, then return.
- Build verification passed with `dotnet build .\Assembly-CSharp.csproj -v:minimal`; remaining warnings are unrelated existing project warnings.

### Aim Mining Rig At Current Resource Node

Updated the mining robot so its Animation Rigging `MultiAimConstraint` targets the current resource node while mining.

Changed:

- Added `MultiAimConstraint` support to `MiningRobotController`.
- Added `SetMiningLookTarget` and `ClearMiningLookTarget` methods to control the first source object on the LookAim constraint.
- Automatically finds a child `MultiAimConstraint` if one is not assigned in the inspector.
- Preserves an existing default aim source when one is assigned, or creates a fallback target so the constraint can be cleared safely.
- Updated `MiningRobotCommandExecutor` to set the look target to the current resource node transform before entering the mining state, and clear it when the mining command ends.

Result:

- While mining, the robot's LookAim source object is the transform of the current resource node.
- The rig can now aim up or down based on the resource node's actual position.
- Build verification passed with `dotnet build .\Assembly-CSharp.csproj -v:minimal`; remaining warnings are unrelated existing project warnings.

### Rebuilt Mining LookAim Rig After Runtime Target Changes

Fixed a runtime Animation Rigging issue where the `LookAim` inspector showed the current resource node as the source object, but the spine did not move in play mode.

Changed:

- Added a `RigBuilder` reference to `MiningRobotController`.
- Automatically finds the child `RigBuilder` when it is not assigned in the inspector.
- Rebuilds and syncs the rig after swapping the `MultiAimConstraint` source object.

Reason:

- Animation Rigging binds source transform handles when the rig graph is built.
- Changing the `MultiAimConstraint` source object at runtime updates the serialized data shown in the inspector, but the playable job can keep using the old/null transform handle until the rig is rebuilt.

Result:

- The dynamically assigned resource node should now drive the runtime spine aim, not just appear in the constraint inspector.
- Build verification passed with `dotnet build .\Assembly-CSharp.csproj -v:minimal`; remaining warnings are unrelated existing project warnings.
