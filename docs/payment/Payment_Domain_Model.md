# Payment Domain Model

## Purpose

The Payment Module manages student financial obligations and integrates with the HU Treasury System for receipt management and payment processing.

The Portal does not own pricing information. All payable amounts originate from HU Treasury receipts.

---

# Core Concepts

## Treasury Receipt

A Treasury Receipt represents a billable item maintained by the HU Treasury System.

Receipts are retrieved from HU Treasury through:

```http
GET /api/payments/receipts
```

Only receipts belonging to the configured external financial source (ConnectionTypeId = 6) are relevant to the Portal.

Examples:

* Credit Hour
* Transcript Fee
* Graduation Certificate Fee
* Enrollment Fee

Treasury remains the source of truth for receipt amounts.

---

## Service

A Service represents a business capability offered by the Portal.

Examples:

* Register Credit Hours
* Request Transcript
* Request Graduation Certificate

A Service does not contain pricing information.

Instead, each service is linked to a Treasury Receipt.

---

## Service Receipt Mapping

Defines which Treasury Receipt should be used when generating student fees.

Example:

Transcript Service
→ Transcript Receipt

Graduation Certificate Service
→ Graduation Certificate Receipt

Credit Hour Registration Service
→ Credit Hour Receipt

This allows Treasury to change prices without requiring application changes.

---

## Student Fee

A Student Fee represents a financial obligation assigned to a student.

A fee is generated when a student performs an action that requires payment.

Properties:

* FeeId
* StudentId
* ReceiptId
* Quantity
* UnitAmount
* TotalAmount
* Status

Statuses:

* Pending
* IncludedInOrder
* Paid
* Cancelled

---

# Quantity Based Fees

Treasury receipts represent a single billable unit.

The Portal may generate multiple units of the same receipt.

Example:

Receipt:

Credit Hour
Amount = 500 EGP

Student registers:

18 Credit Hours

Generated Fee:

Quantity = 18
UnitAmount = 500
TotalAmount = 9000

For display purposes the student sees:

Credit Hour Registration
18 × 500 EGP
= 9000 EGP

---

# Fee Aggregation

Multiple fees originating from the same receipt may be grouped in the user interface.

Grouping is a presentation concern only.

Payment processing operates on the actual fee records.

---

# Order

An Order represents a payment attempt created from one or more unpaid fees selected by the student.

Properties:

* OrderId
* StudentId
* Status
* Gateway
* MerchantOrderId

Statuses:

* Created
* PendingPayment
* Paid
* Failed
* Expired
* Refunded

An order may contain multiple fees.

A fee may belong to only one active order.

---

# Payment

A Payment represents successful settlement of a fee.

Properties:

* PaymentId
* FeeId
* OrderId
* Amount
* PaidAt

Rules:

* One payment record is created per paid fee.
* Payments are immutable after creation.

---

# Payment Transaction

Represents communication with HU Treasury payment gateways.

Properties:

* TransactionId
* OrderId
* MerchantOrderId
* Gateway
* Status
* GatewayReference
* RawResponse

Supported Gateways:

* Mastercard
* Bank Misr
* eFinance

This entity exists for auditability and troubleshooting and should not replace business-level Payment records.
