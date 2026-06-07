import { useState, useCallback, useEffect } from 'react';

const STORAGE_PREFIX = 'capu_colvis_';

function loadSavedKeys(tableId) {
  try {
    const raw = localStorage.getItem(`${STORAGE_PREFIX}${tableId}`);
    if (raw) return new Set(JSON.parse(raw));
  } catch {}
  return null;
}

function saveKeys(tableId, keys) {
  try {
    localStorage.setItem(`${STORAGE_PREFIX}${tableId}`, JSON.stringify([...keys]));
  } catch {}
}

export function useColumnVisibility(tableId, columnDefs) {
  const alwaysKeys = new Set(
    columnDefs.filter(col => col.always).map(col => col.key)
  );
  const toggleableDefs = columnDefs.filter(col => !col.always);
  const defaultKeys = new Set(toggleableDefs.map(col => col.key));

  const [visibleKeys, setVisibleKeys] = useState(() => {
    const saved = loadSavedKeys(tableId);
    return saved && saved.size > 0 ? saved : new Set(defaultKeys);
  });

  useEffect(() => {
    saveKeys(tableId, visibleKeys);
  }, [visibleKeys, tableId]);

  const isVisible = useCallback((key) => {
    if (alwaysKeys.has(key)) return true;
    return visibleKeys.has(key);
  }, [visibleKeys, alwaysKeys]);

  const toggle = useCallback((key) => {
    if (alwaysKeys.has(key)) return;
    setVisibleKeys(prev => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key); else next.add(key);
      return next;
    });
  }, [alwaysKeys]);

  const reset = useCallback(() => {
    setVisibleKeys(new Set(defaultKeys));
  }, [defaultKeys]);

  const orderedColumns = columnDefs
    .filter(col => alwaysKeys.has(col.key) || visibleKeys.has(col.key))
    .sort((a, b) => a.order - b.order);

  return { isVisible, toggle, reset, orderedColumns, visibleKeys };
}
