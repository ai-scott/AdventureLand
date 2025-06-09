// Enemy AI - Helper functions for use in Event Sheet TypeScript blocks
// This file provides utilities that can be used in "Add TypeScript" actions

// Store enemy data globally
if (!(globalThis as any).enemyAIData) {
  (globalThis as any).enemyAIData = new Map();
}

// Helper functions available to event sheet TypeScript blocks
(globalThis as any).EnemyAI = {
  
  // Initialize an enemy
  init(enemyUid: number, maskUid: number, enemyType: string, direction = "Down") {
    const enemyData = (globalThis as any).enemyAIData;
    enemyData.set(enemyUid, {
      maskUid: maskUid,
      type: enemyType,
      state: "Idle",
      stateTimer: Math.floor(Math.random() * 60),
      direction: direction
    });
    
    console.log(`Initialized ${enemyType} AI: enemy=${enemyUid}, mask=${maskUid}`);
    return true;
  },
  
  // Update enemy AI and return action string
  update(enemyUid: number, playerX: number, playerY: number, enemyX: number, enemyY: number): string {
    const enemyData = (globalThis as any).enemyAIData;
    const data = enemyData.get(enemyUid);
    if (!data) return "none";
    
    // Decrement timer
    data.stateTimer = Math.max(0, data.stateTimer - 1);
    
    if (data.type === "Ooze") {
      return this.updateOoze(data, playerX, playerY, enemyX, enemyY);
    } else if (data.type === "Crab") {
      return this.updateCrab(data, playerX, playerY, enemyX, enemyY);
    }
    
    return "none";
  },
  
  updateOoze(data: any, playerX: number, playerY: number, enemyX: number, enemyY: number): string {
    if (data.state === "Idle" && data.stateTimer <= 0) {
      data.state = "Hop";
      data.stateTimer = 30;
      
      const angle = Math.atan2(playerY - enemyY, playerX - enemyX);
      const speed = 150;
      
      return `hop|${data.maskUid}|Hop_${data.direction}|${Math.cos(angle) * speed}|${Math.sin(angle) * speed}`;
    }
    else if (data.state === "Hop" && data.stateTimer <= 0) {
      data.state = "Idle";
      data.stateTimer = 30 + Math.floor(Math.random() * 30);
      
      return `idle|${data.maskUid}|Idle_${data.direction}|0|0`;
    }
    
    return "none";
  },
  
  updateCrab(data: any, playerX: number, playerY: number, enemyX: number, enemyY: number): string {
    const distance = Math.sqrt(Math.pow(playerX - enemyX, 2) + Math.pow(playerY - enemyY, 2));
    
    if (data.state === "Idle" && data.stateTimer <= 0) {
      if (distance < 32) {
        data.state = "Attack";
        data.stateTimer = 60;
        return `attack|${data.maskUid}|Attack_${data.direction}|0|0`;
      } else if (distance < 120) {
        data.state = "Walk";
        data.stateTimer = 30 + Math.floor(Math.random() * 30);
        
        const angle = Math.atan2(playerY - enemyY, playerX - enemyX);
        const angleDeg = ((angle * 180 / Math.PI) + 360) % 360;
        
        if (angleDeg >= 315 || angleDeg < 45) data.direction = "Right";
        else if (angleDeg >= 45 && angleDeg < 135) data.direction = "Down";
        else if (angleDeg >= 135 && angleDeg < 225) data.direction = "Left";
        else data.direction = "Up";
        
        return `walk|${data.maskUid}|Walk_${data.direction}|${Math.cos(angle) * 20}|${Math.sin(angle) * 20}|${data.direction}`;
      } else {
        data.state = "Walk";
        data.stateTimer = 30 + Math.floor(Math.random() * 30);
        
        const directions = ["Up", "Down", "Left", "Right"];
        data.direction = directions[Math.floor(Math.random() * directions.length)];
        
        let vx = 0, vy = 0;
        if (data.direction === "Up") { vx = 0; vy = -20; }
        else if (data.direction === "Down") { vx = 0; vy = 20; }
        else if (data.direction === "Left") { vx = -20; vy = 0; }
        else if (data.direction === "Right") { vx = 20; vy = 0; }
        
        return `walk|${data.maskUid}|Walk_${data.direction}|${vx}|${vy}|${data.direction}`;
      }
    }
    else if ((data.state === "Walk" || data.state === "Attack") && data.stateTimer <= 0) {
      data.state = "Idle";
      data.stateTimer = 12 + Math.floor(Math.random() * 30);
      return `idle|${data.maskUid}|Idle_${data.direction}|0|0`;
    }
    
    return "none";
  },
  
  getState(enemyUid: number): string {
    const enemyData = (globalThis as any).enemyAIData;
    const data = enemyData.get(enemyUid);
    return data ? data.state : "Unknown";
  },
  
  debug(): number {
    const enemyData = (globalThis as any).enemyAIData;
    console.log("Enemy data:", Array.from(enemyData.entries()));
    return enemyData.size;
  }
};

console.log("EnemyAI helper functions loaded");