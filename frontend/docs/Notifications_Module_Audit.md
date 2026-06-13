# Frontend Audit: Notifications Module
**Date:** 2025-05-24
**Auditor:** Senior Frontend Architect
**Status:** Sub-optimal - Requires Refactoring

## 1. State, Context & Persistence
- **Local State Overload:** `NotificationsPage.jsx` manages `items`, `loading`, `error`, `marking` all in local state. 
- **Lack of Sync:** If a notification is read here, the "Global Bell Icon" (usually in the TopBar) will not update unless a global state (Zustand/Redux) or an EventBus/Socket is used.
- **Hard Refresh:** Triggers a full API reload. No client-side caching (like React Query) is implemented.

## 2. Error Handling & Resilience
- **API Failures:** `try/catch` is present in `load` and `handleMarkRead`. However, the error is just a string set to state.
- **Error Banner:** The error banner is a simple `div`. It's not a global toast or a robust notification system.
- **Race Conditions:** `handleMarkRead` sets `marking(id)` to disable the button, which is good. However, if multiple notifications are marked read in rapid succession, the `items` state update might suffer from race conditions (though the functional update `setItems(prev => ...)` mitigates this).

## 3. Architecture & Wiring
- **Suboptimal Service Usage:** 
    - `handleMarkAllRead` implementation:
      ```javascript
      await Promise.all(unread.map(n => notificationService.markNotificationRead(n.id)));
      ```
    - **Issue:** This sends N network requests to the server. 
    - **Fact:** `notificationService.js` contains `markAllNotificationsRead()` and `markManyNotificationsRead(ids)`, but these are ignored in the UI component. This is a significant architectural "miss".
- **Formatting Logic:** `formatTime` is defined locally in the component. This should be a utility function (or use `date-fns`/`dayjs` which are standard in modern projects).
- **Component Bloat:** The entire page is one large component. The notification item should be its own memoized component (`NotificationItem.jsx`) to prevent re-rendering the whole list when one item is marked read.

## 4. Code Quality & Tech Debt
- **Hardcoded Constants:**
    - `TYPE_ICON` uses `1, 2, 3`. 
    - It should use the exported `NOTIFICATION_TYPE` from `notificationService.js` (e.g., `[NOTIFICATION_TYPE.Info]: Info`).
- **Translation Gaps:**
    - `notificationService.getNotificationTypeLabel(n.type)` returns hardcoded strings ("Info", "Warning", "Error") from the service, bypassing the `i18next` system. These won't be translated.
    - Reference types (e.g., `n.referenceType`) are displayed raw.
- **Inline Styles:** 
    - `NotificationsPage.jsx` contains multiple inline styles (e.g., `style={{ marginLeft: "auto", ... }}`). These should be moved to `notifications.css`.
- **Accessibility (a11y):**
    - Notification items are `div`s. They should probably be `article` or `li` within a `ul` for screen readers.
    - Buttons like "Mark as Read" should have more descriptive labels for screen readers (e.g., "Mark [Title] as read").
- **Performance:**
    - `unreadCount` is a `useMemo` based on `items`. This is good.
    - However, every time `tab` changes, the entire list is re-fetched (`useEffect` depends on `load` which depends on `tab`). This causes a "flicker" and unnecessary network traffic when switching back and forth between "All" and "Unread".

## 5. File-by-File Specific Flaws
- **`routes.js`**: `applicableTo: "both"` is used. This is fine for the current architecture, but the icon is a hardcoded string `"Bell"`.
- **`NotificationsPage.jsx`**:
    - The `TYPE_ICON` mapping is missing a fallback for unexpected types.
    - `setItems(Array.isArray(data) ? data : [])` - The API should ideally be guaranteed to return an array, but the defensive check is okay. However, it indicates a lack of trust in the service layer.
    - `handleMarkAllRead` calls `load()` after finishing. This causes yet another network request instead of updating the local state optimistically or using the result of the `markAll` call.

## Recommendation
1. **Refactor `handleMarkAllRead`** to use `notificationService.markAllNotificationsRead()`.
2. **Move `formatTime`** to a shared utility.
3. **Extract `NotificationItem`** into a separate component.
4. **Fix i18n** for notification type labels.
5. **Implement optimistic updates** for marking as read to improve perceived performance.
6. **Move inline styles** to CSS.
