import { useEffect, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { Search, BookOpen, Info, AlertCircle, Star } from "lucide-react";
import * as courseService from "../../../core/services/courseService";
import PortalPageShell from "../components/shared/PortalPageShell";
import PortalCard from "../components/shared/PortalCard";
import PortalBadge from "../components/shared/PortalBadge";
import PortalSkeleton from "../components/shared/PortalSkeleton";
import PortalEmptyState from "../components/shared/PortalEmptyState";
import styles from "./CourseRegistration.module.css";

const WISHLIST_KEY = "capu_course_wishlist";

function loadWishlist() {
  try { return new Set(JSON.parse(localStorage.getItem(WISHLIST_KEY)) || []); } catch { return new Set(); }
}

/**
 * Read-only course catalog (browse + search + wishlist). Per the documented
 * Registration model the portal does NOT register students — enrollment
 * happens at the registrar — so this page is a catalog reference only.
 */
function CourseCatalog() {
  const { t } = useTranslation();
  const [term, setTerm] = useState("");
  const [debounced, setDebounced] = useState("");
  const [category, setCategory] = useState(null);
  const [wishlistOnly, setWishlistOnly] = useState(false);
  const [wishlist, setWishlist] = useState(loadWishlist);

  useEffect(() => {
    const id = setTimeout(() => setDebounced(term.trim()), 300);
    return () => clearTimeout(id);
  }, [term]);

  const toggleWish = (id) => {
    setWishlist((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id); else next.add(id);
      try { localStorage.setItem(WISHLIST_KEY, JSON.stringify([...next])); } catch { /* ignore */ }
      return next;
    });
  };

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ["course-catalog", debounced],
    staleTime: 60_000,
    queryFn: async () => {
      const res = await courseService.searchCourses(debounced ? { search: debounced, pageSize: 50 } : { pageSize: 50 });
      return Array.isArray(res) ? res : res?.items || [];
    },
  });

  const courses = useMemo(() => {
    let list = data || [];
    if (category != null) list = list.filter((c) => c.category === category);
    if (wishlistOnly) list = list.filter((c) => wishlist.has(c.id));
    return list;
  }, [data, category, wishlistOnly, wishlist]);

  return (
    <PortalPageShell
      title={t("portal_catalog.title", { defaultValue: "Course Catalog" })}
      subtitle={t("portal_catalog.subtitle", { defaultValue: "Browse courses and save the ones you're interested in" })}
      backTo="/student/courses"
    >
      <div className={styles.searchBar}>
        <Search size={15} />
        <input
          type="text"
          value={term}
          onChange={(e) => setTerm(e.target.value)}
          placeholder={t("portal_catalog.search_ph", { defaultValue: "Search by code, title, or keyword…" })}
        />
        {term && (
          <button type="button" className={styles.clearBtn} onClick={() => setTerm("")}>×</button>
        )}
      </div>

      <div className={styles.chips}>
        <button
          type="button"
          className={`${styles.chip} ${category == null && !wishlistOnly ? styles.chipActive : ""}`}
          onClick={() => { setCategory(null); setWishlistOnly(false); }}
        >
          {t("portal_catalog.all", { defaultValue: "All" })}
        </button>
        {courseService.COURSE_CATEGORIES.map((c) => (
          <button
            key={c.value}
            type="button"
            className={`${styles.chip} ${category === c.value ? styles.chipActive : ""}`}
            onClick={() => { setCategory(category === c.value ? null : c.value); setWishlistOnly(false); }}
          >
            {c.label}
          </button>
        ))}
        <button
          type="button"
          className={`${styles.chip} ${styles.wishChip} ${wishlistOnly ? styles.chipActive : ""}`}
          onClick={() => { setWishlistOnly((p) => !p); setCategory(null); }}
        >
          <Star size={11} /> {t("portal_catalog.saved", { defaultValue: "Saved" })} ({wishlist.size})
        </button>
      </div>

      <div className={styles.note}>
        <Info size={13} />
        {t("portal_catalog.readonly_note", { defaultValue: "This catalog is read-only. To register or drop a course, visit the registrar's office." })}
      </div>

      {isLoading ? (
        <div className={styles.grid}>
          {Array.from({ length: 6 }).map((_, i) => <PortalSkeleton key={i} variant="block" height={130} />)}
        </div>
      ) : isError ? (
        <PortalEmptyState
          icon={AlertCircle}
          title={t("portal_catalog.load_failed", { defaultValue: "Unable to load the catalog" })}
          onAction={() => refetch()}
          actionLabel={t("retry", { defaultValue: "Retry" })}
        />
      ) : courses.length === 0 ? (
        <PortalEmptyState
          icon={wishlistOnly ? Star : BookOpen}
          title={wishlistOnly
            ? t("portal_catalog.no_saved", { defaultValue: "No saved courses yet" })
            : t("portal_catalog.no_results", { defaultValue: "No courses found" })}
          text={debounced && !wishlistOnly
            ? t("portal_catalog.no_match", { defaultValue: "No matches for \"{{q}}\"", q: debounced })
            : undefined}
        />
      ) : (
        <div className={styles.grid}>
          {courses.map((c) => (
            <PortalCard key={c.id} interactive>
              <div className={styles.cardHead}>
                <h3 className={styles.cardTitle}>{c.title || c.name}</h3>
                <button
                  type="button"
                  className={`${styles.starBtn} ${wishlist.has(c.id) ? styles.starOn : ""}`}
                  onClick={() => toggleWish(c.id)}
                  aria-label={t("portal_catalog.toggle_save", { defaultValue: "Save course" })}
                >
                  <Star size={16} />
                </button>
              </div>
              <div className={styles.cardMeta}>
                <PortalBadge tone="primary">{c.code}</PortalBadge>
                {c.creditHours != null && (
                  <PortalBadge tone="accent">{c.creditHours} {t("portal_catalog.cr", { defaultValue: "cr" })}</PortalBadge>
                )}
                {c.category != null && (
                  <PortalBadge tone="neutral">{courseService.getCourseCategoryLabel(c.category)}</PortalBadge>
                )}
                {c.isClosed && <PortalBadge tone="danger">{t("portal_catalog.closed", { defaultValue: "Closed" })}</PortalBadge>}
              </div>
              {c.description && <p className={styles.desc}>{c.description}</p>}
            </PortalCard>
          ))}
        </div>
      )}
    </PortalPageShell>
  );
}

export default CourseCatalog;
