# 📋 Logging Guide - StockPrice Service

## 🎯 Oversigt

Appen bruger struktureret logging med **prefixes** som gør det nemt at følge flowet:

- `[SAXO-LOGIN]` - OAuth login flow
- `[SAXO-CALLBACK]` - Callback fra Saxo efter login
- `[SAXO-TOKEN]` - Token refresh automatisk
- `[SAXO-BALANCE]` - Hentning af balance fra Saxo
- `[JOB]` - Daglig værdiberegning
- `[SCHEDULER]` - Scheduler/timer logic

## 🔍 Sådan debugger du login-problemet

### 1️⃣ **Log ind via web interface**

Gå til: `http://localhost:5151/saxo/login`

Tjek **Output window** og se disse logs:

```
[SAXO-LOGIN] Genererer login URL
[SAXO-LOGIN] Auth Endpoint: https://live.logonvalidation.net/authorize
[SAXO-LOGIN] Client ID: ****
[SAXO-LOGIN] Redirect URI: http://localhost:5151/saxo/callback
[SAXO-LOGIN] ✓ Login URL genereret succesfuldt
```

✅ Hvis du ser disse = login URL blev genereret korrekt

### 2️⃣ **Efter du logger ind i Saxo og returnerer**

Du skal se disse logs i Output window:

```
[SAXO-CALLBACK] ========== OAUTH CALLBACK STARTER ==========
[SAXO-CALLBACK] ✓ Auth code modtaget (længde: 100+ tegn)
[SAXO-CALLBACK] [STEP 1] Starter token exchange
[SAXO-CALLBACK] Token Endpoint: https://live.logonvalidation.net/token
[SAXO-CALLBACK] Redirect URL: http://localhost:5151/saxo/callback
[SAXO-CALLBACK] [STEP 1.1] POST request sendes til token endpoint...
[SAXO-CALLBACK] [STEP 1.2] Svar status: 200
[SAXO-CALLBACK] ✓ Token response succesfuldt modtaget
[SAXO-CALLBACK] [STEP 2] Parser tokens fra JSON response
[SAXO-CALLBACK] ✓ Access Token modtaget (længde: 150+ tegn)
[SAXO-CALLBACK] ✓ Refresh Token modtaget (længde: 150+ tegn)
[SAXO-CALLBACK] [STEP 3] Starter krypton og gemning af refresh token
[SAXO-CALLBACK] [STEP 3.1] Krypterer refresh token med EncryptionKey...
[SAXO-CALLBACK] ✓ Token krypteret succesfuldt (længde: 200+ tegn)
[SAXO-CALLBACK] [STEP 3.2] Token path: /app/data/refresh_token.bin
[SAXO-CALLBACK] [STEP 3.3] Directory: /app/data
[SAXO-CALLBACK] [STEP 3.4] Skriver til fil...
[SAXO-CALLBACK] ✓ Refresh token gemt sikkert på: /app/data/refresh_token.bin
[SAXO-CALLBACK] [STEP 4] Starter balance hentning som bekræftelse
[SAXO-CALLBACK] API Base URL: https://gateway.saxobank.com/openapi/openapi/port/v1/balances/me
[SAXO-CALLBACK] [STEP 4.1] GET request til balance endpoint...
[SAXO-CALLBACK] [STEP 4.2] Balance response status: 200
[SAXO-CALLBACK] ✓ Balance hentet succesfuldt
[SAXO-CALLBACK] ✓ Balance deserializeret: 500000.00 DKK
[SAXO-CALLBACK] ========== ✓ CALLBACK SUCCESFULDT ==========
[SAXO-CALLBACK] Returnerer success response
```

#### ⚠️ **Fejlfinding**

Hvis du får `ERR_EMPTY_RESPONSE`:

- **Status 400** = AppKey/AppSecret forkert
  ```
  [SAXO-CALLBACK] ✗ FEJL: Token request afvist af Saxo!
  [SAXO-CALLBACK] Status Code: 400
  [SAXO-CALLBACK] Response Body: {"error":"invalid_client"}
  ```
  👉 Check dine Saxo credentials i `.env`

- **Status 401** = Redirect URL matcher ikke 100%
  ```
  [SAXO-CALLBACK] ✗ FEJL: Token request afvist af Saxo!
  [SAXO-CALLBACK] Status Code: 401
  ```
  👉 Sikr at `SAXO_REDIRECT_URL` i `.env` matcher portalen til punkt

- **JSON parsing fejl** = Saxo returnerede uventet format
  ```
  [SAXO-CALLBACK] ✗ FEJL: Kunne ikke parse JSON response fra Saxo
  [SAXO-CALLBACK] Raw response: {...}
  ```
  👉 Kontakt Saxo support

- **Ingen access til fil** = permissions problem på `/app/data/`
  ```
  [SAXO-CALLBACK] ✗ FEJL: Kunne ikke gemme refresh token på disk!
  ```
  👉 Check folder permissions

---

## 🤖 **Automatisk daily job**

Når jobbet kører automatisk (03:30 hver dag):

