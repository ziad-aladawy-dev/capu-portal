import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  fetchStudentFees, fetchStudentOrders, fetchOrder,
  createOrder, cancelOrder, ORDER_TERMINAL,
} from "../../../core/services/treasuryPaymentService";

const STALE = 30_000;

export function useStudentFees(studentId) {
  return useQuery({
    queryKey: ["payments", "fees", studentId],
    enabled: Boolean(studentId),
    staleTime: STALE,
    queryFn: () => fetchStudentFees(studentId),
  });
}

export function useStudentOrders(studentId) {
  return useQuery({
    queryKey: ["payments", "orders", studentId],
    enabled: Boolean(studentId),
    staleTime: STALE,
    queryFn: () => fetchStudentOrders(studentId),
  });
}

/**
 * Polls a single order until it reaches a terminal state. Used by the payment
 * return page to detect webhook settlement after the student comes back from
 * the gateway.
 */
export function useOrderPolling(orderId, { enabled = true } = {}) {
  return useQuery({
    queryKey: ["payments", "order", orderId],
    enabled: Boolean(orderId) && enabled,
    queryFn: () => fetchOrder(orderId),
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      return status != null && ORDER_TERMINAL.includes(status) ? false : 3000;
    },
  });
}

export function useCreateOrder() {
  return useMutation({ mutationFn: createOrder });
}

export function useCancelOrder(studentId) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: cancelOrder,
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["payments", "orders", studentId] });
      qc.invalidateQueries({ queryKey: ["payments", "fees", studentId] });
    },
  });
}
