// enemy-configs.ts - Enemy Configuration Data

// ===== TYPE DEFINITIONS =====
export interface BehaviorCondition {
  type: "distance" | "health" | "timer" | "random" | "hurt";
  operator: "<" | ">" | "<=" | ">=" | "==";
  value: number;
}

export interface ActionConfig {
  type: "move" | "animate" | "sound" | "invulnerable";
  params: {
    pattern?: "toward_player" | "away_from_player" | "random" | "stop" | "sideways_left" | "sideways_right" | "crab_toward_player";
    speed?: number;
    name?: string;
    duration?: number;
  };
}

export interface BehaviorConfig {
  name: string;
  duration: [number, number];
  weight: number;
  cooldown?: number;
  conditions?: BehaviorCondition[];
  actions: ActionConfig[];
}

export interface EnemyConfig {
  type: string;
  baseStats: {
    health: number;
    speed: number;
    viewDistance: number;
    attackDistance: number;
  };
  behaviors: BehaviorConfig[];
}

export interface EnemyData {
  maskUid: number;
  type: string;
  state: string;
  stateTimer: number;
  direction: string;
  config: EnemyConfig;
  currentBehavior: BehaviorConfig;
  lastPlayerDistance: number;
  behaviorCooldowns: Map<string, number>;
  behaviorStarted: boolean;
  isHurt: boolean;
  invulnerableTimer: number;
  sidewaysDirection: string;
}

// ===== ENEMY CONFIGURATIONS =====
export const OOZE_CONFIG: EnemyConfig = {
  type: "Ooze",
  baseStats: {
    health: 2,
    speed: 15,
    viewDistance: 120,
    attackDistance: 0
  },
  behaviors: [
    {
      name: "idle",
      duration: [1.0, 2.0], 
      weight: 4,
      actions: [
        { type: 'animate', params: { name: 'Idle' } },
        { type: 'move', params: { pattern: 'toward_player', speed: 20 } }		
      ]
    },
    {
      name: "hop",
      duration: [0.8, 1.2],
      weight: 2,
      cooldown: 2.0,
      conditions: [
        { type: 'distance', operator: '<', value: 250 }
      ],
      actions: [
        { type: 'animate', params: { name: 'Hop_${direction}' } },
        { type: 'move', params: { pattern: 'toward_player', speed: 50 } },
        { type: 'sound', params: { name: 'Slime_Jump' } }
      ]
    }
  ]
};

export const CRAB_CONFIG: EnemyConfig = {
  type: "Crab",
  baseStats: {
    health: 3,
    speed: 20,
    viewDistance: 150,
    attackDistance: 32
  },
  behaviors: [
    {
      name: "idle",
      duration: [0.5, 1.5],
      weight: 2,
      conditions: [
        { type: 'distance', operator: '>', value: 120 }
      ],
      actions: [
        { type: 'animate', params: { name: 'Idle' } }
      ]
    },
    {
      name: "cranky_chase",
      duration: [1.5, 3.0],
      weight: 6,
      conditions: [
        { type: 'distance', operator: '<', value: 120 },
        { type: 'distance', operator: '>=', value: 32 }
      ],
      actions: [
        { type: 'animate', params: { name: 'Cranky_${direction}' } },
        { type: 'move', params: { pattern: 'crab_toward_player', speed: 30 } },
        { type: 'sound', params: { name: 'Crab Walk' } }
      ]
    },
    {
      name: "shell_retreat",
      duration: [1.0, 1.5],
      weight: 2,
      cooldown: 3.0,
      conditions: [
        { type: 'random', operator: '<', value: 30 }
      ],
      actions: [
        { type: 'animate', params: { name: 'Retreat_Right' } },
        { type: 'move', params: { pattern: 'stop', speed: 0 } }
      ]
    },
    {
      name: "hurt_flash",
      duration: [0.2, 0.2],
      weight: 15,
      cooldown: 0.1,
      conditions: [
        { type: 'hurt', operator: '==', value: 1 }
      ],
      actions: [
        { type: 'animate', params: { name: 'Hurt' } },
        { type: 'move', params: { pattern: 'stop', speed: 0 } }
      ]
    },
    {
      name: "hurt_shell",
      duration: [1.0, 1.0],
      weight: 15,
      cooldown: 5.0,
      conditions: [
        { type: 'distance', operator: '<', value: 999 }
      ],
      actions: [
        { type: 'animate', params: { name: 'Retreat_${direction}' } },
        { type: 'move', params: { pattern: 'stop', speed: 0 } },
        { type: 'invulnerable', params: { duration: 1.0 } }
      ]
    },
    {
      name: "attack",
      duration: [0.8, 1.2],
      weight: 10,
      cooldown: 1.5,
      conditions: [
        { type: 'distance', operator: '<', value: 32 }
      ],
      actions: [
        { type: 'animate', params: { name: 'Attack_${direction}' } },
        { type: 'move', params: { pattern: 'toward_player', speed: 40 } },
        { type: 'sound', params: { name: 'CrabAttack' } }
      ]
    }
  ]
};

// ===== CONFIG GETTER FUNCTION =====
export function getEnemyConfig(enemyType: string): EnemyConfig | null {
  switch (enemyType.toLowerCase()) {
    case "crab":
      return CRAB_CONFIG;
    case "ooze":
    case "slime":
      return OOZE_CONFIG;
    default:
      console.error(`❌ Unknown enemy type: ${enemyType}`);
      return null;
  }
}