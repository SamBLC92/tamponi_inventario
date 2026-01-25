# Swab Inventory Web

Web app Flask + SQLite per gestione **tamponi** con barcode, presi/resi, macchina su PRESO, conteggio giorni unici d'uso.

## Avvio
```bash
pip install -r requirements.txt
python app.py
```

Apri:
- http(s)://<IP>:5000/swabs
- http(s)://<IP>:5000/scan-camera

## Password admin
Imposta la variabile ambiente:
- Windows (PowerShell): `setx ADMIN_PASSWORD "LaTuaPassword"` (poi riapri terminale)
- Linux/macOS: `export ADMIN_PASSWORD="LaTuaPassword"`

La gestione è protetta solo per:
- aggiunta tampone (POST)
- modifica/elimina tampone
- aggiunta/elimina macchine (POST)

Le pagine **/swabs** e **/history** restano consultabili liberamente.
