/*
 * Shared framer-motion variants for the student portal. Import these instead
 * of redefining per page so motion stays consistent:
 *
 *   <motion.div variants={pageVariants} initial="initial" animate="enter" exit="exit">
 *   <motion.ul variants={staggerContainer} initial="hidden" animate="show">
 *     <motion.li variants={staggerItem}>…</motion.li>
 */

export const pageVariants = {
  initial: { opacity: 0, y: 12 },
  enter: {
    opacity: 1,
    y: 0,
    transition: { duration: 0.25, ease: "easeOut" },
  },
  exit: {
    opacity: 0,
    y: -8,
    transition: { duration: 0.15, ease: "easeIn" },
  },
};

export const cardVariants = {
  hidden: { opacity: 0, y: 16 },
  show: {
    opacity: 1,
    y: 0,
    transition: { duration: 0.3, ease: "easeOut" },
  },
};

export const staggerContainer = {
  hidden: {},
  show: {
    transition: { staggerChildren: 0.05 },
  },
};

export const staggerItem = {
  hidden: { opacity: 0, y: 12 },
  show: {
    opacity: 1,
    y: 0,
    transition: { duration: 0.25, ease: "easeOut" },
  },
};

export const modalVariants = {
  hidden: { opacity: 0, scale: 0.96 },
  show: {
    opacity: 1,
    scale: 1,
    transition: { duration: 0.2, ease: "easeOut" },
  },
  exit: {
    opacity: 0,
    scale: 0.96,
    transition: { duration: 0.15, ease: "easeIn" },
  },
};

/* Props spread onto motion buttons for a subtle tactile press. */
export const tapScale = {
  whileTap: { scale: 0.97 },
  whileHover: { scale: 1.02 },
};
