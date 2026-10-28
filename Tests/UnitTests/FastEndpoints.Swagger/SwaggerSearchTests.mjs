// Run with: node --test Tests/UnitTests/FastEndpoints.Swagger/SwaggerSearchTests.mjs
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import { runInNewContext } from 'node:vm';

const source = readFileSync(new URL('../../../Src/Swagger/Extensions.cs', import.meta.url), 'utf8');
const script = source.match(/<script>([\s\S]*?)<\/script>/)[1];

function start({ uiReady = true, inputReady = false, alias = 'ui' } = {}) {
    let now = 0;
    let input = inputReady ? new EventTarget() : null;
    const timers = new Map();
    const observers = new Set();
    const document = new EventTarget();
    document.body = {};
    document.querySelector = () => input;
    const taggedOps = { pets: { operations: [{ path: '/pets', operation: { summary: 'Find animals' } }] } };
    const state = { toJS: () => ({ taggedOps }), update: (key, update) => { state.filtered = update(); } };
    const system = { fn: {}, Im: { fromJS: value => value }, getState: () => state };
    const window = uiReady ? { [alias]: { getSystem: () => system } } : {};
    const context = {
        document, window,
        setTimeout(callback, delay) {
            const id = Symbol();
            timers.set(id, { callback, due: now + delay });
            return id;
        },
        clearTimeout: id => timers.delete(id),
        MutationObserver: class {
            constructor(callback) { this.callback = callback; }
            observe() { observers.add(this); }
            disconnect() { observers.delete(this); }
        }
    };
    runInNewContext(script, context);
    document.dispatchEvent(new Event('DOMContentLoaded'));
    return {
        system, state, window, timers, observers,
        get input() { return input; },
        renderInput() {
            input = new EventTarget();
            this.mutate();
        },
        mutate() { for (const observer of observers) observer.callback(); },
        advance(milliseconds) {
            const end = now + milliseconds;
            while (timers.size) {
                const [id, timer] = [...timers].sort((a, b) => a[1].due - b[1].due)[0];
                if (timer.due > end) break;
                now = timer.due;
                timers.delete(id);
                timer.callback();
            }
            now = end;
        }
    };
}

test('search works before the asynchronously rendered input exists', () => {
    const page = start();
    const result = page.system.fn.opsFilter({
        pets: { operations: [{ path: '/pets', operation: { summary: 'Find animals' } }] }
    }, 'animals');
    assert.equal(result.pets.operations.length, 1);
    assert.equal(page.observers.size, 1);
    page.mutate(); // Unrelated rendering must not stop waiting.
    page.renderInput();
    assert.equal(page.input.placeholder, 'Search...');
    assert.equal(page.observers.size, 0);
    assert.equal(page.timers.size, 0);
});

test('an already rendered input is initialized without starting a wait', () => {
    const page = start({ inputReady: true });
    assert.equal(page.input.placeholder, 'Search...');
    assert.equal(page.observers.size, 0);
    assert.equal(page.timers.size, 0);
});

test('the input listener is preserved and attached only once', () => {
    const page = start();
    page.renderInput();
    page.mutate();
    page.mutate();
    let updates = 0;
    const update = page.state.update;
    page.state.update = (...args) => { updates++; update(...args); };
    page.input.value = 'animals';
    page.input.dispatchEvent(new Event('input'));
    assert.equal(updates, 1);
    assert.equal(page.state.filtered.pets.operations.length, 1);
    page.input.value = 'missing';
    page.input.dispatchEvent(new Event('input'));
    assert.equal(Object.keys(page.state.filtered).length, 0);
});

test('a delayed UI initializes through either supported global name', () => {
    for (const alias of ['ui', 'swaggerUi']) {
        const page = start({ uiReady: false, inputReady: true });
        page.window[alias] = { getSystem: () => page.system };
        page.advance(250);
        assert.equal(typeof page.system.fn.opsFilter, 'function');
        assert.equal(page.input.placeholder, 'Search...');
        assert.equal(page.timers.size, 0);
    }
});

test('missing or disabled input stops being observed after 30 seconds', () => {
    const page = start();
    page.advance(30000);
    assert.equal(page.observers.size, 0);
    assert.equal(page.timers.size, 0);
    assert.equal(typeof page.system.fn.opsFilter, 'function');
});

test('a UI that never loads stops being polled after 30 seconds', () => {
    const page = start({ uiReady: false });
    page.advance(30000);
    assert.equal(page.observers.size, 0);
    assert.equal(page.timers.size, 0);
});
