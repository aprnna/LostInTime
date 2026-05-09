# Double Deep Q-Network (DDQN) Implementation

## Overview

Double Deep Q-Network (DDQN) adalah algoritma reinforcement learning yang mengatasi masalah **overestimation bias** pada standard DQN. Implementasi ini dibuat sebagai custom trainer plugin untuk Unity ML-Agents.

## Table of Contents

- [Architecture](#architecture)
- [Core Components](#core-components)
- [Algorithm](#algorithm)
- [Hyperparameters](#hyperparameters)
- [Training Flow](#training-flow)
- [File Structure](#file-structure)
- [Comparison with DQN](#comparison-with-dqn)
- [Usage](#usage)

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                        DDQN ARCHITECTURE                             │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌─────────────────────┐         ┌─────────────────────┐           │
│  │    ONLINE NETWORK   │         │   TARGET NETWORK    │           │
│  │    Q_online(s, a)   │         │   Q_target(s, a)    │           │
│  │                     │         │                     │           │
│  │  - Input: State     │         │  - Input: State     │           │
│  │  - Output: Q-values │         │  - Output: Q-values  │           │
│  │  - Hidden: 64 units │         │  - Hidden: 64 units │           │
│  │  - Layers: 2        │         │  - Layers: 2        │           │
│  └─────────────────────┘         └─────────────────────┘           │
│            │                                │                       │
│            │         Soft Update            │                       │
│            │    θ_target = τ*θ_online +     │                       │
│            │         (1-τ)*θ_target          │                       │
│            └───────────────────────────────►│                       │
│                          τ = 0.005          │                       │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Core Components

### 1. QNetworkDDQN (Neural Network)

**File:** `ml_agents_plugin/mlagents_plugin_ddqn/ddqn_optimizer.py`

```python
class QNetworkDDQN(nn.Module, Actor, Critic):
    """Q-Network for DDQN with target network support."""
    
    # Architecture:
    # - ValueNetwork backbone
    # - Input: Observations (state)
    # - Output: Q-values for each discrete action
    
    def __init__(self, stream_names, observation_specs, network_settings, action_spec):
        output_act_size = max(sum(action_spec.discrete_branches), 1)
        self.network_body = ValueNetwork(
            stream_names,
            observation_specs,
            network_settings,
            outputs_per_stream=output_act_size,
        )
```

**Forward Pass:**
```python
def forward(self, inputs, masks=None, memories=None, sequence_length=1):
    # Get Q-values from network
    out_vals, memories = self.critic_pass(inputs, memories, sequence_length)
    
    # Epsilon-greedy action selection
    if random() < self.exploration_rate:
        action = self.get_random_action(inputs)    # Explore
    else:
        action = self.get_greedy_action(out_vals)  # Exploit
    
    return action, run_out, memories
```

### 2. DDQNOptimizer (Learning Algorithm)

**File:** `ml_agents_plugin/mlagents_plugin_ddqn/ddqn_optimizer.py`

```python
class DDQNOptimizer(TorchOptimizer):
    def __init__(self, policy, trainer_settings):
        # Online network (policy.actor)
        self.policy.actor = QNetworkDDQN(...)
        
        # Target network (copy of online)
        self.q_net_target = QNetworkDDQN(...)
        ModelUtils.soft_update(self.policy.actor, self.q_net_target, 1.0)
        
        # Optimizer
        self.optimizer = Adam(params, lr=0.0001)
```

### 3. DDQNTrainer (Training Loop)

**File:** `ml_agents_plugin/mlagents_plugin_ddqn/ddqn_trainer.py`

```python
class DDQNTrainer(OffPolicyTrainer):
    def _process_trajectory(self, trajectory):
        # Store transitions in replay buffer
        super()._process_trajectory(trajectory)
        
        # Update normalization
        self.policy.actor.update_normalization(buffer)
        
        # Store rewards for statistics
        self.collected_rewards["environment"][agent_id] += sum(rewards)
```

---

## Algorithm

### DDQN Target Calculation

**Standard DQN (Overestimation Problem):**
```
y = r + γ * max_a Q_target(s', a)
         └──────────────────────┘
         Target network selects AND evaluates action
         → Tends to overestimate Q-values
```

**DDQN (Reduced Overestimation):**
```
y = r + γ * Q_target(s', argmax_a Q_online(s', a))
         │            │
         │            └── Online network selects action
         └── Target network evaluates Q-value
         
→ Separates action selection from value evaluation
→ Reduces overestimation bias
```

### Update Step Implementation

```python
def update(self, batch, num_sequences):
    # 1. Get current state and next state from batch
    current_obs = ObsUtil.from_buffer(batch)
    next_obs = ObsUtil.from_buffer_next(batch)
    
    # 2. Get current Q-values from ONLINE network
    current_q_values = self.policy.actor.critic_pass(current_obs)
    
    # 3. DDQN KEY: Use ONLINE network to select actions from NEXT state
    with torch.no_grad():
        next_q_online = self.policy.actor.critic_pass(next_obs)
        greedy_actions = argmax(next_q_online)  # Online selects action
        
        # 4. Use TARGET network to evaluate Q-values
        next_q_target = self.q_net_target.critic_pass(next_obs)
        next_q_values = gather(next_q_target, greedy_actions)
    
    # 5. Compute target: y = r + γ * Q_target(s', a*)
    target_q_values = rewards + gamma * next_q_values * (1 - done)
    
    # 6. Compute loss (Huber/Smooth L1)
    loss = smooth_l1_loss(current_q_values, target_q_values)
    
    # 7. Backpropagation
    optimizer.step()
    
    # 8. Soft update target network
    soft_update(policy.actor, q_net_target, tau=0.005)
```

### Loss Function: Smooth L1 (Huber Loss)

```python
loss = torch.nn.functional.smooth_l1_loss(curr_q, target_q_values)
```

**Formula:**
```
         ⎧ 0.5 * (x - y)²    if |x - y| < 1
Loss =  ⎨
         ⎩ |x - y| - 0.5     otherwise
```

**Properties:**
- Quadratic near 0 (like MSE) - stable for small errors
- Linear for large errors (like MAE) - robust to outliers
- Less sensitive to noisy TD targets than MSE

---

## Hyperparameters

| Parameter | Value | Description |
|-----------|-------|-------------|
| `learning_rate` | 0.0001 | Adam optimizer learning rate |
| `gamma` | 0.95 | Discount factor for future rewards |
| `tau` | 0.005 | Soft update coefficient for target network |
| `batch_size` | 64 | Number of samples per training batch |
| `buffer_size` | 50000 | Maximum transitions in replay buffer |
| `exploration_initial_eps` | 1.0 | Starting epsilon (100% random actions) |
| `exploration_final_eps` | 0.05 | Final epsilon (5% random actions) |
| `exploration_decay_steps` | 100000 | Steps to decay epsilon |
| `hidden_units` | 64 | Neurons per hidden layer |
| `num_layers` | 2 | Number of hidden layers |
| `max_steps` | 300000 | Total training steps |

---

## Training Flow

```
┌─────────────────────────────────────────────────────────────────────┐
│                      TRAINING LOOP                                   │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  1. ENVIRONMENT STEP                                                 │
│     ├── Agent observes state s                                       │
│     ├── Q_network selects action a (epsilon-greedy)                 │
│     ├── Environment returns (s', r, done)                          │
│     └── Store (s, a, r, s', done) in replay buffer                  │
│                                                                     │
│  2. LEARNING STEP (every step)                                       │
│     ├── Sample batch from replay buffer                              │
│     ├── Compute DDQN target:                                         │
│     │   y = r + γ * Q_target(s', argmax Q_online(s', a))            │
│     ├── Compute loss: Smooth L1(Q_online(s, a), y)                   │
│     ├── Backpropagate to update Q_online                             │
│     └── Soft update Q_target:                                        │
│         θ_target = τ * θ_online + (1 - τ) * θ_target               │
│                                                                     │
│  3. EXPLORATION DECAY                                                │
│     └── epsilon = linear_decay(initial=1.0, final=0.05, steps=100k) │
│                                                                     │
│  4. REPEAT until max_steps (300,000)                                │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

### Exploration Schedule

```
Epsilon
   │
1.0 ┤████████████████████
    │                    ████
    │                        ████
    │                            ████
0.05┤                                █████████████████████████████
    └─────────────────────────────────────────────────────────────► Steps
     0                                                         100,000
```

---

## File Structure

```
ml_agents_plugin/
└── mlagents_plugin_ddqn/
    ├── __init__.py           # Plugin registration
    ├── ddqn_trainer.py       # Main trainer class
    │   └── class DDQNTrainer(OffPolicyTrainer)
    │       ├── _process_trajectory()
    │       ├── create_optimizer()
    │       └── create_policy()
    │
    ├── ddqn_optimizer.py     # Optimizer & Q-network
    │   ├── class DDQNSettings
    │   ├── class DDQNOptimizer(TorchOptimizer)
    │   │   ├── __init__()
    │   │   └── update()
    │   └── class QNetworkDDQN(nn.Module, Actor, Critic)
    │       ├── forward()
    │       ├── critic_pass()
    │       ├── get_greedy_action()
    │       └── get_random_action()
    │
    └── ddqn_policy.py        # Policy wrapper
        └── class DDQNPolicy(TorchPolicy)

config/
└── ddqn.yaml                 # Training configuration
```

---

## Comparison with DQN

| Aspect | DQN | DDQN |
|--------|-----|------|
| **Target Calculation** | `max Q_target(s', a)` | `Q_target(s', argmax Q_online(s', a))` |
| **Action Selection** | Target network | Online network |
| **Value Evaluation** | Target network | Target network |
| **Overestimation** | High | Reduced |
| **Stability** | Lower | Higher |
| **Convergence** | Slower | Faster |

### Why DDQN Reduces Overestimation

In standard DQN:
```
E[max Q] ≥ max E[Q]  (Jensen's inequality)
```
The max operator overestimates because it selects the maximum Q-value (which tends to be optimistic due to noise).

In DDQN:
```
E[Q(s', argmax Q_online)] ≈ E[Q(s', a*)]
```
The action is selected by a different network (online), breaking the correlation between selection and evaluation.

---

## Usage

### Installation

```bash
cd ml_agents_plugin
pip install -e .
```

### Configuration File

```yaml
# config/ddqn.yaml
behaviors:
  ddqn_dda:
    trainer_type: ddqn
    
    hyperparameters:
      learning_rate: 0.0001
      learning_rate_schedule: linear
      batch_size: 64
      buffer_size: 50000
      tau: 0.005
      exploration_initial_eps: 1.0
      exploration_final_eps: 0.05
      exploration_decay_steps: 100000
      gamma: 0.95
    
    network_settings:
      normalize: true
      hidden_units: 64
      num_layers: 2
    
    reward_signals:
      extrinsic:
        gamma: 0.95
        strength: 1.0
    
    max_steps: 300000
```

### Training Command

```bash
mlagents-learn config/ddqn.yaml --run-id=ddqn_dda_v1 --train
```

### Unity Integration

```csharp
// DDAAgent.cs - Unity side
public class DDAAgent : Agent
{
    public override void CollectObservations(VectorSensor sensor)
    {
        // 12 observations (normalized 0-1):
        sensor.AddObservation(hpRatio);           // 1. HP ratio (after battle)
        sensor.AddObservation(winRate);           // 2. Win rate (rolling 20)
        sensor.AddObservation(turnCountNorm);     // 3. Turn count normalized
        sensor.AddObservation(difficultyNorm);    // 4. Difficulty level (0-1)
        sensor.AddObservation(areaProgress);       // 5. Area progress (0-1)
        sensor.AddObservation(playerLevelNorm);   // 6. Player level (0-1)
        sensor.AddObservation(areaTypeNorm);      // 7. Area type (0-1)
        sensor.AddObservation(damageRatio);       // 8. Damage ratio (0-1)
        // Battle phase features:
        sensor.AddObservation(currentHPRatio);    // 9. Current HP ratio (real-time)
        sensor.AddObservation(resourceDepletion); // 10. Resource depletion (0-1)
        sensor.AddObservation(enemyHPRatio);      // 11. Enemy HP ratio (0-1)
        sensor.AddObservation(criticalFlag);      // 12. Critical flag (HP < 30%)
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        int action = actions.DiscreteActions[0];
        // 0: Maintain difficulty
        // 1: Increase difficulty
        // 2: Decrease difficulty
    }
}
```

### Reward Calculation (Unity Side)

Rewards calculated in DDAAgent.cs, not in Python trainer:

```csharp
// Battle reward: [-0.3, +0.55]
// Peak at 50% HP remaining (flow zone: 40-60%)
float CalculateBattleReward(bool won, int endHP, int startHP, int turns)
{
    if (!won) return -0.3f;

    float hpRatio = (float)endHP / startHP;
    float target = 0.50f;   // Peak at 50% HP
    float width = 0.10f;    // Flow zone: 40-60%
    float dist = Mathf.Abs(hpRatio - target);

    float hpScore;
    if (dist <= width)
        hpScore = 0.5f * (1f - dist / width);  // +0.0 to +0.5
    else
        hpScore = -0.1f * ((dist - width) / (1f - width));

    float efficiencyBonus = turns <= expectedTurns * 1.5f ? 0.05f : 0f;
    return Mathf.Clamp(hpScore + efficiencyBonus, -0.3f, 0.55f);
}

// Flow state bonus: [-0.2, +0.3]
// Peak at 60% win rate (rolling 20 battles)
float CalculateFlowStateBonus()
{
    if (_winHistoryCount < 5) return 0f;

    float winRate = GetRunningWinRate();
    float dist = Mathf.Abs(winRate - 0.60f);

    if (dist < 0.10f)
        return 0.3f * (1f - dist / 0.10f);  // 0 to +0.3
    else
        return -0.2f * Mathf.Clamp01((dist - 0.10f) / 0.30f);
}

// Applied at area completion:
void OnAreaComplete(bool areaWon)
{
    float finalReward = _areaAccumulatedReward + CalculateFlowStateBonus();
    AddReward(finalReward);
    RequestDecision();  // DDA action for next area
}
```

**Target Metrics:**
- Win rate: ~60% (flow state engagement)
- HP remaining: 40-60% (challenging but achievable)

---

## References

1. **Original DDQN Paper:** van Hasselt, H., Guez, A., & Silver, D. (2016). "Deep Reinforcement Learning with Double Q-learning." AAAI.

2. **DQN Paper:** Mnih, V., et al. (2015). "Human-level control through deep reinforcement learning." Nature.

3. **ML-Agents Documentation:** https://github.com/Unity-Technologies/ml-agents

---

## Changelog

| Version | Date | Changes |
|---------|------|---------|
| 1.1.0 | 2026-05-05 | Added reward calculation, updated to 12 observations |
| 1.0.0 | Initial | DDQN implementation with soft update target network |

---

## License

MIT License - See LICENSE file for details.