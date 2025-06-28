// enemy-configs.ts - Enemy Configuration Data with Extended Types

// ===== TYPE DEFINITIONS =====
export interface BehaviorCondition {
  type: "distance" | "health" | "timer" | "random" | "hurt";
  operator: "<" | ">" | "<=" | ">=" | "==";
  value: number;
}

// Extended ActionConfig to support all action types from enemy-ai.ts
export interface ActionConfig {
  type: "move" | "animate" | "sound" | "invulnerable" | "set_effect";
  params: {
    // Movement params
    pattern?: "toward_player" | "away_from_player" | "random" | "stop" | "sideways_left" | "sideways_right" | "crab_toward_player";
    speed?: number;

    // Animation params
    name?: string;

    // Invulnerability params
    duration?: number;

    // Sound params
    sound?: string;

    // Effect params
    effect?: string;
    parameter?: string;
    value?: number;
    enabled?: boolean;
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

// Helper function to get enemy config
export function getEnemyConfig(type: string): EnemyConfig | null {
  const configs: { [key: string]: EnemyConfig } = {
    "Ooze": OOZE_CONFIG,
    "Crab": CRAB_CONFIG
  };
  return configs[type] || null;
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
        { type: 'animate', params: { name: 'Idle_{direction}' } },
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
        { type: 'animate', params: { name: 'Hop_{direction}' } },
        { type: 'move', params: { pattern: 'toward_player', speed: 50 } },
        { type: 'sound', params: { sound: 'Slime_Jump' } }
      ]
    },
    {
      name: "hurt",
      duration: [0.5, 0.5],
      weight: 0,
      conditions: [
        { type: 'hurt', operator: '==', value: 1 }
      ],
      actions: [
        { type: 'animate', params: { name: 'Hurt_{direction}' } },
        { type: 'move', params: { pattern: 'stop' } },
        { type: 'invulnerable', params: { duration: 1.0 } }
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
      duration: [2.0, 4.0],
      weight: 3,
      actions: [
        { type: 'animate', params: { name: 'Idle_{direction}' } },
        { type: 'move', params: { pattern: 'stop' } }
      ]
    },
    {
      name: "cranky_chase",
      duration: [1.5, 3.0],
      weight: 6,
      conditions: [
        { type: 'distance', operator: '<', value: 150 }
      ],
      actions: [
        { type: 'animate', params: { name: 'Walk_{sideways}' } },
        { type: 'move', params: { pattern: 'crab_toward_player', speed: 35 } }
      ]
    },
    {
      name: "hurt_flash",
      duration: [0.2, 0.2],
      weight: 0,
      conditions: [
        { type: 'hurt', operator: '==', value: 1 }
      ],
      actions: [
        { type: 'animate', params: { name: 'Hurt_{direction}' } },
        { type: 'move', params: { pattern: 'stop' } },
        { type: 'set_effect', params: { effect: 'SetColor', parameter: 'brightness', value: 2.0 } }
      ]
    },
    {
      name: "hurt_shell",
      duration: [1.0, 2.0],
      weight: 15,
      cooldown: 5.0,
      conditions: [
        { type: 'hurt', operator: '==', value: 0 },
        { type: 'distance', operator: '<', value: 100 }
      ],
      actions: [
        { type: 'animate', params: { name: 'Hurt_{direction}' } },
        { type: 'move', params: { pattern: 'away_from_player', speed: 10 } },
        { type: 'invulnerable', params: { duration: 2.0 } },
        { type: 'set_effect', params: { effect: 'SetColor', enabled: false } }
      ]
    },
    {
      name: "sideways_dodge",
      duration: [0.8, 1.2],
      weight: 4,
      cooldown: 2.0,
      conditions: [
        { type: 'distance', operator: '<', value: 80 },
        { type: 'random', operator: '<', value: 30 }
      ],
      actions: [
        { type: 'animate', params: { name: 'Walk_{sideways}' } },
        { type: 'move', params: { pattern: 'sideways_left', speed: 50 } }
      ]
    }
  ]
};