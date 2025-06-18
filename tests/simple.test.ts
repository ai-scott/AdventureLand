// tests/simple.test.ts - Minimal test to verify Jest + TypeScript working

describe('Jest + TypeScript Setup', () => {
    test('basic functionality works', () => {
        expect(1 + 1).toBe(2);
        console.log('✅ Jest is working');
    });

    test('TypeScript syntax works', () => {
        const testObj: { name: string; count: number } = {
            name: 'Adventure Land',
            count: 42
        };

        expect(testObj.name).toBe('Adventure Land');
        expect(testObj.count).toBe(42);
        console.log('✅ TypeScript syntax working in Jest');
    });

    test('can handle type assertions', () => {
        const value: unknown = 'hello';
        const str = value as string;

        expect(str).toBe('hello');
        expect(typeof str).toBe('string');
        console.log('✅ TypeScript type assertions working');
    });
});