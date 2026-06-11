import "@testing-library/jest-dom";

// Node >= 22 defines its own `localStorage`/`sessionStorage` globals, which are
// non-functional stubs unless node is started with --localstorage-file. Vitest's
// jsdom environment only overrides globals on its known-keys list (localStorage
// isn't on it), so Node's stub shadows jsdom's real Storage and calls like
// localStorage.clear() blow up. Swap in a working in-memory Storage when the
// ambient one is broken. See vitest's populateGlobal/getWindowKeys.
function ensureWorkingStorage(name) {
  const existing = globalThis[name];
  if (existing && typeof existing.clear === "function") return;

  const backing = new Map();
  const storage = {
    get length() {
      return backing.size;
    },
    key(index) {
      return [...backing.keys()][index] ?? null;
    },
    getItem(key) {
      key = String(key);
      return backing.has(key) ? backing.get(key) : null;
    },
    setItem(key, value) {
      backing.set(String(key), String(value));
    },
    removeItem(key) {
      backing.delete(String(key));
    },
    clear() {
      backing.clear();
    },
  };
  Object.defineProperty(globalThis, name, {
    value: storage,
    writable: true,
    configurable: true,
  });
}

ensureWorkingStorage("localStorage");
ensureWorkingStorage("sessionStorage");
