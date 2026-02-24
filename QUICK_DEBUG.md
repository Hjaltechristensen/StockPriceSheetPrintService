# 🚀 QUICK START - Debug Login Problem

Du får `ERR_EMPTY_RESPONSE` når du returnerer fra Saxo login. Her er hvad du skal gøre:

## **STEP 1: Åbn Output Window i Visual Studio**

1. **View** → **Output** (eller `Ctrl+Alt+O`)
2. I dropdown vælg: **Debug** 
3. Tøm tidligere logs (klik "Clear All")

## **STEP 2: Start appen og gå til login**

1. Run appen (`F5`)
2. Gå til: `http://localhost:5151/saxo/login`
3. Du skal se loginlink i Output vinduet:

```
[SAXO-LOGIN] ✓ Login URL genereret succesfuldt
```

Hvis du **ikke** ser dette = der er fejl med AppKey/AppSecret. Se Sektion 4.

## **STEP 3: Log ind i Saxo**

1. Kopier det link der vises
2. Åbn i browser
3. Log ind med dine Saxo credentials
4. Du får SMS 2FA kode
5. Indsæt koden og tryk OK

**Du ser nu hele callback-loggen i Output vinduet. LES NØJE IGENNNEM:**

### ✅ **Success look:**

```
[SAXO-CALLBACK] ========== OAUTH CALLBACK STARTER ==========
[SAXO-CALLBACK] ✓ Auth code modtaget
[SAXO-CALLBACK] [STEP 1] Starter token exchange
[SAXO-CALLBACK] [STEP 1.2] Svar status: 200
[SAXO-CALLBACK] ✓ Token response succesfuldt modtaget
[SAXO-CALLBACK] [STEP 3] Starter krypton og gemning af refresh token
[SAXO-CALLBACK] ✓ Token krypteret succesfuldt
[SAXO-CALLBACK] ✓ Refresh token gemt sikkert på: /app/data/refresh_token.bin
[SAXO-CALLBACK] [STEP 4] Starter balance hentning som bekræftelse
[SAXO-CALLBACK] [STEP 4.2] Balance response status: 200
[SAXO-CALLBACK] ✓ Balance hentet succesfuldt
[SAXO-CALLBACK] ========== ✓ CALLBACK SUCCESFULDT ==========
```

Du skal se en side med: **"Alt er sat op! Din worker vil nu køre automatisk."**

---

## 🔴 **Hvis der er fejl:**

### **Fejl 1: Ingen [SAXO-CALLBACK] logs vises**

**Årsag:** Request når ikke callback-endepointet

**Løsning:**
- Check at `SAXO_REDIRECT_URL` i `.env` er **NØJAGTIGT** den samme som du konfigurerede i Saxo portal
- Hvis du bruger `http://192.168.1.239:5151/saxo/callback` lokalt → skal være samme i portal
- Hvis du bruger `http://localhost:5151/saxo/callback` → skal være samme i portal

**Test:**
```bash
# I browser, test at URL'en virker
http://localhost:5151/saxo/callback?code=test_code
# Du skulle få en error om manglende code, IKKE ERR_EMPTY_RESPONSE
```

### **Fejl 2: Status 400 ved token exchange**

```
[SAXO-CALLBACK] [STEP 1.2] Svar status: 400
[SAXO-CALLBACK] Response Body: {"error":"invalid_client"}
```

**Årsag:** AppKey eller AppSecret er forkert

**Løsning:**
1. Dobbeltklik dine Saxo credentials i portal
2. Kopier præcist (uden mellemrum!)
3. Sæt i `.env` eller environment variabler:
```env
SAXO_APP_KEY=your_exact_key_here
SAXO_APP_SECRET=your_exact_secret_here
```
4. Genstart appen

### **Fejl 3: Status 401 ved token exchange**

```
[SAXO-CALLBACK] [STEP 1.2] Svar status: 401
```

**Årsag:** Redirect URL matcher ikke nøjagtigt

**Løsning:**
- I Saxo portal: Se præcist hvilken URL du har sat
- I `.env` sæt **NØJAGTIGT** den samme:
```env
# Hvis portal siger: http://localhost:5151/saxo/callback
SAXO_REDIRECT_URL=http://localhost:5151/saxo/callback

# IKKE dette:
# SAXO_REDIRECT_URL=http://localhost:5151/saxo/callback/  (ekstra slash!)
# SAXO_REDIRECT_URL=http://127.0.0.1:5151/saxo/callback  (IP i stedet for localhost)
```

### **Fejl 4: "Kunne ikke parse JSON response"**

```
[SAXO-CALLBACK] ✗ FEJL: Kunne ikke parse JSON response fra Saxo
[SAXO-CALLBACK] Raw response: [XML or HTML]
```

**Årsag:** Du bruger Sandbox endpoint i stedet for Live

**Løsning:**
- Check at `SAXO_AUTH_ENDPOINT` er:
```env
# Live:
SAXO_AUTH_ENDPOINT=https://live.logonvalidation.net/authorize

# IKKE Sandbox:
# SAXO_AUTH_ENDPOINT=https://sim.logonvalidation.net/authorize
```

### **Fejl 5: "Kunne ikke gemme refresh token på disk"**

```
[SAXO-CALLBACK] ✗ FEJL: Kunne ikke gemme refresh token på disk!
```

**Årsag:** `/app/data/` folder eksisterer ikke eller ingen permissions

**Løsning (lokal udvikling):**
```bash
# Opret folder
mkdir C:\temp\app_data

# Opdater token path i appsettings.Development.json:
# OG i SaxoAuthController.cs:
# const string TokenPath = "C:\temp\app_data\refresh_token.bin";
```

**Løsning (Docker):**
```dockerfile
# I Dockerfile, tilføj BEFORE entrypoint:
RUN mkdir -p /app/data && chmod 777 /app/data
```

---

## ✅ **Når det virker:**

1. Du returnerer til en side med success message
2. I Output window ser du alle ✓ symboler
3. Token er nu gemt krypteret på disk
4. Morgen kl 03:30 kører jobbet automatisk

**Test det virker:**

```bash
# Test at token blev gemt:
ls -la /app/data/refresh_token.bin    # Linux/Mac
dir C:\...\app\data\                  # Windows Docker

# Token skal være der og være krypteret (ikke readable)
```

---

## 🆘 **Still stuck?**

1. Copy HELE [SAXO-CALLBACK] logblokken
2. Check event viewer eller application logs
3. Kontakt Saxo support hvis det handler om deres API

**God tur! 🚀**