```
╔═══════════════════════════════════════════╗
║  JOB KØRSEL STARTER - 03:30:00            ║
╚═══════════════════════════════════════════╝

[JOB] [1/4] Starter Saxo balance hentning...
[SAXO-TOKEN] ========== TOKEN REFRESH STARTER ==========
[SAXO-TOKEN] Token Path: /app/data/refresh_token.bin
[SAXO-TOKEN] ✓ Token fil fundet
[SAXO-TOKEN] [STEP 1] Læser encrypted token fra disk...
[SAXO-TOKEN] ✓ Encrypted token læst (længde: 250 tegn)
[SAXO-TOKEN] [STEP 2] Dekrypterer token med EncryptionKey...
[SAXO-TOKEN] ✓ Token dekrypteret succesfuldt
[SAXO-TOKEN] [STEP 3] Sender refresh token request til Saxo...
[SAXO-TOKEN] [STEP 4] Response status: 200
[SAXO-TOKEN] ✓ Token response succesfuldt modtaget
[SAXO-TOKEN] [STEP 5] Krypterer nyt refresh token...
[SAXO-TOKEN] ✓ Nyt token gemt sikkert på disk
[SAXO-TOKEN] ========== ✓ TOKEN REFRESH SUCCESFULDT ==========

[SAXO-BALANCE] Henter balance fra Saxo API...
[SAXO-BALANCE] API Endpoint: https://gateway.saxobank.com/openapi/port/v1/balances/me
[SAXO-BALANCE] [STEP 1] Sender GET request...
[SAXO-BALANCE] [STEP 2] Response status: 200
[SAXO-BALANCE] ✓ Balance response modtaget (længde: 500 tegn)
[SAXO-BALANCE] ✓ Total værdi: 500000.00 DKK
[JOB] ✓ Saxo balance: 500000.00 DKK

[JOB] [2/4] Starter aktiepriser hentning...
[JOB] ✓ Aktieværdi: 100000.00 DKK

[JOB] [3/4] Starter fondsværdi hentning...
[JOB] ✓ Fondsværdi: 50000.00 DKK

[JOB] [4/4] TOTAL værdi: 650000.00 DKK
[JOB] Sender data til Google Sheets...
[JOB] ✓ Google Sheets opdateret succesfuldt

╔═══════════════════════════════════════════╗
║  JOB AFSLUTTET - SUCCESFULDT               ║
║  Total værdi: 650000.00 DKK               ║
╚═══════════════════════════════════════════╝
```

---

## 🔍 **Symbole i logs**

| Symbol | Betydning |
|--------|-----------|
| ✓      | Succesfuldt/OK |
| ✗      | Fejl/Problem |
| ⚠      | Advarsel (ikke kritisk) |
| [TAG]  | Log kategori |

---

## 📍 **Vigtige steder at tjekke**

### **I Visual Studio - Output window:**

1. Højreklik på "Output" tab
2. Vælg "Output from: Debug" (eller "All")
3. Søg efter `[SAXO` eller `[JOB` for at se relevante logs

### **I Docker:**

```bash
# Se live logs
docker logs -f stockprice-worker

# Søg efter Saxo logs
docker logs stockprice-worker | grep "SAXO"
```

### **I Azure (hvis deployet der):**

1. App Service > Log stream
2. Eller: Container registries > Logs

---

## 🎓 **Hvad hver log fortæller**

### **Login-flow (første gang):**

1. **[SAXO-LOGIN]** = Bruger klikker på login link
2. **[SAXO-CALLBACK]** = Saxo sender dig tilbage med en auth-code
3. **Callback gemmer tokens sikkert på disk**

### **Automatisk daily job:**

1. **[SCHEDULER]** = Timer checker om det er tid
2. **[JOB] [1/4]** = Henter Saxo balance (bruger cached refresh token)
3. **[SAXO-TOKEN]** = Token refresh sker automatisk
4. **[JOB] [2/4-4/4]** = Henter aktier, fonde, opdaterer sheets

### **Fejlfinding:**

- Hvis du ser `⚠` (advarsel) = appen kan stadig køre, men en del mangler
- Hvis du ser `✗` (fejl) = dette punkt fejlede, check det specifikt
- Hvis du ser exception med stacktrace = noget gik virkelig galt

---

## 💡 **Eksempel: Debug login-fejl**

Bruger rapporterer: "Jeg får blank side efter at logge ind i Saxo"

**Trin:**

1. Check logs for `[SAXO-CALLBACK]`
2. Find linjen `[SAXO-CALLBACK] [STEP X.Y]` hvor det fejler
3. Se status code:
   - `400` = Credentials problem
   - `401` = Redirect URL problem
   - `500` = Saxo server problem
4. Løs baseret på fejltypen

---

## 🚀 **Best Practice**

1. **Ved hver deploy:** Start serveren og tjek at du ser "WORKER STARTET" loggen
2. **Før du går live:** Test login-flowet helt manuelt
3. **Daglig:** Se på om daily job kørte (tjek 03:30+ logs)
4. **Ved fejl:** Kopier hele log-blokken fra `[SAXO-...] STARTER` til `AFSLUTTET` og analyser

---

**Last updated:** 2024
