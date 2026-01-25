# TamponiInventario WPF - Requisiti e installazione

## Requisiti minimi
- **Windows 10/11** (x64 consigliato).
- **.NET 6 Desktop Runtime** se si usa un build *framework-dependent*.
  - Per evitare prerequisiti, usa il publish **self-contained** (vedi sotto).
- **Accesso a file system locale** per il database SQLite (cartella dell'app o percorso configurato).

## Dipendenze e librerie
- **SQLite**
  - Il progetto `InventoryDataAccess` usa `Microsoft.Data.Sqlite` (dipendenza NuGet).
  - In publish *self-contained* le native SQLite vengono incluse automaticamente.
  - In publish *framework-dependent* assicurati che il runtime .NET 6 sia presente sul client.
- **Barcode/QR**
  - Nel progetto WPF sono elencate librerie suggerite (UI/placeholder) ma **non sono installate** di default.
  - Librerie consigliate:
    - `ZXing.Net` (decodifica 1D/2D con supporto WPF via `BitmapSource`).
    - `BarcodeLib` (funzionalità base).
    - `QRCoder` (solo generazione QR).
  - Per la camera: `AForge.Video.DirectShow`, `Accord.Video.DirectShow` o `Windows.Media.Capture`.
  - Se abiliti la scansione reale, aggiungi i package NuGet richiesti e aggiorna la pipeline di acquisizione.

## Publish ClickOnce (consigliato per distribuzione rapida)
È presente un profilo di publish pronto:
- `TamponiInventario.Wpf/Properties/PublishProfiles/ClickOnce.pubxml`

### Da Visual Studio
1. Apri `TamponiInventario.Wpf`.
2. **Publish** → seleziona il profilo **ClickOnce**.
3. Scegli cartella di output (default `bin\Release\net6.0-windows\publish\clickonce`).
4. Esegui il publish. Verranno generati `setup.exe` e i manifest.

### Da CLI
```bash
# dalla root del repo
cd TamponiInventario.Wpf

dotnet publish -c Release -p:PublishProfile=ClickOnce
```

> Suggerimento: imposta `ApplicationVersion` e `PublisherName` nel profilo se serve versionamento e firma.

## Installer MSI (per deployment gestito)
Per un MSI classico è consigliato usare **WiX Toolset**.
Il profilo `Msi.pubxml` prepara i binari da impacchettare.

### 1) Genera i binari da distribuire
```bash
cd TamponiInventario.Wpf

dotnet publish -c Release -p:PublishProfile=Msi
```
Output in `bin\Release\net6.0-windows\publish\msi`.

### 2) Crea l’MSI con WiX
1. Installa **WiX Toolset v4** (o v3 se preferisci).
2. Crea un progetto WiX e punta ai binari pubblicati.
3. Compila il progetto WiX per ottenere l’installer MSI.

> Se vuoi evitare prerequisiti, usa `SelfContained=true` nel profilo `Msi.pubxml`.

## Note operative
- Verifica che il database SQLite sia in una posizione scrivibile.
- Se usi webcam o scanner USB, assicurati che i driver siano installati sul client.
