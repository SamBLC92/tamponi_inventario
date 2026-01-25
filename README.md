# Swab Inventory Web

Web app Flask + SQLite per gestione **tamponi** con barcode, presi/resi, macchina su PRESO, conteggio giorni unici d'uso.

## TamponiInventario WPF (desktop)
La versione desktop WPF non è presente in questo repository: va creata ex-novo e poi documentata in un
`INSTALLATION.md` dedicato.

## Avvio
```bash
pip install -r requirements.txt
python app.py
```

Apri:
- http(s)://<IP>:5000/swabs
- http(s)://<IP>:5000/scan-camera

## Configurazione locale (admin)
Crea un file `appsettings.local.json` (non versionato) partendo da `appsettings.example.json` e proteggilo con permessi 600:

```bash
cp appsettings.example.json appsettings.local.json
chmod 600 appsettings.local.json
```

Puoi anche impostare un percorso alternativo con `APPSETTINGS_PATH`.

Esempio di contenuto:
```json
{
  "admin_password": "UnaPasswordForte",
  "flask_secret_key": "StringaCasualeLunga"
}
```

La gestione è protetta solo per:
- aggiunta tampone (POST)
- modifica/elimina tampone
- aggiunta/elimina macchine (POST)

Le pagine **/swabs** e **/history** restano consultabili liberamente.
