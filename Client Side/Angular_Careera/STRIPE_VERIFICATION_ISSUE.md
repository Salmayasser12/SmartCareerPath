# Stripe Payment Verification Issue

## Problem

Frontend shows "Payment verified successfully" but:
- ❌ Database role ID NOT updated
- ❌ Backend webhook didn't fire (or didn't update role)
- ❌ /api/payment/verify didn't update role

## What Should Happen

### 1. Payment Completes on Stripe
```
Stripe Event: charge.succeeded → charge.paid
Stripe Event: customer.subscription.created
```

### 2. Backend Webhook Should Fire
```
POST /api/payment/webhook
Body: Stripe webhook event
Action: Update user role in DB
```

### 3. Backend Verify Endpoint Should Confirm
```
POST /api/payment/verify
{
  "providerReference": "cs_test_...",
  "request": {}
}

Backend should:
1. Get session from Stripe ✓
2. Check if payment succeeded ✓
3. If yes: Update user role in DB
4. Return: { status: "completed", ... }
```

## Testing

### Check if Webhook Fired
```sql
-- SQL Server
SELECT * FROM StripeWebhookEvents ORDER BY CreatedAt DESC LIMIT 5
-- Look for charge.succeeded or customer.subscription.created events
```

### Check if Role Was Updated
```sql
-- SQL Server
SELECT Id, Email, RoleId, CreatedAt, UpdatedAt 
FROM Users 
WHERE Email = 'your@email.com'
-- Check if RoleId changed and UpdatedAt is recent
```

### Check Backend Logs
- Look for: "Processing Stripe webhook"
- Look for: "Updating user role to Premium"
- Look for: "Payment verification successful"

## Possible Issues

### Issue 1: Webhook Not Configured
- Backend might not have webhook URL set up in Stripe
- Stripe doesn't know where to send events
- **Solution:** Add webhook URL to Stripe Dashboard

### Issue 2: Webhook Signature Validation Fails
- Backend validates webhook signature
- If signature validation fails, webhook is ignored
- **Solution:** Check webhook secret key in backend config

### Issue 3: Verify Endpoint Not Updating Role
- `/api/payment/verify` endpoint doesn't update DB
- Only returns verification status
- **Solution:** Backend needs to update role in verify endpoint

### Issue 4: Payment Status Not "Completed"
- Backend checks if payment is actually completed
- If payment is pending/processing, role not updated
- **Solution:** Wait or check payment status

## Diagnostic Steps

### Step 1: Check Payment in Stripe Dashboard
```
1. Go to https://dashboard.stripe.com/test/payments
2. Find the payment you just made
3. Status should be: Succeeded ✓
4. Look for charge ID: ch_test_...
```

### Step 2: Check Webhook Events in Stripe
```
1. Go to https://dashboard.stripe.com/test/webhooks
2. Find your webhook endpoint
3. Check "Events" tab
4. Should show: charge.succeeded, customer.subscription.created
5. Check status: Should be green (delivered) ✓
6. If red (failed), click to see error
```

### Step 3: Check Backend Logs
```
Run backend with: dotnet run
Look for console output after payment:
- "Payment webhook received"
- "Processing charge.succeeded"
- "Updating user role"
- "Webhook processed successfully"
```

### Step 4: Query Database
```sql
-- Check if webhook table has events
SELECT * FROM StripeWebhookEvents 
ORDER BY CreatedAt DESC LIMIT 1

-- Check if user role was updated
SELECT Id, Email, RoleId, UpdatedAt 
FROM Users 
WHERE Email = 'your@email.com'
ORDER BY UpdatedAt DESC LIMIT 1
```

## Quick Fix Checklist

- [ ] Stripe payment shows as "Succeeded" in Dashboard
- [ ] Webhook endpoint is configured in Stripe
- [ ] Webhook secret key matches backend config
- [ ] Backend logs show webhook received
- [ ] Database shows role updated
- [ ] /api/payment/verify updates role

## Expected Behavior (After Fix)

```
1. User completes payment on Stripe ✓
2. Stripe sends charge.succeeded webhook ✓
3. Backend webhook handler updates role to Premium ✓
4. Frontend calls /api/payment/verify ✓
5. Verify endpoint confirms: status = "completed" ✓
6. Frontend updates UI to show Premium ✓
7. User can access premium features ✓
8. Database shows role updated to Premium ✓
```

## Contact Backend

Tell backend to check:

1. **Webhook Configuration**
   - Is webhook endpoint registered in Stripe?
   - Is webhook secret key correct?
   - Is webhook handler receiving events?

2. **Role Update Logic**
   - Does webhook handler update user role?
   - Does verify endpoint update user role?
   - Or do they just check payment status?

3. **Database**
   - After payment, is RoleId changed in DB?
   - If not, why not? (No webhook? Handler failed? etc.)
