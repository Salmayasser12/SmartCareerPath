# Backend: Return Updated JWT Token from Payment Verification Endpoint

## Problem
After user completes a Stripe payment, the frontend redirects to the success page and calls `POST /api/payment/verify` to confirm the payment. However, the backend is not returning an updated JWT token with the new "Premium" role in the response. Users have to manually sign out and sign in again to see the premium role take effect.

## Solution
Modify the `POST /api/payment/verify` endpoint to return a new JWT token with the updated user role (if payment verification succeeds).

## Implementation Steps

### Step 1: Extract User Info from Verified Payment
When verifying the payment with Stripe, extract the user who made the payment:
```csharp
// In your verify endpoint handler
public async Task<IActionResult> VerifyPayment([FromBody] VerifyPaymentRequest request)
{
    // Verify the payment was successful with Stripe
    var session = await client.Sessions.GetAsync(request.ProviderReference);
    
    if (session == null || session.PaymentStatus != "paid") {
        return BadRequest(new { message = "Payment not verified" });
    }
    
    // Extract the user ID from the session or from current auth context
    // Option 1: If you stored user ID in session metadata
    var userId = session.Metadata.ContainsKey("userId") ? session.Metadata["userId"] : null;
    
    // Option 2: Get from HttpContext (current authenticated user)
    if (string.IsNullOrEmpty(userId)) {
        userId = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    }
    
    if (string.IsNullOrEmpty(userId)) {
        return BadRequest(new { message = "Could not identify user" });
    }
    
    // ... rest of verification logic ...
}
```

### Step 2: Update User Role in Database
After verifying payment, update the user's role in the database to "Premium":

```csharp
// Find the user
var user = await _userManager.FindByIdAsync(userId);
if (user == null) {
    return BadRequest(new { message = "User not found" });
}

// Add Premium role if not already assigned
var roles = await _userManager.GetRolesAsync(user);
if (!roles.Contains("Premium")) {
    var result = await _userManager.AddToRoleAsync(user, "Premium");
    if (!result.Succeeded) {
        // Log the error but don't fail the payment verification
        _logger.LogError($"Failed to add Premium role to user {userId}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
    }
}

// Update last role changed timestamp (optional, for audit)
user.RoleUpdatedAt = DateTime.UtcNow;
await _userManager.UpdateAsync(user);
```

### Step 3: Generate a New JWT Token with Updated Role
After updating the role, generate a fresh JWT token with the new claims:

```csharp
// Generate a new token with the updated role
var newToken = await GenerateJwtToken(user);

Console.WriteLine($"[Payment.Verify] Generated new token for user {userId} with Premium role");
```

