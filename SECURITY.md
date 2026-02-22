# Sikkerhedspolitik - Stock Price Sheet Print Service

## 🔒 Miljøvariabler og Hemmeligheder

### Konfiguration
- **ALDRIG** commit credentials, API-nøgler eller tokens til git
- Brug `.env` filer lokalt (gitignored)
- I produktion: Brug Azure KeyVault, AWS Secrets Manager eller lignende

### Secrets Hierarki (Prioritet)
1. **Production**: Azure KeyVault / AWS Secrets Manager / Kubernetes Secrets
2. **Development**: Local user secrets (`dotnet user-secrets`)
3. **Local Testing**: `.env` filer (gitignored)
4. **ALDRIG**: Hardcoded værdier i source code

## 🔐 Sikker Token-Håndtering

### Saxo Bank Tokens
- **Access Tokens**: Kortvarende (kan caches i memory)
- **Refresh Tokens**: Skal ALTID være krypteret og sikkert lagret
- **OAuth Secrets**: Skal aldrig logges eller vises

### Google Sheets Credentials
- Service account JSON skal gemmes i sikker location
- Sti skal konfigureres via environment variable
- Aldrig commit credentials-filer

## 🚨 Audit Logging

Alle følgende operationer logges:
- OAuth login attempts (uden sensitive data)
- Token refresh operations
- Google Sheets API calls
- Configuration errors (uden secrets)

## 📋 Pre-Commit Checklist

Før du pusher:
```bash
# Check for secrets
git diff --cached | grep -i "password\|secret\|token\|key\|api"

# Verify .gitignore
git check-ignore Secrets/
git check-ignore *.txt
git check-ignore .env
```

## 🔧 Lokal Setup

### Udvikling
```bash
# Kopier template
cp .env.example .env

# Edit med dine development credentials (ALDRIG push!)
nano .env

# Load local secrets (ikke i git)
dotnet user-secrets set "Saxo:AppKey" "your_key"
dotnet user-secrets set "Saxo:AppSecret" "your_secret"
```

### Docker / Production
```bash
# Brug environment variables
docker run -e SAXO_APP_KEY=xxx -e SAXO_APP_SECRET=yyy ...

# ELLER Kubernetes secrets
kubectl apply -f secrets.yaml
```

## 🛡️ Best Practices

- ✅ Alle external API calls har error handling
- ✅ Sensitive data er ALDRIG logget
- ✅ HTTPS er enforced
- ✅ CORS konfigureres restriktivt
- ✅ Refresh tokens roteres automatisk
- ✅ Access tokens cachet minimalt

## 🚫 Ting der er forbudt

- ❌ Lagring af plaintext tokens på disk
- ❌ Hardcoded credentials
- ❌ Sensitve data i logs
- ❌ Default credentials i production
- ❌ Credentials i URL-parameters (brug POST body)
- ❌ Eksponering af error stack traces til client

## 📞 Rapportering af Sikkerhedsproblem

Hvis du opdager et sikkerhedsproblem:
1. Rapportér det PRIVAT til team lead
2. Åbn IKKE issue offentligt med sensitive data
3. Beskriv problemet uden at vise hemmeligheder

---
Last updated: 2024
