# HU Treasury System Payment APIs

## Overview

This collection provides a comprehensive set of APIs for managing payment operations within the HU Treasury System. It enables seamless integration with multiple payment gateways to process transactions, check payment statuses, handle refunds, and receive real-time payment notifications.

---

# Supported Payment Gateways

### Mastercard

Full-featured payment processing with initiation, status tracking, and refund capabilities.

### Bank Misr (BM)

Payment initiation and status tracking for the Bank Misr gateway.

### eFinance

Payment initiation and status tracking for the eFinance gateway.

---

# Main Capabilities

* **Payment Initiation** — Start new payment transactions through Mastercard, Bank Misr, or eFinance.
* **Status Checking** — Retrieve real-time payment status using merchant order IDs.
* **Refund Processing** — Process refunds for completed Mastercard transactions.
* **Webhook Handling** — Receive and process payment status notifications from payment gateways.

---

# Authentication

> Coming soon.

---

# Getting Started

1. Configure the `baseUrl` variable to point to your HU Treasury System environment.
2. Choose the appropriate payment gateway (Mastercard, Bank Misr, or eFinance).
3. Use the payment initiation endpoints to start transactions.
4. Monitor payment status using status endpoints.
5. Configure webhook endpoints to receive payment notifications.

---

# Filter Receipts

## Endpoint

```http
GET /api/payments/receipts
```

## Query Parameters

| Parameter         | Type      | Required | Description                                                      |
| ----------------- | --------- | -------- | ---------------------------------------------------------------- |
| connectionTypeIds | Integer[] | ✅ Yes    | List of connection type IDs to filter by (at least one required) |
| receiptTypeIds    | Integer[] | ❌ No     | List of receipt type IDs to filter by                            |

## Example Request

```http
GET /api/payments/receipts?connectionTypeIds=1,2&receiptTypeIds=1,3
```

---

# Bank Misr Gateway

## Initiate Payment

### Endpoint

```http
POST /api/payments/bm/initiate
```

### Request Body

| Field               | Type      | Required | Description                       |
| ------------------- | --------- | -------- | --------------------------------- |
| receiptIds          | Integer[] | ✅ Yes    | List of receipt IDs to pay for    |
| studentReferenceId  | String    | ✅ Yes    | Student unique reference ID       |
| billingDetails      | Object    | ✅ Yes    | Customer billing information      |
| merchantRedirectUrl | String    | ✅ Yes    | Custom redirect URL after payment |

### Billing Details

| Field        | Type   | Required | Description                       |
| ------------ | ------ | -------- | --------------------------------- |
| firstName    | String | ✅ Yes    | Customer first name               |
| lastName     | String | ✅ Yes    | Customer last name                |
| emailAddress | String | ✅ Yes    | Valid email address               |
| mobileNumber | String | ✅ Yes    | Customer mobile number            |
| state        | String | ❌ No     | State/Province                    |
| city         | String | ❌ No     | City                              |
| countryCode  | String | ❌ No     | Two-letter country code (e.g. EG) |
| zipCode      | String | ❌ No     | Postal/ZIP code                   |
| currency     | String | ❌ No     | Currency code (default: EGP)      |

---

## Status Check

### Endpoint

```http
GET /api/payments/bm/status/{merchantOrderId}
```

### Response (`PaymentTransactionResponse`)

| Field           | Type     |
| --------------- | -------- |
| transactionId   | Integer  |
| merchantOrderId | String   |
| gatewayResult   | String   |
| status          | String   |
| amount          | String   |
| currency        | String   |
| timestamp       | DateTime |
| createdBy       | String   |
| createdAt       | DateTime |
| modifiedBy      | String   |
| modifiedAt      | DateTime |

---

# Mastercard Gateway

## Initiate Payment

### Endpoint

```http
POST /api/payments/mastercard/initiate
```

### Request Body

| Field               | Type      | Required | Description                    |
| ------------------- | --------- | -------- | ------------------------------ |
| receiptIds          | Integer[] | ✅ Yes    | List of receipt IDs to pay for |
| studentReferenceId  | String    | ✅ Yes    | Student unique reference ID    |
| merchantRedirectUrl | String    | ❌ No     | Redirect URL after payment     |
| currency            | String    | ❌ No     | Currency code (default: EGP)   |

### Response (`MCPaymentInitiationResponse`)

| Field            | Type   | Description              |
| ---------------- | ------ | ------------------------ |
| merchantOrderId  | String | Internal order reference |
| gatewaySessionId | String | Session ID from bank     |
| sessionVersion   | String | Session version          |
| successIndicator | String | Security indicator       |
| redirectUrl      | String | Payment page URL         |

---

## Status Check

### Endpoint

