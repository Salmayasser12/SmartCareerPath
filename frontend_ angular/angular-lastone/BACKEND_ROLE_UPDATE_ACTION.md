# What Backend Needs To Do - Verify Endpoint

## Current Issue

Frontend shows "Payment verified" but database role is NOT updated.

This means one of two things:
1. Backend webhook didn't fire when payment completed
2. Backend webhook fired but didn't update role
3. Backend /api/payment/verify endpoint doesn't update role

## What Should Happen

### Option 1: Webhook Updates Role (Preferred)
```csharp
[HttpPost("webhook")]
public async Task<IActionResult> HandleStripeWebhook()
{
    // When Stripe sends charge.succeeded event:
    var chargeSucceededEvent = // ... extract from webhook
    
    var userId = chargeSucceededEvent.ClientReferenceId;
    var user = await _db.Users.FindAsync(userId);
    
    // UPDATE ROLE HERE!
    user.RoleId = 2; // Premium role ID
    await _db.SaveChangesAsync();
    
    return Ok();
}
```

### Option 2: Verify Endpoint Updates Role
```csharp
[HttpPost("verify")]
[AllowAnonymous]
public async Task<IActionResult> Verify([FromBody] VerifyPaymentRequest request)
{
    // Get session from Stripe
    var session = await _stripeService.GetSessionAsync(request.ProviderReference);
    
    // Check if payment succeeded
    if (session.PaymentStatus == "paid")
    {
        var transactionId = request.ProviderReference;
        var transaction = await _db.PaymentTransactions
            .FirstOrDefaultAsync(x => x.StripeSessionId == transactionId);
        
        if (transaction != null)
        {
            var user = await _db.Users.FindAsync(transaction.UserId);
            
            // UPDATE ROLE HERE!
            user.RoleId = 2; // Premium role ID
            await _db.SaveChangesAsync();
        }
    }
    
    return Ok(new { status = "completed", ... });
}
```

## Question for Backend Team

**Which approach are you using?**

1. **Webhook approach**: Webhook updates role when charge.succeeded fires
2. **Verify approach**: Verify endpoint updates role when called
3. **Both**: Webhook AND verify both update role

## Diagnostic Information

Tell backend to add logging:

```csharp
// In webhook handler:
Console.WriteLine($"[WEBHOOK] Payment received for session: {sessionId}");
Console.WriteLine($"[WEBHOOK] Updating user {userId} role to Premium");
Console.WriteLine($"[WEBHOOK] Role updated in DB: {success}");

// In verify endpoint:
Console.WriteLine($"[VERIFY] Verifying session: {sessionId}");
Console.WriteLine($"[VERIFY] Payment status: {session.PaymentStatus}");
Console.WriteLine($"[VERIFY] Updating user {userId} role");
Console.WriteLine($"[VERIFY] Role updated: {success}");
```

Then check backend console after payment and tell us what logs appear.

## What Frontend Does Now

After backend's `/api/payment/verify` returns 200 OK:

1. Frontend checks if response includes a new token
2. If yes: Store token and extract role from it
3. If no: Frontend tries to refresh user profile via GET /api/Auth/me
4. Frontend updates UI based on role from backend

**Frontend does NOT assume role is "Premium"** - it only updates what backend tells it!

## What Should Happen

```
1. User pays on Stripe ✓
   ↓
2. Stripe sends webhook to backend ✓
3. Backend webhook updates user role in DB ✓
4. User redirects to /paymob/response ✓
5. Frontend calls POST /api/payment/verify ✓
6. Backend verify checks if payment succeeded ✓
7. Backend returns: { status: "completed", role: "Premium" }
   OR returns new token with updated role
8. Frontend updates UI with role from response ✓
9. Frontend shows: "Payment successful, role updated to Premium" ✓
10. Frontend navigates to /home ✓
11. Dashboard shows Premium features ✓
12. Database shows role updated ✓
```

## Action Items

Backend needs to:
1. [ ] Ensure webhook fires when payment completes
2. [ ] Ensure webhook handler updates user role in DB
3. [ ] OR ensure verify endpoint updates user role in DB
4. [ ] Add logging to show what's happening
5. [ ] Test with a real payment
6. [ ] Verify database is updated after payment

Frontend is ready - just waiting for backend to update the database!

## Testing Command

After backend fix, test with:

```bash
curl -X POST http://localhost:5164/api/payment/verify \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{"providerReference":"cs_test_YOUR_SESSION_ID","request":{}}'
```

Response should include the updated role or a new token with role.
