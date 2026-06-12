import { useEffect, useState } from "react";
import { useSearchParams, Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { CheckCircle2, XCircle, Clock, Loader2 } from "lucide-react";
import { useOrderPolling } from "../hooks/usePayments";
import { ORDER_STATUS, ORDER_TERMINAL, fmtAmount } from "../../../core/services/treasuryPaymentService";
import "../styles/studentPayments.css";

const POLL_TIMEOUT_MS = 45_000;

function PaymentReturn() {
  const { t } = useTranslation();
  const [params] = useSearchParams();
  // Gateways may strip our query param — fall back to the stash set at checkout.
  const orderId = params.get("orderId") || localStorage.getItem("capu_pending_order");

  const { data: order, isLoading } = useOrderPolling(orderId);
  const [timedOut, setTimedOut] = useState(false);

  const terminal = order && ORDER_TERMINAL.includes(order.status);

  useEffect(() => {
    if (terminal) localStorage.removeItem("capu_pending_order");
  }, [terminal]);

  useEffect(() => {
    const id = setTimeout(() => setTimedOut(true), POLL_TIMEOUT_MS);
    return () => clearTimeout(id);
  }, []);

  let view;
  if (!orderId) {
    view = (
      <ReturnState icon={<XCircle size={48} />} cls="fail"
        title={t("portal_payments.return_no_order", { defaultValue: "No order to confirm" })}
        text={t("portal_payments.return_no_order_hint", { defaultValue: "We couldn't find a payment to confirm." })} />
    );
  } else if (order?.status === ORDER_STATUS.Paid) {
    view = (
      <ReturnState icon={<CheckCircle2 size={48} />} cls="ok"
        title={t("portal_payments.return_paid", { defaultValue: "Payment successful" })}
        text={t("portal_payments.return_paid_hint", { defaultValue: "Your fees have been paid. A receipt will be available shortly." })}
        amount={order.totalAmount} />
    );
  } else if (order && (order.status === ORDER_STATUS.Failed || order.status === ORDER_STATUS.Expired)) {
    view = (
      <ReturnState icon={<XCircle size={48} />} cls="fail"
        title={t("portal_payments.return_failed", { defaultValue: "Payment not completed" })}
        text={t("portal_payments.return_failed_hint", { defaultValue: "Your payment didn't go through. Your fees are still due — you can try again." })} />
    );
  } else if (order && (order.status === ORDER_STATUS.Cancelled || order.status === ORDER_STATUS.Refunded)) {
    view = (
      <ReturnState icon={<XCircle size={48} />} cls="fail"
        title={t("portal_payments.return_cancelled", { defaultValue: "Order closed" })}
        text={t("portal_payments.return_cancelled_hint", { defaultValue: "This order is no longer active." })} />
    );
  } else if (timedOut) {
    view = (
      <ReturnState icon={<Clock size={48} />} cls="pending"
        title={t("portal_payments.return_pending", { defaultValue: "Still processing" })}
        text={t("portal_payments.return_pending_hint", { defaultValue: "We haven't received confirmation yet. It may take a few minutes — check your Orders shortly." })} />
    );
  } else {
    view = (
      <ReturnState icon={<Loader2 size={48} className="sp-spin" />} cls="pending"
        title={t("portal_payments.return_confirming", { defaultValue: "Confirming your payment…" })}
        text={t("portal_payments.return_confirming_hint", { defaultValue: "Please wait while we verify with HU Treasury. Don't close this page." })} />
    );
  }

  return (
    <div className="student-payments">
      <div className="sp-return-wrap">
        {view}
        {!isLoading && (
          <Link to="/student/payments" className="sp-return-link">
            {t("portal_payments.back_to_payments", { defaultValue: "Back to Payments" })}
          </Link>
        )}
      </div>
    </div>
  );
}

function ReturnState({ icon, cls, title, text, amount }) {
  return (
    <div className={`sp-return-state ${cls}`}>
      <div className="sp-return-icon">{icon}</div>
      <h2>{title}</h2>
      {amount != null && <div className="sp-return-amount">{fmtAmount(amount)} EGP</div>}
      <p>{text}</p>
    </div>
  );
}

export default PaymentReturn;
