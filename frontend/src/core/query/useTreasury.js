import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import * as treasuryService from "../services/treasuryService";
import { ORDER_STATUS } from "../services/treasuryService";

const RECEIPTS_KEY = ["treasury", "receipts"];
const LIVE_RECEIPTS_KEY = ["treasury", "receipts", "live"];
const FEES_KEY = ["treasury", "fees"];
const ORDERS_KEY = ["treasury", "orders"];
const MAPPINGS_KEY = ["treasury", "mappings"];

/* ────────────────────────────────────────────────────────────────
   Receipts
   ──────────────────────────────────────────────────────────────── */

export function useReceipts(options = {}) {
  return useQuery({
    queryKey: RECEIPTS_KEY,
    queryFn: () => treasuryService.fetchReceipts(),
    select: (data) => (Array.isArray(data) ? data : []),
    staleTime: 5 * 60 * 1000, // synced reference data, refreshed by a 6h cron
    ...options,
  });
}

/** Live Treasury feed — fetched on demand only (enabled: false by default). */
export function useLiveReceipts(options = {}) {
  return useQuery({
    queryKey: LIVE_RECEIPTS_KEY,
    queryFn: () => treasuryService.fetchLiveReceipts(),
    select: (data) => (Array.isArray(data) ? data : []),
    enabled: false,
    gcTime: 0,
    ...options,
  });
}

/** Map of receiptId (Guid) → receipt, for resolving fee display names. */
export function buildReceiptIndex(receipts) {
  const index = {};
  for (const r of receipts || []) index[r.id] = r;
  return index;
}

/* ────────────────────────────────────────────────────────────────
   Student fees
   ──────────────────────────────────────────────────────────────── */

export function useUnpaidFees(studentId, options = {}) {
  return useQuery({
    queryKey: [...FEES_KEY, studentId],
    queryFn: () => treasuryService.fetchUnpaidFees(studentId),
    select: (data) => (Array.isArray(data) ? data : []),
    enabled: !!studentId,
    ...options,
  });
}

/* ────────────────────────────────────────────────────────────────
   Orders
   ──────────────────────────────────────────────────────────────── */

export function useStudentOrders(studentId, options = {}) {
  return useQuery({
    queryKey: [...ORDERS_KEY, "by-student", studentId],
    queryFn: () => treasuryService.fetchOrdersForStudent(studentId),
    select: (data) => {
      const orders = Array.isArray(data) ? data : [];
      return [...orders].sort(
        (a, b) => new Date(b.createdAt) - new Date(a.createdAt)
      );
    },
    enabled: !!studentId,
    ...options,
  });
}

/**
 * Single order with live tracking: while the order awaits gateway settlement
 * (PendingPayment) the query re-polls so webhook/reconciliation outcomes show
 * up without a manual refresh.
 */
export function useOrder(orderId, { pollMs = 5000, ...options } = {}) {
  return useQuery({
    queryKey: [...ORDERS_KEY, orderId],
    queryFn: () => treasuryService.fetchOrder(orderId),
    enabled: !!orderId,
    refetchInterval: (query) =>
      query.state.data?.status === ORDER_STATUS.PendingPayment ? pollMs : false,
    ...options,
  });
}

export function useCreateOrder() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body) => treasuryService.createOrder(body),
    onSuccess: (order) => {
      qc.setQueryData([...ORDERS_KEY, order.id], order);
      qc.invalidateQueries({ queryKey: ORDERS_KEY });
      // Selected fees moved Pending → IncludedInOrder; refresh the pool.
      qc.invalidateQueries({ queryKey: FEES_KEY });
    },
    onError: () => {
      // A 409 usually means a fee was claimed concurrently — resync the pool.
      qc.invalidateQueries({ queryKey: FEES_KEY });
    },
  });
}

export function useCancelOrder() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id) => treasuryService.cancelOrder(id),
    onSettled: () => {
      qc.invalidateQueries({ queryKey: ORDERS_KEY });
      qc.invalidateQueries({ queryKey: FEES_KEY });
    },
  });
}

export function useInitiatePayment() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, redirectUrl }) => treasuryService.initiatePayment(id, redirectUrl),
    onSettled: () => {
      qc.invalidateQueries({ queryKey: ORDERS_KEY });
    },
  });
}

/** Call when a tracked order settles (Paid/Failed/Expired) to resync lists. */
export function useInvalidateTreasury() {
  const qc = useQueryClient();
  return () => {
    qc.invalidateQueries({ queryKey: ORDERS_KEY });
    qc.invalidateQueries({ queryKey: FEES_KEY });
  };
}

/* ────────────────────────────────────────────────────────────────
   Service → receipt mappings
   ──────────────────────────────────────────────────────────────── */

export function useServiceMappings(options = {}) {
  return useQuery({
    queryKey: MAPPINGS_KEY,
    queryFn: () => treasuryService.fetchServiceMappings(),
    select: (data) => (Array.isArray(data) ? data : []),
    ...options,
  });
}

export function useCreateServiceMapping() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body) => treasuryService.createServiceMapping(body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: MAPPINGS_KEY });
    },
  });
}

export function useDeactivateServiceMapping() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id) => treasuryService.deactivateServiceMapping(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: MAPPINGS_KEY });
    },
  });
}