```http
GET /api/payments/mastercard/status/{merchantOrderId}
```

Returns `PaymentTransactionResponse`.

---

## Refund Payment

### Endpoint

```http
POST /api/payments/mastercard/refund
```

### Request Body

| Field           | Type       | Required | Description                                                          |
| --------------- | ---------- | -------- | -------------------------------------------------------------------- |
| merchantOrderId | String     | ✅ Yes    | Original transaction order ID                                        |
| amount          | BigDecimal | ❌ No     | Partial refund amount. If omitted, full remaining amount is refunded |
| reason          | String     | ❌ No     | Refund reason                                                        |

### Response (`MCRefundResponse`)

| Field                     | Type       |
| ------------------------- | ---------- |
| merchantOrderId           | String     |
| refundTransactionId       | String     |
| refundedAmount            | BigDecimal |
| currency                  | String     |
| status                    | String     |
| gatewayCode               | String     |
| gatewayMessage            | String     |
| orderStatus               | String     |
| totalRefundedAmount       | BigDecimal |
| remainingRefundableAmount | BigDecimal |
| originalNetAmount         | BigDecimal |
| processedAt               | DateTime   |

---

# eFinance Gateway

## Initiate Payment

### Endpoint

```http
POST /api/payments/efinance/initiate
```

### Request Body

| Field              | Type      | Required | Description                                                                  |
| ------------------ | --------- | -------- | ---------------------------------------------------------------------------- |
| receiptIds         | Integer[] | ✅ Yes    | List of receipt IDs to pay for                                               |
| studentReferenceId | String    | ✅ Yes    | Student unique reference ID                                                  |
| billingDetails     | Object    | ✅ Yes    | Customer billing information                                                 |
| paymentMechanism   | String    | ❌ No     | `CARD`, `CHANNEL`, `MOBILE_WALLET`, `MEEZA`, or `NOT_SET` (customer chooses) |
| description        | String    | ❌ No     | Payment description                                                          |
| redirectUrl        | String    | ✅ Yes    | Redirect URL after payment                                                   |

### Billing Details

| Field        | Type   | Required | Description                  |
| ------------ | ------ | -------- | ---------------------------- |
| firstName    | String | ✅ Yes    | Customer first name          |
| lastName     | String | ✅ Yes    | Customer last name           |
| emailAddress | String | ❌ No     | Customer email               |
| mobileNumber | String | ❌ No     | Customer mobile number       |
| currency     | String | ❌ No     | Currency code (default: EGP) |

### Response (`EFinancePaymentInitiationResponse`)

| Field               | Type       | Description                    |
| ------------------- | ---------- | ------------------------------ |
| merchantOrderId     | String     | Internal transaction reference |
| senderRequestNumber | String     | eFinance request identifier    |
| redirectUrl         | String     | Gateway redirect URL           |
| formData            | Object     | Form data to submit            |
| totalAmount         | BigDecimal | Total amount customer pays     |
| baseAmount          | BigDecimal | Base amount before fees        |
| collectionFees      | BigDecimal | Gateway fees                   |
| currency            | String     | Currency code                  |
| expiryDate          | String     | Payment expiry date            |

### FormData

| Field               | Type   |
| ------------------- | ------ |
| SenderID            | String |
| RandomSecret        | String |
| RequestObject       | String |
| HashedRequestObject | String |

---

## Status Check

### Endpoint

```http
GET /api/payments/efinance/status/{merchantOrderId}
```

Returns `PaymentTransactionResponse`.

---

# Response Structures

## BaseResponse

All endpoints except `POST /api/payments/bm/initiate` return responses wrapped in a `BaseResponse`.

### Structure

| Field   | Type    | Description                     |
| ------- | ------- | ------------------------------- |
| success | Boolean | Indicates request success       |
| message | String  | Human-readable response message |
| data    | Object  | Endpoint-specific payload       |

---

# Endpoint Response Models

## GET /api/payments/receipts

Returns an array of `ReceiptResponse`.

| Field                  | Type       |
| ---------------------- | ---------- |
| id                     | Long       |
| receiptSerialNumber    | String     |
| receiptName            | String     |
| description            | String     |
| receiptTypeName        | String     |
| receiptTypeDescription | String     |
| active                 | String     |
| connectionTypeName     | String     |
| totalAmount            | BigDecimal |

---

## POST /api/payments/bm/initiate

Returns `InitiateCheckoutResponse` directly (not wrapped in `BaseResponse`).

### Structure

| Field   | Type   |
| ------- | ------ |
| status  | String |
| message | String |
| data    | Object |

### Data Object

| Field       | Type   |
| ----------- | ------ |
| sessionId   | String |
| checkoutUrl | String |
| token       | String |
| bmmId       | String |

---
