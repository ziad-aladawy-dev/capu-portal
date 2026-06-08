# HU Treasury System Payment APIs

## Overview

This collection provides a comprehensive set of APIs for managing payment operations within the HU Treasury System. It enables seamless integration with multiple payment gateways to process transactions, check payment statuses, handle refunds, and receive real-time payment notifications.

---

## Supported Payment Gateways

### Mastercard

* Payment initiation
* Payment status tracking
* Refund processing

### Bank Misr (BM)

* Payment initiation
* Payment status tracking

### eFinance

* Payment initiation
* Payment status tracking

---

## Main Capabilities

### Payment Initiation

Start new payment transactions through:

* Mastercard
* Bank Misr
* eFinance

### Status Checking

Retrieve real-time payment status using merchant order IDs.

### Refunds

Process refunds for completed Mastercard transactions.

### Webhook Handling

Receive and process payment status notifications from supported payment gateways.

---

## Authentication

> Coming soon.

---

# Getting Started

1. Configure the `baseUrl` variable to point to your HU Treasury System environment.
2. Choose the appropriate payment gateway.
3. Use payment initiation endpoints to start transactions.
4. Monitor payment status using status endpoints.
5. Configure webhook endpoints to receive real-time payment updates.

---

# Filter Receipts

## Endpoint

```http
GET /api/payments/receipts
```

## Query Parameters

| Parameter         | Type      | Required | Description                                         |
| ----------------- | --------- | -------- | --------------------------------------------------- |
| connectionTypeIds | Integer[] | ✅ Yes    | List of connection type IDs (at least one required) |
| receiptTypeIds    | Integer[] | ❌ No     | List of receipt type IDs                            |

## Example Request

```http
GET /api/payments/receipts?connectionTypeIds=1,2&receiptTypeIds=1,3
```

---

# Bank Misr (BM) Gateway

## Initiate Payment

### Endpoint

```http
POST /api/payments/bm/initiate
```

### Request Body

| Field               | Type      | Required | Description                |
| ------------------- | --------- | -------- | -------------------------- |
| receiptIds          | Integer[] | ✅ Yes    | Receipt IDs to pay         |
| studentReferenceId  | String    | ✅ Yes    | Student reference ID       |
| billingDetails      | Object    | ✅ Yes    | Billing information        |
| merchantRedirectUrl | String    | ✅ Yes    | Redirect URL after payment |

### Billing Details

| Field        | Type   | Required | Description            |
| ------------ | ------ | -------- | ---------------------- |
| firstName    | String | ✅ Yes    | First name             |
| lastName     | String | ✅ Yes    | Last name              |
| emailAddress | String | ✅ Yes    | Email                  |
| mobileNumber | String | ✅ Yes    | Mobile number          |
| state        | String | ❌ No     | State/province         |
| city         | String | ❌ No     | City                   |
| countryCode  | String | ❌ No     | Country code (e.g. EG) |
| zipCode      | String | ❌ No     | ZIP code               |
| currency     | String | ❌ No     | Default: EGP           |

### Response

Returns `InitiateCheckoutResponse`.

#### Response Fields

| Field   | Type   | Description        |
| ------- | ------ | ------------------ |
| status  | String | success or failure |
| message | String | Response message   |
| data    | Object | Session data       |

#### Session Data

| Field       | Type   | Description   |
| ----------- | ------ | ------------- |
| sessionId   | String | BM Session ID |
| checkoutUrl | String | Payment URL   |
| token       | String | JWT token     |
| bmmId       | String | Merchant ID   |

---

## Check Payment Status

### Endpoint

```http
GET /api/payments/bm/status/{merchantOrderId}
```

### Response

Returns `PaymentTransactionResponse`.

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

| Field               | Type      | Required | Description          |
| ------------------- | --------- | -------- | -------------------- |
| receiptIds          | Integer[] | ✅ Yes    | Receipt IDs          |
| studentReferenceId  | String    | ✅ Yes    | Student reference ID |
| merchantRedirectUrl | String    | ❌ No     | Redirect URL         |
| currency            | String    | ❌ No     | Default: EGP         |

### Response

Returns `MCPaymentInitiationResponse`.

| Field            | Type   | Description              |
| ---------------- | ------ | ------------------------ |
| merchantOrderId  | String | Internal order reference |
| gatewaySessionId | String | Bank session ID          |
| sessionVersion   | String | Session version          |
| successIndicator | String | Security indicator       |
| redirectUrl      | String | Payment URL              |

---

## Check Payment Status

### Endpoint

```http
GET /api/payments/mastercard/status/{merchantOrderId}
```

### Response

Returns `PaymentTransactionResponse`.

---

## Refund Payment

### Endpoint

```http
POST /api/payments/mastercard/refund
```

### Request Body

| Field           | Type       | Required | Description           |
| --------------- | ---------- | -------- | --------------------- |
| merchantOrderId | String     | ✅ Yes    | Original order ID     |
| amount          | BigDecimal | ❌ No     | Partial refund amount |
| reason          | String     | ❌ No     | Refund reason         |

