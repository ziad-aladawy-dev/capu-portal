# Frontend Audit: Landing Module
**Date:** 2025-05-24
**Auditor:** Senior Frontend Architect
**Status:** Deficient - Not Production Ready

## 1. State, Context & Persistence
- **State Management:** Uses localized `useState` for UI concerns (Hero slider index, scroll state, mobile menu toggle).
- **Persistence:** No persistence implemented. This is acceptable for a landing page, but some preferences (like "don't show CTA again") are missing.
- **Refresh Resilience:** Hard refresh resets the Hero slider to index 0 and scrolls to top. Consistent with expected landing page behavior.
- **Efficiency:** `CountUp.jsx` and `Reveal.jsx` each create a new `IntersectionObserver` per instance. On a page with many stats or revealed elements, this can lead to performance overhead. A shared observer pattern or a more optimized hook would be preferred.

## 2. Error Handling & Resilience
- **Try/Catch Blocks:** Completely absent in `LandingPage.jsx` and all sub-components. 
- **API Resilience:** Currently, news and stats are loaded from static data (`landingData.js`). If these were migrated to API calls, the module lacks any error handling or fallback UI for fetch failures.
- **Error Boundaries:** No Error Boundary wrapping the Landing module. A crash in `HeroSlider` or `CountUp` would take down the entire page.

## 3. Architecture & Wiring
- **Dead/Mock Links:** 
    - `LandingNavbar.jsx`: All nav links (Home, About, Faculties, Admissions, Services, News, Contact) are static `<span>` tags with no `onClick` or `href`. They are strictly visual and non-functional.
    - `LandingFooter.jsx`: Social links and most Quick Links use `href="#"` or unrouted anchors like `#about`.
- **Hardcoded Routes:** `navigate("/admin/login")` is hardcoded in 4 different places across `LandingNavbar`, `HeroSlider`, and `CTASection`. These should be centralized in a routes constant file.
- **Component Coupling:** High dependency on `landingData.js`. The components are not truly reusable as they are tightly coupled to the specific structure of that data file.

## 4. Code Quality & Tech Debt
- **Hardcoded Strings & i18n:** 
    - `LandingPage.jsx` imports `useTranslation` but only uses it to initialize `t`. It doesn't actually translate anything.
    - All text in `LandingNavbar`, `HeroSlider`, `FacultiesSection`, `ServicesSection`, `NewsSection`, `CTASection`, and `LandingFooter` is hardcoded in English.
    - `landingData.js` contains the bulk of the content, all hardcoded. This prevents multi-language support which is a core requirement of the application (indicated by the `i18next` import).
- **Mock Data:** `landingData.js` contains production-looking mock data. For a "Production Ready" state, dynamic content like `news` and `stats` should be fetched from the backend.
- **Accessibility (a11y):**
    - `LandingNavbar`: Mobile menu button and close button lack descriptive `aria-label`.
    - `HeroSlider`: Slider dots are buttons but lack `aria-label` or `aria-current`.
- **Performance:** 
    - Images in `landingData.js` (e.g., `/images/University1.png`) are referenced directly. No optimization strategy (WebP, srcset, or lazy loading) is evident for these large hero images.
    - `HeroSlider` uses a 4.5s `setInterval`. If the tab is backgrounded, this continues to run (though `setInterval` is throttled by browsers, it's better to use `requestAnimationFrame` or check for visibility).

## 5. File-by-File Specific Flaws
- **`LandingNavbar.jsx`**: `isMobile` check `windowWidth < 768` is hardcoded. Should use theme breakpoints.
- **`HeroSlider.jsx`**: Logic for `prevSlide` uses `(prev - 1 + slides.length) % slides.length`. While correct, the `key={currentSlide}` on `hero-content` forces a full re-mount of the text block on every slide change, which might cause "flicker" if not handled by CSS transitions.
- **`CountUp.jsx`**: `duration / 16` assumes 60fps. This is a "magic number". Uses `setInterval` for animation instead of `requestAnimationFrame`.
- **`LandingFooter.jsx`**: Hardcoded year in copyright (Wait, it uses `new Date().getFullYear()`, that's actually okay, but the "since 1995" is hardcoded).

## Recommendation
Immediate refactor required to:
1. Implement full i18n support.
2. Connect `News` and `Stats` to real API endpoints with proper error handling.
3. Fix non-functional navigation links.
4. Centralize routing paths.
5. Improve accessibility and image loading performance.
