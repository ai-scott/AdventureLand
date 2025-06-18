// tests/basic.test.ts - Super simple test to verify ts-jest is working

describe('Basic Jest + TypeScript Test', () => {
  test('Jest is working', () => {
    expect(1 + 1).toBe(2);
  });

  test('TypeScript basic types work', () => {
    const message: string = 'Adventure Land';
    const count: number = 42;
    
    expect(message).toBe('Adventure Land');
    expect(count).toBe(42);
  });

  test('Arrays work', () => {
    const items: string[] = ['Crab', 'Ooze'];
    expect(items.length).toBe(2);
    expect(items[0]).toBe('Crab');
  });
});