### Response

Returns `MCRefundResponse`.

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

| Field              | Type      | Required | Description                                  |
| ------------------ | --------- | -------- | -------------------------------------------- |
| receiptIds         | Integer[] | ✅ Yes    | Receipt IDs                                  |
| studentReferenceId | String    | ✅ Yes    | Student reference ID                         |
| billingDetails     | Object    | ✅ Yes    | Billing details                              |
| paymentMechanism   | String    | ❌ No     | CARD, CHANNEL, MOBILE_WALLET, MEEZA, NOT_SET |
| description        | String    | ❌ No     | Payment description                          |
| redirectUrl        | String    | ✅ Yes    | Redirect URL                                 |

### Billing Details

| Field        | Type   | Required |
| ------------ | ------ | -------- |
| firstName    | String | ✅ Yes    |
| lastName     | String | ✅ Yes    |
| emailAddress | String | ❌ No     |
| mobileNumber | String | ❌ No     |
| currency     | String | ❌ No     |

### Response

Returns `EFinancePaymentInitiationResponse`.

| Field               | Type       |
| ------------------- | ---------- |
| merchantOrderId     | String     |
| senderRequestNumber | String     |
| redirectUrl         | String     |
| formData            | Object     |
| totalAmount         | BigDecimal |
| baseAmount          | BigDecimal |
| collectionFees      | BigDecimal |
| currency            | String     |
| expiryDate          | String     |

### Form Data

| Field               | Type   |
| ------------------- | ------ |
| SenderID            | String |
| RandomSecret        | String |
| RequestObject       | String |
| HashedRequestObject | String |

---

## Check Payment Status

### Endpoint

```http
GET /api/payments/efinance/status/{merchantOrderId}
```

### Example

```http
GET /api/payments/efinance/status/EF-1-30311070201755-4b94f115
```

### Response

Returns `PaymentTransactionResponse`.

```json
{
  "success": false,
  "message": "string",
  "data": ""
}
```

---

## Test Initiate Payment

### Endpoint

```http
POST /api/payments/test/eFinance/initiate-payment
```

### Notes

* Returns HTML redirect response.
* Used for testing payment initiation.

### Sample Request

```json
{
  "billingDetails": {
    "firstName": "string",
    "lastName": "string",
    "emailAddress": "string@gmail",
    "mobileNumber": "string"
  },
  "receiptIds": [1],
  "studentReferenceId": "30201901044528",
  "paymentMechanism": "NOT_SET",
  "description": "string",
  "redirectUrl": "https://masarhnu.netlify.app/#/home/main",
  "clientIp": "::1"
}
```

---

# Response Wrapper

Most endpoints return responses wrapped in `BaseResponse`.

```json
{
  "success": true,
  "message": "Operation completed successfully",
  "data": {}
}
```

## BaseResponse Fields

| Field   | Type    | Description      |
| ------- | ------- | ---------------- |
| success | Boolean | Request result   |
| message | String  | Response message |
| data    | Object  | Response payload |

---

# Webhook Integration

## Receive Treasury Webhook

### Endpoint

```http
POST /your-webhook-endpoint
```

### Authentication

Header:

```http
X-Webhook-Signature: <apiKey>
```

### Purpose

Receive transaction status updates from the HU Treasury System.

You must:

1. Implement this endpoint.
2. Register the endpoint URL with the treasury system.

### Example Webhook Payload

```json
{
  "webhookId": "550e8400-e29b-41d4-a716-446655440000",
  "eventType": "TRANSACTION_PAID",
  "eventTimestamp": "2026-02-18T10:30:00.000Z",
  "transaction": {
    "transactionId": 12345,
    "merchantOrderId": "ORDER-2026-001234",
    "studentReferenceId": "STU-20210001",
    "status": "PAID",
    "previousStatus": "PENDING",
    "gatewayType": "EFINANCE",
    "gatewayTransactionId": "EF-TXN-789456",
    "gatewayOrderId": "EF-ORD-123456",
    "grossAmount": 5000,
    "gatewayFee": 50,
    "netAmount": 4950,
    "currency": "EGP",
    "gatewayResult": "SUCCESS",
    "paymentDate": "2026-02-18T10:29:55.000Z",
    "createdAt": "2026-02-18T10:00:00.000Z",
    "billingDetails": {
      "firstName": "Ahmed",
      "lastName": "Mohamed",
      "email": "ahmed.mohamed@example.com",
      "mobileNumber": "01012345678"
    },
    "receipt": {
      "receiptId": 789,
      "receiptNumber": "RCP-2026-001234",
      "receiptType": "Tuition Fees",
      "totalAmount": 5000
    }
  },
  "refund": null,
  "metadata": null
}
```

### Success Response

```json
{
  "success": true,
  "message": "Webhook processed successfully"
}
```

---

# Available Payment Gateways

* Mastercard
* Bank Misr (BM)
* eFinance

---

# Common Payment Status Response

Used by:

* BM Status
* Mastercard Status
* eFinance Status

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
|                 |          |
