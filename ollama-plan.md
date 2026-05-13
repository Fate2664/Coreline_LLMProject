# Ollama Robot Command Plan

## Goal

Use an Ollama-hosted LLM as a command translation layer for mining robots. The LLM should not directly control Unity objects. It should translate player text into a small JSON command format, then Unity should parse, validate, queue, and execute those commands through deterministic game systems.

## Inference Timing

Inference should happen only when the player submits a robot instruction, not every frame.

Recommended timing:

1. Player selects or interacts with a mining robot.
2. Player enters a prompt in the robot command UI.
3. `OllamaRobotCommandClient` sends one request to Ollama.
4. The response is parsed into `RobotCommandSequence`.
5. The sequence is validated against registered scene targets.
6. Valid commands are queued on the selected robot.
7. The robot executes the queue over time using `NavMeshAgent`, mining, pickup, and delivery logic.

Avoid continuous inference for navigation, animation, mining intervals, or target proximity checks. Those should stay inside Unity code.

Expected cadence:

- Prompt submission: user-driven, occasional.
- Command execution: frame/coroutine-driven in Unity.
- Re-prompting: only when the player gives a new instruction or when a command fails and the UI asks for clarification.

## Data Flow

```text
Player prompt
-> OllamaRobotCommandClient
-> OllamaRequest
-> Raw LLM response text
-> RobotCommandParser
-> RobotCommandSequence
-> RobotCommandValidator
-> RobotCommandQueue
-> RobotCommandExecutor
-> NavMeshAgent / IMineable / MiningRobotInventory / delivery target
```

The LLM output is treated as untrusted data. It must pass through parsing and validation before it affects the robot.

Scene objects that can be referenced by commands should have `CommandTarget` attached. `CommandTargetRegistry` provides lookup by target id and resource type.

## Prompt Structure

The system prompt should be strict and short:

- Return only valid JSON.
- Do not include markdown or explanations.
- Use only allowed actions.
- Use target ids exactly as provided.
- Use either a single command object or a `sequence` array.

Allowed actions:

- `move`
- `mine_resource`
- `pickup`
- `deliver`
- `wait`
- `stop`

Single command example:

```json
{
  "action": "mine_resource",
  "target": "iron_node_12",
  "priority": "high"
}
```

Sequence example:

```json
{
  "sequence": [
    {
      "action": "move",
      "target": "storage_1"
    },
    {
      "action": "pickup",
      "resource": "iron"
    },
    {
      "action": "deliver",
      "target": "refinery"
    }
  ]
}
```

The user prompt can include a generated list of available command targets:

```text
Available command targets:
- iron_node_12 type=ResourceNode resource=iron
- storage_1 type=Storage resource=none
- refinery type=Refinery resource=none

Player instruction:
Mine the closest iron and take it to the refinery.
```

## Risks

### Invalid JSON

Local models may return markdown, comments, or explanation text. `RobotCommandParser` extracts the first JSON object and rejects invalid JSON.

### Unknown Targets

The model may invent targets. `RobotCommandValidator` rejects targets that do not exist in `CommandTargetRegistry`.

### Unsupported Actions

The model may produce actions outside the game command vocabulary. Unknown actions are rejected.

### Ambiguous Player Requests

Commands like "mine something valuable" need game-side rules. The current structure supports target lookup by resource, but higher-level intent ranking should be added later if needed.

### Stale Scene Context

A target can be destroyed after the prompt is sent. The executor re-checks targets while executing and fails safely if a target disappears.

### Long Inference Latency

Ollama response time depends on model size and hardware. The UI should show a pending state while waiting and should allow canceling or replacing the robot queue.

### Overloaded Prompts

Sending every target in a large world can slow inference and confuse the model. Keep target lists short, local, or filtered by robot/player proximity.

### Model Hallucination

The model should never be trusted to decide whether an action is valid. All world permissions, distances, resources, inventory changes, and target states should be checked in Unity.

## Recommended Next Steps

1. Add `CommandTargetRegistry` to each gameplay scene.
2. Add `CommandTarget` to mineable rocks, storage, refinery, and pickup objects.
3. Build a `MiningRobot` prefab with `NavMeshAgent` and robot scripts.
4. Bake scene NavMesh data.
5. Create a simple robot command UI that calls `OllamaRobotCommandClient.SubmitPrompt`.
6. Test hardcoded JSON before relying on model output.