**Helper method to generate token (if you don't have one):**
```csharp
private async Task<string> GenerateJwtToken(User user)
{
    var roles = await _userManager.GetRolesAsync(user);
    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim("fullName", user.FullName ?? ""),
        new Claim("userId", user.Id),
    };
    
    // Add role claims (supports Microsoft path used by Angular frontend)
    foreach (var role in roles)
    {
        claims.Add(new Claim("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", role));
        claims.Add(new Claim(ClaimTypes.Role, role));
    }
    
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    var expires = DateTime.UtcNow.AddDays(_jwtSettings.ExpiryDays);
    
    var token = new JwtSecurityToken(
        issuer: _jwtSettings.Issuer,
        audience: _jwtSettings.Audience,
        claims: claims,
        expires: expires,
        signingCredentials: creds
    );
    
    return new JwtSecurityTokenHandler().WriteToken(token);
}
```

### Step 4: Return the New Token in the Response
Modify your `VerifyPaymentResponse` to include the new token:

```csharp
public class VerifyPaymentResponse
{
    public string Status { get; set; }  // "completed", "pending", "failed"
    public bool Verified { get; set; }
    public string Message { get; set; }
    public string Token { get; set; }    // ← NEW: Return the updated JWT
    public string UserId { get; set; }
    public DateTime? VerifiedAt { get; set; }
}
```

**Return it from the endpoint:**
```csharp
return Ok(new VerifyPaymentResponse
{
    Status = "completed",
    Verified = true,
    Message = "Payment verified successfully",
    Token = newToken,    // ← Include the new JWT with Premium role
    UserId = userId,
    VerifiedAt = DateTime.UtcNow
});
```

### Step 5: Complete Endpoint Example

```csharp
[HttpPost("verify")]
[AllowAnonymous]  // Allow anonymous because frontend calls after redirect
public async Task<IActionResult> VerifyPayment([FromBody] VerifyPaymentRequest request)
{
    Console.WriteLine($"[Payment.Verify] Verifying payment with provider reference: {request.ProviderReference}");
    
    try
    {
        // 1. Verify with Stripe
        var session = await _stripeClient.Sessions.GetAsync(request.ProviderReference);
        
        if (session == null || session.PaymentStatus != "paid")
        {
            Console.WriteLine($"[Payment.Verify] Payment not verified. Status: {session?.PaymentStatus}");
            return Ok(new VerifyPaymentResponse
            {
                Status = "failed",
                Verified = false,
                Message = "Payment not found or not completed"
            });
        }
        
        Console.WriteLine($"[Payment.Verify] Payment verified with Stripe. SessionId: {session.Id}");
        
        // 2. Extract user ID
        var userId = session.Metadata.ContainsKey("userId") 
            ? session.Metadata["userId"] 
            : User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userId))
        {
            Console.WriteLine("[Payment.Verify] Could not identify user from session or auth context");
            return BadRequest(new { message = "Could not identify user" });
        }
        
        // 3. Find user in database
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            Console.WriteLine($"[Payment.Verify] User {userId} not found");
            return BadRequest(new { message = "User not found" });
        }
        
        // 4. Update user role to Premium
        var roles = await _userManager.GetRolesAsync(user);
        if (!roles.Contains("Premium"))
        {
            Console.WriteLine($"[Payment.Verify] Adding Premium role to user {userId}");
            var result = await _userManager.AddToRoleAsync(user, "Premium");
            if (!result.Succeeded)
            {
                Console.WriteLine($"[Payment.Verify] Failed to add role: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                // Don't fail the payment — role update is secondary
            }
        }
        
        // 5. Update timestamp
        user.RoleUpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);
        
        // 6. Generate new token with updated role
        Console.WriteLine($"[Payment.Verify] Generating new token for user {userId}");
        var newToken = await GenerateJwtToken(user);
        
        // 7. Store the payment verification in database (if needed for audit)
        // await _paymentRepository.RecordVerification(userId, session.Id, DateTime.UtcNow);
        
        // 8. Return success with the new token
        return Ok(new VerifyPaymentResponse
        {
            Status = "completed",
            Verified = true,
            Message = "Payment verified and role updated to Premium",
            Token = newToken,      // ← Frontend will store this and extract the new role
            UserId = userId,
            VerifiedAt = DateTime.UtcNow
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Payment.Verify] Exception: {ex.Message}");
        return StatusCode(500, new { message = "An error occurred during verification", error = ex.Message });
    }
}
```

---

## What the Frontend Expects

The frontend expects the response to have this shape:
```json
{
    "status": "completed",
    "verified": true,
    "message": "Payment verified and role updated to Premium",
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "userId": "user-id-here",
    "verifiedAt": "2025-12-03T12:00:00Z"
}
```

**Important:** The `token` must be a JWT containing these role claims (one or more):
```json
{
    "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": "Premium",
    "role": "Premium",
    "roleName": "Premium"
}
```

---

## What Happens After Backend Returns the Token

1. Frontend receives the verify response with `token`
2. Frontend calls `authService.setToken(response.token)`
3. `setToken()` decodes the token and extracts the "Premium" role from claims
4. All subscribers to `auth.role$` are notified of the role change
5. Sidebar updates immediately to show Premium features enabled
6. Premium guard allows access to `/job-parser` and `/ai-interviewer`
7. **No page reload or manual sign-out/sign-in required**

---

## Debugging Tips

### Check Token Was Generated Correctly
```csharp
var decodedToken = new JwtSecurityTokenHandler().ReadJwtToken(newToken);
Console.WriteLine("Token Claims:");
foreach (var claim in decodedToken.Claims)
{
    Console.WriteLine($"  {claim.Type}: {claim.Value}");
}
```

### Verify Role Was Added to User
```csharp
var roles = await _userManager.GetRolesAsync(user);
Console.WriteLine($"User roles after payment: {string.Join(", ", roles)}");
```

### Test in Postman
1. Call `POST /api/payment/verify` with a valid Stripe session ID
2. Check response contains `"token": "eyJ..."`
3. Decode the token at jwt.io
4. Verify it contains the `role` claim with value "Premium"

---

## Integration Checklist

- [ ] Verify endpoint receives verified payment from Stripe
- [ ] Extract user ID from session metadata or auth context
- [ ] Find user in UserManager
- [ ] Add "Premium" role to user if not already present
- [ ] Call `GenerateJwtToken(user)` to create new JWT
- [ ] Return response with `"token": newToken`
- [ ] Token contains role claim: `http://schemas.microsoft.com/ws/2008/06/identity/claims/role`
- [ ] Console logs show: `[Payment.Verify] Adding Premium role to user {userId}`
- [ ] Console logs show: `[Payment.Verify] Generating new token for user {userId}`
- [ ] Test in browser: after payment, sidebar shows Premium features unlocked
- [ ] Test in browser: no page reload required
- [ ] Test in browser: no manual sign-out/sign-in required

---

## Copy This Prompt to GitHub Copilot Backend Chat

> I need to modify my `POST /api/payment/verify` endpoint to return an updated JWT token after payment verification completes.
>
> Current flow:
> - User completes Stripe payment
> - Frontend calls `POST /api/payment/verify` with the Stripe session ID
> - Backend verifies the session with Stripe
> - Backend returns `{ status: "completed", verified: true }`
>
> **What I need:**
> - Extract the user ID from the verified payment
> - Update the user's role in the database to "Premium"
> - Generate a new JWT token that includes the "Premium" role in the claims
> - Return the new token in the verify response: `{ status: "completed", verified: true, token: "eyJ..." }`
>
> **Important:** The token must have the role claim at this path (Angular frontend expects it):
> `http://schemas.microsoft.com/ws/2008/06/identity/claims/role`
>
> Can you write the updated endpoint code? Include logging so I can debug. Make sure the role is actually added to the user in the database before generating the token.
