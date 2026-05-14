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

### Populated Collection Robot Mining-Robot Dropdown

Updated the collection robot chat dropdown so it is filled from the active mining robots in the scene.

Changed:

- Updated `RobotChatUIController` to find the custom Nova `DropDownSetting` item view in the collection robot chat.
- Added dropdown click, hover, press, release, outside-click collapse, and selection handling.
- Populates the dropdown with every active `MiningRobotController` in the scene when a collecting robot chat opens.
- Uses each mining robot's `CommandTarget.TargetId`, so renamed mining robots appear by their current player-facing names.
- Hides the mining-robot dropdown when the chat is opened for a non-collecting robot.
- Stores the selected mining robot on `CollectingRobotController`.
- Updated `DropDownVisuals` so it can refresh a runtime data source and notify selection changes.
- Updated `MultiOptionSetting` so runtime option lists clamp selection safely.

Result:

- Opening the collection robot chat should show all active mining robots in the Nova dropdown.
- Selecting a dropdown item updates the collecting robot's selected mining robot reference for future collection logic.
- Build verification passed with `dotnet build .\Assembly-CSharp.csproj -v:minimal`; remaining warnings are unrelated existing project warnings.

### Added None Default To Collection Mining-Robot Dropdown

Updated the collection robot mining-robot dropdown so it can explicitly have no selected mining robot.

Changed:

- Added `None` as the first dropdown option.
- Made `None` the default selection when the collecting robot has no existing selected mining robot.
- Mapped dropdown index `0` to a null `SelectedMiningRobot`.
- Shifted real mining robot options to start after `None`.

Result:

- Opening the collection robot chat defaults the mining robot selector to `None`.
- Selecting a mining robot still stores that mining robot on `CollectingRobotController`.
- Build verification passed with `dotnet build .\Assembly-CSharp.csproj -v:minimal`; remaining warnings are unrelated existing project warnings.

### Improved Robot Target Names And Dropdown Refresh

Updated robot command-target naming and fixed the collection dropdown list refresh.

Changed:

- `BaseRobotController` now ensures every robot has a `CommandTarget` registered as `CommandTargetType.Robot`.
- Default robot target ids are type-aware:
  - Mining robots use `MiningRobot_#`.
  - Collection robots use `CollectionRobot_#`.
  - Scanning robots use `ScanningRobot_#`.
- Existing custom robot names are preserved unless they are blank, generic, or already duplicated.
- `RobotNameText` is populated with the active robot's command-target id when chat opens.
- If `RobotNameText` is backed by a Nova `TextField`, editing it updates the active robot's command-target id.
- The collection dropdown now reads mining robot labels from `MiningRobotController.RobotTargetId`.
- `DropDownVisuals.Expand` now activates the expanded root before setting and refreshing the Nova `ListView` data source.

Result:

- Robot names shown in the chat and dropdown now match command-target registry ids.
- The collection dropdown should show mining robot options immediately without needing to scroll first.
- Build verification passed with `dotnet build .\Assembly-CSharp.csproj -v:minimal`; remaining warnings are unrelated existing project warnings.

### Restored Robot ChatPanel Messages

Fixed the robot chat UI not showing the submitted prompt or robot response.

Changed:

- `RobotChatUIController` now finds the child `NeocortexChatPanel`.
- Adds the player's prompt to the chat panel before sending it to Ollama.
- Subscribes to `OllamaRobotCommandClient.OnCommandAccepted` and `OnCommandRejected`.
- Adds a robot response message after a command is accepted or rejected.
- Keeps the chat open when a chat panel exists, so the response remains visible.
- Added optional `clearMessagesOnOpen` support back to the chat controller.

Result:

- Submitted prompts should now appear in the chat panel.
- Accepted commands show `Message understood.`
- Rejected commands show a readable failure message instead of only logging to the console.
- Build verification passed with `dotnet build .\Assembly-CSharp.csproj -v:minimal`; remaining warnings are unrelated existing project warnings.

## 2026-05-13

### Added Collection Robot Follow-And-Collect Logic

Implemented the collection robot logic for collecting visible dropped ores and optionally following an assigned mining robot.

Changed:

- Updated `CollectingRobotCommandExecutor` so `pickup` with no target collects all visible pickup items inside the collecting robot's vision trigger.
- Added support for `pickup` targeting a robot, which makes the collecting robot follow that robot and collect visible nearby pickup items while following.
- Pickup commands targeting the selected mining robot, or follow-style pickup actions, follow the selected miner and collect nearby visible items.
- Kept explicit pickup-item targets working as before.
- Updated pickup validation so collecting robots can accept targetless visible-collection commands and robot-target follow-collection commands.
- Added parser aliases for collection language such as `collect_ores`, `gather_ores`, and `follow_and_collect`.
- Normalized generic resource terms like `ore` and `ores` to mean any pickup resource.
- Updated the Ollama prompt context so collection prompts produce either targetless pickup commands or pickup commands targeting the selected mining robot id.

Result:

- With dropdown set to `None`, prompts like `Collect all ores that you can see` should collect pickup items inside the collection robot's sphere trigger vision.
- With a mining robot selected, prompts like `Follow this robot and pickup all ores nearby` should target that mining robot, follow it, and collect visible ores along the route.
- Build verification passed with `dotnet build .\Assembly-CSharp.csproj -v:minimal`; remaining warnings are unrelated existing project warnings.

### Added Collection Robot Inventory UI

Implemented alt-interaction support for opening the collection robot inventory panel.

Changed:

