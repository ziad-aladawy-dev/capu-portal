import { useEffect, useRef, useState } from "react";

/**
 * Animates a number from 0 to `target` over `duration` ms using
 * requestAnimationFrame. Respects prefers-reduced-motion (jumps straight to the
 * target). Returns the current display value.
 */
export function useCountUp(target, { duration = 1000 } = {}) {
  const [value, setValue] = useState(0);
  const rafRef = useRef(0);

  useEffect(() => {
    const end = Number(target) || 0;

    const reduce =
      typeof window !== "undefined" &&
      window.matchMedia &&
      window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    if (reduce || end === 0) {
      setValue(end);
      return undefined;
    }

    let startTs = null;
    const step = (ts) => {
      if (startTs === null) startTs = ts;
      const progress = Math.min(1, (ts - startTs) / duration);
      // easeOutCubic
      const eased = 1 - Math.pow(1 - progress, 3);
      setValue(end * eased);
      if (progress < 1) {
        rafRef.current = requestAnimationFrame(step);
      } else {
        setValue(end);
      }
    };

    rafRef.current = requestAnimationFrame(step);
    return () => cancelAnimationFrame(rafRef.current);
  }, [target, duration]);

  return value;
}
