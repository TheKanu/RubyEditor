# 🎮 Ruby GameEditor - WoW-Style Zone Building Tool

## 🚀 Quick Start

### 1. Installation
1. Kopiere alle RubyEditor Scripts in deinen Unity Project
2. Erstelle einen leeren GameObject in der Szene
3. Füge das `RubyEditorBootstrap.cs` Script hinzu
4. Drücke Play!

### 2. Erste Zone erstellen
```
1. Der Editor startet automatisch mit einer neuen Zone
2. Drücke W für Place Mode
3. Wähle ein Objekt aus der Tool Palette
4. Klicke in die Szene zum Platzieren
```

## 🎯 Editor Modi

### Q - Select Mode
- Objekte auswählen und bearbeiten
- Transform Gizmos nutzen
- Properties im Inspector ändern

### W - Place Mode  
- Props aus der Library platzieren
- **R** - Rotieren während Platzierung
- **Shift + Click** - Mehrfach platzieren
- **ESC** - Platzierung abbrechen

### E - Paint Mode
- Terrain Texturen malen
- **Scroll** - Textur wechseln
- **[ ]** - Pinselgröße ändern
- **- +** - Pinselstärke ändern

### R - Terrain Mode
- **1** - Raise (Anheben)
- **2** - Lower (Absenken)  
- **3** - Smooth (Glätten)
- **4** - Flatten (Ebnen)
- **5** - Paint (Texturen)
- **6** - Path (Wege)

## ⌨️ Wichtige Hotkeys

### Allgemein
- **G** - Grid Snap an/aus
- **Ctrl+S** - Zone speichern
- **Ctrl+O** - Zone öffnen
- **Ctrl+N** - Neue Zone
- **Ctrl+Z/Y** - Undo/Redo

### Kamera
- **WASD** - Bewegen
- **Q/E** - Hoch/Runter
- **Rechte Maus** - Drehen
- **Mittlere Maus** - Schwenken
- **Mausrad** - Zoom
- **Shift** - Schneller bewegen

### Terrain Editing
- **[ ]** - Pinselgröße
- **- +** - Pinselstärke  
- **Scroll** - Textur wechseln (Paint Mode)

## 📁 Projekt Struktur

```
Assets/
├── StreamingAssets/
│   ├── Zones/          # Gespeicherte Zonen
│   ├── Props/          # 3D Modelle
│   │   ├── Nature/
│   │   ├── Buildings/
│   │   └── Decorations/
│   ├── Materials/      # Materials
│   └── Textures/       # Texturen
└── Scripts/
    └── RubyEditor/     # Editor Scripts
```

## 🏗️ Zone Building Workflow

### 1. Terrain erstellen
1. Wechsle zu **Terrain Mode (R)**
2. Forme das Terrain mit Raise/Lower
3. Glätte raue Stellen mit Smooth
4. Male Texturen mit Paint

### 2. Props platzieren
1. Wechsle zu **Place Mode (W)**
2. Wähle Kategorie (Nature, Buildings, etc.)
3. Klicke auf gewünschtes Prop
4. Platziere im Level

### 3. Spawn Points setzen
1. Nutze das Spawn Points Panel
2. "Add Player Spawn" für Spieler Start
3. "Add NPC Spawn" für NPCs
4. Positioniere im Scene View

### 4. Environment einstellen
1. Öffne Zone Settings
2. Stelle Fog Color/Density ein
3. Passe Ambient Light an
4. Setze Zone Grenzen

### 5. Speichern
1. **Ctrl+S** oder Save Button
2. Wähle Speicherort
3. Zone wird als .zone Datei gespeichert

## 🎨 Prop Database

### Neue Props hinzufügen
1. Importiere 3D Modelle in Props Ordner
2. Öffne Prop Database im Inspector
3. Klicke "Auto Scan"
4. Props werden automatisch kategorisiert

### Props organisieren
- **Nature**: Bäume, Steine, Pflanzen
- **Buildings**: Häuser, Mauern, Türme
- **Props**: Kisten, Fässer, Möbel
- **Lights**: Fackeln, Lampen
- **Effects**: Partikel, VFX

## 🔧 Erweiterte Features

### Grid Snapping
- **G** - Toggle Grid
- Grid Size in Zone Settings anpassen
- Hilft bei präziser Platzierung

### Multi-Select
- **Ctrl + Click** - Zur Auswahl hinzufügen
- **Shift + Drag** - Box Selection
- Bearbeite mehrere Objekte gleichzeitig

### Prefab Variations
- Halte **V** beim Platzieren
- Zufällige Rotation/Scale
- Erstellt natürlichere Umgebungen

## 💡 Tipps & Tricks

### Performance
- Nutze LODs für große Props
- Kombiniere kleine Objekte
- Optimiere Terrain Resolution

### Workflow
- Beginne mit groben Terrain Formen
- Platziere große Props zuerst
- Verfeinere mit kleinen Details
- Teste regelmäßig im Play Mode

### Backup
- Speichere verschiedene Versionen
- Nutze sinnvolle Namen
- Exportiere fertige Zonen

## 🐛 Fehlerbehebung

### Editor startet nicht
1. Prüfe Console auf Fehler
2. Stelle sicher dass alle Scripts vorhanden sind
3. Lösche Library Ordner und starte Unity neu

### Props werden nicht angezeigt
1. Prüfe Prop Database
2. Führe "Auto Scan" aus
3. Stelle sicher dass Prefabs korrekt sind

### Terrain Tools funktionieren nicht
1. Terrain muss in Szene sein
2. Terrain Layer müssen zugewiesen sein
3. Prüfe ob Terrain ausgewählt ist

## 📚 Weitere Ressourcen

- Unity Terrain Documentation
- Mirror Networking Guide
- WoW Level Design Principles

## 🎮 Viel Spaß beim Zone Building!

Erstelle epische Welten für dein MMO! Bei Fragen oder Problemen, check die Console Logs oder erstelle ein Issue.

---
Ruby GameEditor v1.0 - Made for WoW-Style MMO Development