- Added `IAltInteractable` so interactable objects can respond to the alternate interaction key separately from normal interaction.
- Updated `PlayerInteractionDetector` to detect alt-interact press edges and call `IAltInteractable.AltInteract`.
- Updated `RobotInteraction` so alt-interacting with a collecting robot opens `CollectionRobotInventoryRoot`, while normal interaction still opens the robot chat.
- Added `CollectingRobotInventoryUIController` to bind a collecting robot's inventory into the Nova inventory grid.
- The collection inventory UI auto-finds `CollectionRobotInventoryRoot`, its `Grid`, `CloseButton`, and `RobotNameText` by scene/prefab object names.
- Added `InventoryChanged` events to `CollectingRobotInventory` so the UI refreshes when the robot collects, removes, or clears items.
- Updated `PlayerController` so movement/look input is blocked while the collection robot inventory UI is open.
- Added a null guard around the collecting robot vision trigger setup.

Result:

- Pressing the alt interaction key near a collecting robot toggles the collection robot inventory UI.
- The UI displays item stacks collected through pickup item data.
- Resource-only stacks can also display if matching `OreItemSO` definitions are assigned on the inventory UI controller.
- Build verification passed with `dotnet build .\Assembly-CSharp.csproj -v:minimal`; remaining warnings are unrelated existing project warnings.

### Added Collection-To-Player Inventory Transfer

Updated the collection robot inventory UI so it can be used beside the player inventory.

Changed:

- Opening `CollectionRobotInventoryRoot` now also opens the player's inventory UI.
- Added a serialized `playerInventoryCloseButton` field on `CollectingRobotInventoryUIController`; when assigned, it is hidden while the robot inventory is open and restored afterward.
- Added drag/release handling to collection robot inventory slots.
- Dropping a dragged robot inventory stack over the player inventory grid transfers that stack to the player inventory.
- Added capacity checks to `UIManager` so robot stacks are only removed if the player inventory has room.
- Added item-stack removal support to `CollectingRobotInventory`.
- Added explicit `OpenInventory` and `CloseInventory` methods to `PlayerController`.
- Blocked the normal inventory toggle key while the collection robot transfer UI is open.

Result:

- The player inventory appears alongside the collection robot inventory.
- The player inventory close button can be disabled while transfer mode is active.
- Dragging a stack from the collection robot inventory onto the player inventory moves that stack from the robot to the player.
- Build verification passed with `dotnet build .\Assembly-CSharp.csproj -v:minimal`; remaining warnings are unrelated existing project warnings.

### Fixed Collection Inventory Transfer Detection

Fixed the first pass of collection-to-player transfer not triggering.

Changed:

- Removed the requirement for Nova `Gesture.OnDrag` to fire before an item can transfer.
- The inventory prefabs have draggable axes disabled, so `OnDrag` may not be emitted for grid slots.
- Transfer now requires only that the press started on a valid collection robot inventory slot and the release happened over the player inventory grid.
- Consumed the robot slot press/release events when they are used for transfer.

Result:

- Dragging or press-moving a collection robot inventory stack onto the player inventory should now transfer the stack.
- Build verification passed with `dotnet build .\Assembly-CSharp.csproj -v:minimal`; remaining warnings are unrelated existing project warnings.

### Added Collection Inventory Drag Preview

Added a visual preview while dragging items from the collection robot inventory.

Changed:

- `CollectingRobotInventoryUIController` now creates a small Nova `UIBlock2D` preview when the player presses a valid robot inventory item.
- The preview uses the dragged item's `InventoryItemData.itemDesc.Icon`.
- The preview follows the mouse until release or cancel.
- Added a high render-order `SortGroup` so the preview renders above the inventory panels.
- Added optional serialized settings for preview camera, icon size, offset, and z-index.

Result:

- Dragging an item from the collection robot inventory now shows the item icon under the mouse until the player releases it.
- Build verification passed with `dotnet build .\Assembly-CSharp.csproj -v:minimal`; remaining warnings are unrelated existing project warnings.

### Fixed Collection Robot Inventory Capacity

Fixed the collection robot only accepting one pickup item type.

Changed:

- Updated `CollectingRobotInventory` default stack capacity from 12 to 24.
- Changed inventory capacity checks to use one shared total stack count across item stacks and resource stacks.
- Updated `TestScene` collection robot inventory from `maxItemStacks: 1` to `maxItemStacks: 24`.

Reason:

- The scene instance had `maxItemStacks` serialized as `1`, so the first unique pickup item consumed the only available stack slot and later item types were rejected.

Result:

- The collection robot can now pick up multiple different item/resource types into separate inventory stacks.
- Build verification passed with `dotnet build .\Assembly-CSharp.csproj -v:minimal`; remaining warnings are unrelated existing project warnings.

### Added Robot Workbench Crafting Architecture

Added the first pass of robot crafting support for the new workbench UI.

Changed:

- Added `RobotCraftingRecipe` assets for configuring craftable robot type, display text, icon, prefab, and ore requirements.
- Added `RobotWorkbenchUIController` to bind `WorkbenchRoot` option slots, requirement slots, info text, craft button, and close button.
- Added `RobotWorkbenchInteraction` so a world workbench can open/toggle the workbench UI through the existing interaction detector.
- Added ore counting and ore spending helpers to `UIManager`.
- Updated player input blocking so movement, camera look, inventory toggling, and alternate interaction respect the workbench UI.
- Added the new crafting scripts to `Assembly-CSharp.csproj` so command-line build verification includes them.

Result:

- The workbench can select a robot recipe, show the owned/required ore costs, spend the required ores, and instantiate the configured robot prefab near a spawn point or the player.
- Build verification passed with `dotnet build .\Coreline_LLMProject.sln --no-restore`; remaining warnings are unrelated existing project warnings.
