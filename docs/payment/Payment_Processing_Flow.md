# Payment Processing Flow

## Overview

The Portal delegates payment execution to the HU Treasury System.

HU Treasury is responsible for:

* Payment session creation
* Payment gateway integration
* Payment status tracking
* Refund execution
* Payment notifications

The Portal is responsible for:

* Fee generation
* Order creation
* Payment recording
* Student-facing payment workflows

---

# Fee Generation Flow

1. Student requests a service.
2. Service Receipt Mapping is resolved.
3. Associated Treasury Receipt is identified.
4. Fee amount is calculated using the receipt amount.
5. Student Fee is created.

Example:

Student registers 18 credit hours.

Receipt:

Credit Hour = 500 EGP

Generated Fee:

18 × 500 = 9000 EGP

Fee Status:

Pending

---

# Order Creation Flow

1. Student views unpaid fees.
2. Student selects fees to pay.
3. Portal validates fees.
4. Portal creates Order.
5. Selected fees are linked to the order.

Order Status:

Created

---

# Payment Session Creation

The Portal initiates payment through HU Treasury.

Supported gateways:

* Mastercard
* Bank Misr
* eFinance

The Portal collects all receipt identifiers represented by the selected fees.

The appropriate initiation endpoint is called.

Examples:

POST /api/payments/mastercard/initiate

POST /api/payments/bm/initiate

POST /api/payments/eFinance/initiate

Request contains:

* ReceiptIds
* StudentReferenceId
* Billing Details (when required)
* Redirect URL

---

# Treasury Response

HU Treasury creates a payment session and returns:

* MerchantOrderId
* RedirectUrl
* Session Information

The Portal stores:

* MerchantOrderId
* Gateway
* Gateway Session Information

Order Status becomes:

PendingPayment

Student is redirected to the payment page.

---

# Payment Execution

The student completes payment using the selected gateway.

Flow:

Student
→ Gateway

Gateway
→ HU Treasury

HU Treasury
→ Updates Transaction Status

The Portal is not involved in payment execution.

---

# Payment Confirmation

Payment completion may be detected using:

## Webhook

Preferred mechanism.

HU Treasury sends a payment notification containing:

* MerchantOrderId
* Payment Status

## Status Verification

Fallback mechanism.

Portal calls:

GET /api/payments/mastercard/status/{merchantOrderId}

GET /api/payments/bm/status/{merchantOrderId}

GET /api/payments/efinance/status/{merchantOrderId}

This process is used when webhook delivery fails or reconciliation is required.

---

# Successful Payment Processing

When payment status becomes Paid:

1. Resolve Order using MerchantOrderId.
2. Load all fees belonging to the order.
3. Create one Payment record per fee.
4. Mark all fees as Paid.
5. Mark order as Paid.

Example:

Order contains:

* Fee A
* Fee B
* Fee C

Generated records:

Payment A
Payment B
Payment C

All linked to the same order.

---

# Idempotency

Webhook processing must be idempotent.

Receiving the same notification multiple times must not create duplicate payments.

Before creating a payment record:

Check if payment already exists for the fee.

If payment exists:

Ignore duplicate notification.

---

# Reconciliation Process

A scheduled reconciliation process should periodically verify unfinished orders.

Process:

1. Load orders in PendingPayment state.
2. Query HU Treasury status endpoint.
3. If status is Paid:

   * Execute successful payment processing.
4. If status remains unpaid:

   * Leave order unchanged.

This guarantees eventual consistency between the Portal and HU Treasury.

---

# Refund Flow

Refunds are currently supported through Mastercard.

Portal requests refund through:

POST /api/payments/mastercard/refund

Using:

* MerchantOrderId
* Refund Amount (optional)
* Reason (optional)

After successful refund:

* Refund transaction is recorded.
* Order status may be updated.
* Financial audit records are preserved.

Refund processing must never delete payment records.
No Edits are allowed after even 1 transaction on a fee
(use the same logic as close, open to edits in authorization but make it internal triggers no human can re open it)